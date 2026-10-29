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

        private List<TimelinePlayer> runtimeChildren = new List<TimelinePlayer>();
        private TimelinePlayer runtimeParent;
        private readonly List<GameObject> ownedInstances = new List<GameObject>();

        public event Action<PlayableDirector> Stopped {
            add => Director.stopped += value;
            remove => Director.stopped -= value;
        }

        private static readonly IReadOnlyDictionary<string, object> EmptyBindingMap = new Dictionary<string, object>();

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
            if (Director.state == PlayState.Playing) {
                // Started by Play On Awake before the bindings were applied.
                Debug.LogWarning($"Timeline '{name}' was already playing before {nameof(Init)}; " +
                                 "disable Play On Awake on its PlayableDirector.", this);
                Director.Stop();
            }

            RestoreBindings(bindingMap);

            initialized = true;
        }

        /// <summary>
        /// Play this timeline.
        /// </summary>
        /// <param name="autoDestroyOnStop">Auto destroy when the timeline stop.</param>
        public void PlayTimeline(bool autoDestroyOnStop = false) {
            if (!initialized) {
                Debug.LogWarning($"{nameof(PlayTimeline)} called on '{name}' before {nameof(Init)}; ignoring.", this);
                return;
            }

            Director.stopped -= OnDirectorStopped;
            if (autoDestroyOnStop) {
                Director.stopped += OnDirectorStopped;

                var wrapMode = Director.extrapolationMode;
                if (wrapMode == DirectorWrapMode.Hold || wrapMode == DirectorWrapMode.Loop) {
                    Debug.LogWarning($"Timeline '{name}' uses wrap mode {wrapMode}, which never stops by itself, " +
                                     $"so it won't auto destroy; call {nameof(ClearTimeline)} instead.", this);
                }
            }

            Director.time = 0d;
            Director.Play();
        }

        /// <summary>
        /// Clear the timeline and remove all the runtime children.
        /// </summary>
        public void ClearTimeline() {
            Director.Stop();
            ReleaseRuntime();
            Destroy(gameObject);
        }

        private void OnDestroy() {
            ReleaseRuntime();
        }

        private void OnDirectorStopped(PlayableDirector director) {
            ClearTimeline();
        }

        /// <summary>
        /// Detach from the parent and destroy the sub timeline instances this player created.
        /// </summary>
        private void ReleaseRuntime() {
            if (playableDirector != null) {
                playableDirector.stopped -= OnDirectorStopped;
            }

            foreach (var child in runtimeChildren) {
                if (child == null) continue;

                child.runtimeParent = null;
                child.ReleaseRuntime();
            }
            runtimeChildren.Clear();

            foreach (var instance in ownedInstances) {
                if (instance != null) Destroy(instance);
            }
            ownedInstances.Clear();

            if (runtimeParent != null) {
                runtimeParent.runtimeChildren.Remove(this);
                runtimeParent.ownedInstances.Remove(gameObject);
                runtimeParent = null;
            }
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
                var subPlayer = subPlayerBindInfo.subPlayer;
                if (subPlayer == null) continue;

                TimelinePlayer player;
                if (subPlayer.transform.IsChildOf(transform)) {
                    // Child sub player is already referenced by this director.
                    player = subPlayer;
                } else {
                    var instance = Instantiate(subPlayer.gameObject);
                    ownedInstances.Add(instance);
                    Director.SetReferenceValue(hashName, instance);
                    player = instance.GetComponent<TimelinePlayer>();
                }

                var key = subPlayerBindInfo.key;
                if (key == null ||
                    !bindingMap.TryGetValue(key, out var value) ||
                    !(value is IReadOnlyDictionary<string, object> subBindingMap)) {
                    Debug.LogWarning($"No binding map found for sub timeline '{key}'", this);
                    subBindingMap = EmptyBindingMap;
                }
                player.RestoreBindings(subBindingMap, this);

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
                Debug.LogError($"{nameof(SaveTimeline)} must run on an instance in a scene or Prefab Mode, " +
                               "not on a prefab asset.", this);
                return;
            }

            var oldControlBindings = controlBindInfos;
            var oldTrackBindings = trackBindInfos;
            var oldSubTimelines = subTimelines;
            controlBindInfos = new List<ControlBindInfo>();
            trackBindInfos = new List<TrackBindInfo>();

            var oldControlBindingMap = new Dictionary<PlayableAsset, ControlBindInfo>();
            if (oldControlBindings != null) {
                foreach (var oldControlBinding in oldControlBindings) {
                    if (oldControlBinding.playableAsset == null) continue;
                    oldControlBindingMap[oldControlBinding.playableAsset] = oldControlBinding;
                }
            }

            var oldTrackBindingMap = new Dictionary<TrackAsset, TrackBindInfo>();
            if (oldTrackBindings != null) {
                foreach (var oldTrackBinding in oldTrackBindings) {
                    if (oldTrackBinding.trackAsset == null) continue;
                    oldTrackBindingMap[oldTrackBinding.trackAsset] = oldTrackBinding;
                }
            }

            var oldSubTimelineMap = new Dictionary<PlayableAsset, SubPlayerBindInfo>();
            if (oldSubTimelines != null) {
                foreach (var oldSubTimeline in oldSubTimelines) {
                    if (oldSubTimeline.playableAsset == null) continue;
                    oldSubTimelineMap[oldSubTimeline.playableAsset] = oldSubTimeline;
                }
            }

            subTimelines = new List<SubPlayerBindInfo>();
            var timelineAsset = (TimelineAsset) Director.playableAsset;
            foreach (var track in timelineAsset.GetOutputTracks()) {
                if (track == timelineAsset.markerTrack) continue;

                var hasBindableOutput = false;
                foreach (var output in track.outputs) {
                    if (output.outputTargetType != null) {
                        hasBindableOutput = true;
                        break;
                    }
                }

                if (hasBindableOutput) {
                    var bindingObject = Director.GetGenericBinding(track);
                    var isChildBinding =
                        bindingObject is Component component && component.transform.IsChildOf(transform) ||
                        bindingObject is GameObject go && go.transform.IsChildOf(transform);
                    if (!isChildBinding) {
                        var trackBindInfo = new TrackBindInfo {
                            trackAsset = track,
                        };
                        trackBindInfo.key = oldTrackBindingMap.TryGetValue(track, out var oldTrackBindInfo)
                            ? oldTrackBindInfo.key
                            : track.name;
                        trackBindInfos.Add(trackBindInfo);
                    }
                }
                foreach (var timelineClip in track.GetClips()) {
                    var asset = (PlayableAsset) timelineClip.asset;
                    if (!(asset is ControlPlayableAsset controlPlayableAsset)) continue;
                    var bindGO = controlPlayableAsset.sourceGameObject.Resolve(Director);
                    if (bindGO == null) continue;
                    
                    if (bindGO.TryGetComponent<PlayableDirector>(out _) && bindGO.TryGetComponent<TimelinePlayer>(out var subPlayer)) {
                        var subKey = oldSubTimelineMap.TryGetValue(controlPlayableAsset, out var oldSubTimeline)
                            ? oldSubTimeline.key
                            : timelineClip.displayName;

                        if (bindGO.transform.IsChildOf(transform)) {
                            subTimelines.Add(new SubPlayerBindInfo {
                                key = subKey,
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
                                    key = subKey,
                                    hash = controlPlayableAsset.sourceGameObject.exposedName.GetHashCode(),
                                    playableAsset = controlPlayableAsset,
                                    subPlayer = prefabPlayer,
                                });
                            } else {
                                Debug.LogError($"Sub timeline '{subKey}': prefab '{prefabPath}' has no " +
                                               $"{nameof(TimelinePlayer)} on its root; skipped.", subPlayer);
                            }
                        } else {
                            Debug.LogError($"Sub timeline '{subKey}' ('{bindGO.name}') must be a child of '{name}' " +
                                           "or a prefab instance root to be restored at runtime; skipped.", bindGO);
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
                subPlayerBindMap[subPlayerBindInfo.hash] = subPlayerBindInfo;
            }

            controlBindMap = new Dictionary<ControlPlayableAsset, ControlBindInfo>(controlBindInfos.Count);
            foreach (var controlBindInfo in controlBindInfos) {
                if (controlBindInfo.playableAsset is null) continue;
                controlBindMap[controlBindInfo.playableAsset] = controlBindInfo;
            }

            trackBindMap = new Dictionary<TrackAsset, TrackBindInfo>(trackBindInfos.Count);
            foreach (var trackBindInfo in trackBindInfos) {
                if (trackBindInfo.trackAsset is null) continue;
                trackBindMap[trackBindInfo.trackAsset] = trackBindInfo;
            }
        }
    }
}