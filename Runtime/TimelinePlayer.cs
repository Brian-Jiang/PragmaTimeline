using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif
 
namespace PragmaFramework.Timeline.Runtime {
    [RequireComponent(typeof(PlayableDirector))]
    public class TimelinePlayer : MonoBehaviour, ISerializationCallbackReceiver {
        /// <summary>
        /// Bind info for control Track.
        /// </summary>
        public List<ControlBindInfo> controlBindInfos;
        
        /// <summary>
        /// Bind info for track.
        /// </summary>
        public List<TrackBindInfo> trackBindInfos;
        private Dictionary<TrackAsset, TrackBindInfo> trackBindMap;
        private Dictionary<ControlPlayableAsset, ControlBindInfo> controlBindMap;
        
        /// <summary>
        /// Bind info for child timelines
        /// </summary>
        public List<SubPlayerBindInfo> subTimelines;

        private Dictionary<PropertyName, SubPlayerBindInfo> subPlayerBindMap;

        private List<TimelinePlayer> runtimeChildren;
        private TimelinePlayer runtimeParent;

        public event Action<PlayableDirector> Stopped {
            add => Director.stopped += value;
            remove => Director.stopped -= value;
        }

        private bool initialized;

        private PlayableDirector playableDirector;
        
        /// <summary>
        /// The <c>PlayableDirector</c> of this timeline player.
        /// </summary>
        public PlayableDirector Director {
            get {
                if (playableDirector == null) {
                    playableDirector = GetComponent<PlayableDirector>();
                }

                return playableDirector;
            }
        }

        /// <summary>
        /// Initialize this timeline player with a bindingMap; it only does the initialization and does not play it yet.
        /// </summary>
        /// <param name="bindingMap"></param>
        public void Init(IReadOnlyDictionary<string, object> bindingMap) {
            RestoreBindings(bindingMap);

            initialized = true;
        }

        /// <summary>
        /// Play this timeline.
        /// </summary>
        /// <param name="autoDestroyOnStop">Auto destroy when the timeline stop.</param>
        public void PlayTimeline(bool autoDestroyOnStop = false) {
            if (!initialized) return;

            if (autoDestroyOnStop) {
                Director.stopped += director => {
                    ClearTimeline();
                };
            }
            
            Director.time = 0d;
            Director.Play();
        }

        /// <summary>
        /// Clear the timeline and remove all the runtime children.
        /// </summary>
        public void ClearTimeline() {
            Director.Stop();
            runtimeChildren.ForEach(child => child.ClearTimeline());
            runtimeChildren.ForEach(child => Destroy(child.gameObject));
            runtimeChildren.Clear();
            runtimeParent = null;
            Destroy(gameObject);
        }

        private void RestoreBindings(IReadOnlyDictionary<string, object> bindingMap, TimelinePlayer parent = null) {
            runtimeParent = parent;
            
            var timelineAsset = (TimelineAsset) Director.playableAsset;
            foreach (var controlBind in controlBindMap) {
                var controlBindInfo = controlBind.Value;
                var key = controlBindInfo.key;
                if (bindingMap.TryGetValue(key, out var value)) {
                    Director.SetReferenceValue(controlBindInfo.hash, (Object) value);
                }
            }

            runtimeChildren = new List<TimelinePlayer>(subPlayerBindMap.Count);
            foreach (var subPlayerBind in subPlayerBindMap) {
                var hashName = subPlayerBind.Key;
                var subPlayerBindInfo = subPlayerBind.Value;
                var instance = Instantiate(subPlayerBindInfo.subPlayer.gameObject);
                Director.SetReferenceValue(hashName, instance);
                
                var player = instance.GetComponent<TimelinePlayer>();
                player.RestoreBindings((IReadOnlyDictionary<string, object>) bindingMap[subPlayerBindInfo.key], this);
                
                runtimeChildren.Add(player);
            }
            
            foreach (var track in timelineAsset.GetOutputTracks()) {
                if (track == timelineAsset.markerTrack) continue;
                
                if (trackBindMap.TryGetValue(track, out var trackBindInfo)) {
                    var key = trackBindInfo.key;
                    if (bindingMap.TryGetValue(key, out var value)) {
                        Director.SetGenericBinding(track, (Object) value);
                    }
                }
            }
        }

#if UNITY_EDITOR
        public void SaveTimeline() {
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject)) {
                Debug.LogError("Is prefab asset");
                return;
            }

            var oldControlBindings = controlBindInfos;
            var oldTrackBindings = trackBindInfos;
            controlBindInfos = new List<ControlBindInfo>();
            trackBindInfos = new List<TrackBindInfo>();

