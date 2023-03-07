using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;
 
namespace PragmaFramework.Timeline.Runtime {
    [RequireComponent(typeof(PlayableDirector))]
    public class TimelinePlayer : MonoBehaviour, ISerializationCallbackReceiver {
        public TimelineHolder holder;
        public List<SubPlayerBindInfo> subTimelines;

        private Dictionary<PropertyName, SubPlayerBindInfo> subPlayerBindMap;

        private TimelinePlayer parent;

        public event Action<PlayableDirector> Stopped {
            add => Director.stopped += value;
            remove => Director.stopped -= value;
        }

        private bool initialized;

        private PlayableDirector playableDirector;
        public PlayableDirector Director {
            get {
                if (playableDirector == null) {
                    playableDirector = GetComponent<PlayableDirector>();
                }

                return playableDirector;
            }
        }

        public void Init(Dictionary<string, Object> bindingMap) {
            RestoreBindings(bindingMap);

            initialized = true;
        }

        public void PlayTimeline(bool autoDestroyOnStop = false) {
            if (!initialized) {
                return;
            }

            if (autoDestroyOnStop) {
                Director.stopped += director => {
                    ClearTimeline();
                };
            }
            
            Director.time = 0d;
            Director.Play();
        }

        public void ClearTimeline() {
            Destroy(gameObject);
        }

        public void RestoreBindings(Dictionary<string, Object> bindingMap) {
            var timelineAsset = (TimelineAsset) Director.playableAsset;
            foreach (var (_, controlBindInfo) in holder.controlBindMap) {
                var key = controlBindInfo.key;
                if (bindingMap.TryGetValue(key, out var value)) {
                    Director.SetReferenceValue(controlBindInfo.hash, value);
                }
            }
            
            foreach (var (hashName, subPlayerBindInfo) in subPlayerBindMap) {
                Director.SetReferenceValue(hashName, subPlayerBindInfo.subPlayer.gameObject);
            }
            
            foreach (var track in timelineAsset.GetOutputTracks()) {
                if (track == timelineAsset.markerTrack) continue;
                
                if (holder.trackBindMap.TryGetValue(track, out var trackBindInfo)) {
                    var key = trackBindInfo.key;
                    if (bindingMap.TryGetValue(key, out var value)) {
                        Director.SetGenericBinding(track, value);
                    }
                }
            }
            // foreach (var track in Director.playableAsset.outputs)
            // {
            //     if (track.sourceObject is ControlTrack)
            //     {
            //         ControlTrack ct = (ControlTrack)track.sourceObject;
            //         if (ct.name == "nestedTimeline" || true)
            //         {
            //             foreach (TimelineClip timelineClip in ct.GetClips())
            //             {
            //                 ControlPlayableAsset playableAsset = (ControlPlayableAsset)timelineClip.asset;
            //                 // playableAsset.postPlayback = ActivationControlPlayable.PostPlaybackState.Revert;
            //                 // playableAsset.updateDirector = false;
            //                 // playableAsset.updateParticle = false;
            //
            //                 var hash = playableAsset.GetHashCode();
            //                 var id = playableAsset.GetInstanceID();
            //                 print($"hash: {hash}, id: {id}");
            //                 // set the reference of the nested timeline to the parent playable asset
            //                 // Director.SetReferenceValue(playableAsset.sourceGameObject.exposedName, nestedTimeline.gameObject);
            //                 
            //                 // rebind the playableGraph of the parent timeline director
            //                 Director.RebindPlayableGraphOutputs();
            //             }
            //         }
            //     }
            // }
        }

        public void SaveTimeline() {
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject)) {
                Debug.LogError("Is prefab asset");
                return;
            }

            var oldControlBindings = holder.controlBindInfos;
            var oldTrackBindings = holder.trackBindInfos;
            holder.controlBindInfos = new List<ControlBindInfo>();
            holder.trackBindInfos = new List<TrackBindInfo>();

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
                    
                    var bindingObject = (Component) Director.GetGenericBinding(track);
                    if (bindingObject.transform.IsChildOf(transform)) continue;
                        
                    var trackBindInfo = new TrackBindInfo {
                        trackAsset = track,
                    };
                    trackBindInfo.key = oldTrackBindingMap.TryGetValue(track, out var oldTrackBindInfo)
                        ? oldTrackBindInfo.key
                        : track.name;
                    holder.trackBindInfos.Add(trackBindInfo);
                }
                foreach (var timelineClip in track.GetClips()) {
                    var asset = (PlayableAsset) timelineClip.asset;
                    if (asset is not ControlPlayableAsset controlPlayableAsset) continue;
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

                        holder.controlBindInfos.Add(controlBindInfo);
                    }
                }
            }
        }

        public void OnBeforeSerialize() {
            
        }

        public void OnAfterDeserialize() {
            subPlayerBindMap = new Dictionary<PropertyName, SubPlayerBindInfo>();
            foreach (var subPlayerBindInfo in subTimelines) {
                subPlayerBindMap.Add(subPlayerBindInfo.hash, subPlayerBindInfo);
            }
        }
    }
}