            var oldControlBindingMap = new Dictionary<PlayableAsset, ControlBindInfo>();
            if (oldControlBindings != null) {
                foreach (var oldControlBinding in oldControlBindings) {
                    oldControlBindingMap.Add(oldControlBinding.playableAsset, oldControlBinding);
                }
            }

            var oldTrackBindingMap = new Dictionary<TrackAsset, TrackBindInfo>();
            if (oldTrackBindings != null) {
                foreach (var oldTrackBinding in oldTrackBindings) {
                    oldTrackBindingMap.Add(oldTrackBinding.trackAsset, oldTrackBinding);
                }
            }

            subTimelines = new List<SubPlayerBindInfo>();
            var timelineAsset = (TimelineAsset) Director.playableAsset;
            foreach (var track in timelineAsset.GetOutputTracks()) {
                if (track == timelineAsset.markerTrack) continue;
                
                var outputs = track.outputs;
                foreach (var binding in outputs) {
                    if (binding.outputTargetType == null) continue;
                    
                    var bindingObject = Director.GetGenericBinding(track);
                    if (bindingObject is Component component && component.transform.IsChildOf(transform) ||
                        bindingObject is GameObject go && go.transform.IsChildOf(transform)) continue;
                        
                    var trackBindInfo = new TrackBindInfo {
                        trackAsset = track,
                    };
                    trackBindInfo.key = oldTrackBindingMap.TryGetValue(track, out var oldTrackBindInfo)
                        ? oldTrackBindInfo.key
                        : track.name;
                    trackBindInfos.Add(trackBindInfo);
                }
                foreach (var timelineClip in track.GetClips()) {
                    var asset = (PlayableAsset) timelineClip.asset;
                    if (!(asset is ControlPlayableAsset controlPlayableAsset)) continue;
                    var bindGO = controlPlayableAsset.sourceGameObject.Resolve(Director);
                    if (bindGO == null) continue;
                    
                    if (bindGO.TryGetComponent<PlayableDirector>(out _) && bindGO.TryGetComponent<TimelinePlayer>(out var subPlayer)) {
                        if (bindGO.transform.IsChildOf(transform)) {
                            subTimelines.Add(new SubPlayerBindInfo {
                                hash = controlPlayableAsset.sourceGameObject.exposedName.GetHashCode(),
                                playableAsset = controlPlayableAsset,
                                subPlayer = subPlayer,
                            });
                        } else if (PrefabUtility.IsAnyPrefabInstanceRoot(subPlayer.gameObject)) {
                            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(subPlayer);
                            if (string.IsNullOrEmpty(prefabPath)) continue;
                                
                            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                            if (prefab.TryGetComponent<TimelinePlayer>(out var prefabPlayer)) {
                                subTimelines.Add(new SubPlayerBindInfo {
                                    hash = controlPlayableAsset.sourceGameObject.exposedName.GetHashCode(),
                                    playableAsset = controlPlayableAsset,
                                    subPlayer = prefabPlayer,
                                });
                            } else {
                                Debug.LogError(null);
                            }
                        } else {
                            Debug.LogError(null);
                        }
                            
                    } else {
                        if (bindGO.transform.IsChildOf(transform)) continue;
                            
                        var controlBindInfo = new ControlBindInfo {
                            hash = controlPlayableAsset.sourceGameObject.exposedName.GetHashCode(),
                            trackAsset = track,
                            playableAsset = controlPlayableAsset,
                        };
                        controlBindInfo.key =
                            oldControlBindingMap.TryGetValue(controlPlayableAsset, out var oldControlBindInfo)
                                ? oldControlBindInfo.key
                                : timelineClip.displayName;

                        controlBindInfos.Add(controlBindInfo);
                    }
                }
            }
        }
#endif

        public void OnBeforeSerialize() {
            
        }

        public void OnAfterDeserialize() {
            subPlayerBindMap = new Dictionary<PropertyName, SubPlayerBindInfo>();
            foreach (var subPlayerBindInfo in subTimelines) {
                subPlayerBindMap.Add(subPlayerBindInfo.hash, subPlayerBindInfo);
            }
            
            controlBindMap = new Dictionary<ControlPlayableAsset, ControlBindInfo>(controlBindInfos.Count);
            foreach (var controlBindInfo in controlBindInfos) {
                controlBindMap.Add(controlBindInfo.playableAsset, controlBindInfo);
            }

            trackBindMap = new Dictionary<TrackAsset, TrackBindInfo>(trackBindInfos.Count);
            foreach (var trackBindInfo in trackBindInfos) {
                trackBindMap.Add(trackBindInfo.trackAsset, trackBindInfo);
            }
        }
    }
}