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
    public class TimelinePlayer : MonoBehaviour {
        /// <summary>
        /// Bind info for exposed references of tracks, clips and markers, including control clips and custom ones.
        /// </summary>
        public List<ControlBindInfo> controlBindInfos;

        /// <summary>
        /// Bind info for track.
        /// </summary>
        public List<TrackBindInfo> trackBindInfos;
        private Dictionary<TrackAsset, TrackBindInfo> trackBindMap;

        /// <summary>
        /// Bind info for child timelines
        /// </summary>
        public List<SubPlayerBindInfo> subTimelines;

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
            foreach (var controlBindInfo in controlBindInfos) {
                var key = controlBindInfo.key;
                if (key != null && bindingMap.TryGetValue(key, out var value)) {
                    Director.SetReferenceValue(controlBindInfo.exposedName, (Object) value);
                }
            }

            runtimeChildren = new List<TimelinePlayer>(subTimelines.Count);
            foreach (var subPlayerBindInfo in subTimelines) {
                var subPlayer = subPlayerBindInfo.subPlayer;
                if (subPlayer == null) continue;

                TimelinePlayer player;
                if (subPlayer.transform.IsChildOf(transform)) {
                    // Child sub player is already referenced by this director.
                    player = subPlayer;
                } else {
                    var instance = Instantiate(subPlayer.gameObject);
                    ownedInstances.Add(instance);
                    Director.SetReferenceValue(subPlayerBindInfo.exposedName, instance);
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
            
            trackBindMap ??= BuildTrackBindMap();
            foreach (var track in timelineAsset.GetOutputTracks()) {
                if (track == timelineAsset.markerTrack) continue;

                if (trackBindMap.TryGetValue(track, out var trackBindInfo)) {
                    var key = trackBindInfo.key;
                    if (key != null && bindingMap.TryGetValue(key, out var value)) {
                        Director.SetGenericBinding(track, (Object) value);
                    }
                }
            }
        }

        private Dictionary<TrackAsset, TrackBindInfo> BuildTrackBindMap() {
            var map = new Dictionary<TrackAsset, TrackBindInfo>(trackBindInfos.Count);
            foreach (var trackBindInfo in trackBindInfos) {
                if (trackBindInfo.trackAsset == null) continue;
                map[trackBindInfo.trackAsset] = trackBindInfo;
            }

            return map;
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
            trackBindMap = null;

            var oldControlBindingMap = new Dictionary<PropertyName, ControlBindInfo>();
            if (oldControlBindings != null) {
                foreach (var oldControlBinding in oldControlBindings) {
                    oldControlBindingMap[oldControlBinding.exposedName] = oldControlBinding;
                }
            }

            var oldTrackBindingMap = new Dictionary<TrackAsset, TrackBindInfo>();
            if (oldTrackBindings != null) {
                foreach (var oldTrackBinding in oldTrackBindings) {
                    if (oldTrackBinding.trackAsset == null) continue;
                    oldTrackBindingMap[oldTrackBinding.trackAsset] = oldTrackBinding;
                }
            }

            var oldSubTimelineMap = new Dictionary<PropertyName, SubPlayerBindInfo>();
            if (oldSubTimelines != null) {
                foreach (var oldSubTimeline in oldSubTimelines) {
                    oldSubTimelineMap[oldSubTimeline.exposedName] = oldSubTimeline;
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
            }

            var exposedOwners = CollectExposedReferences(timelineAsset);
            var references = new SerializedObject(Director).FindProperty("m_ExposedReferences.m_References");
            for (var i = 0; i < references.arraySize; i++) {
                var reference = references.GetArrayElementAtIndex(i);
                var referenceName = reference.FindPropertyRelative("first").stringValue;
                var value = reference.FindPropertyRelative("second").objectReferenceValue;

                if (value == null || !exposedOwners.TryGetValue(referenceName, out var owner)) continue;
                if (EditorUtility.IsPersistent(value)) continue;

                var exposedName = new PropertyName(referenceName);
                var bindGO = value is Component component ? component.gameObject : value as GameObject;

                if (owner.asset is ControlPlayableAsset controlPlayableAsset && bindGO != null &&
                    bindGO.TryGetComponent<PlayableDirector>(out _) && bindGO.TryGetComponent<TimelinePlayer>(out var subPlayer)) {
                    var subKey = oldSubTimelineMap.TryGetValue(exposedName, out var oldSubTimeline)
                        ? oldSubTimeline.key
                        : owner.label;

                    if (bindGO.transform.IsChildOf(transform)) {
                        subTimelines.Add(new SubPlayerBindInfo {
                            key = subKey,
                            exposedName = exposedName,
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
                                exposedName = exposedName,
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
                    if (bindGO != null && bindGO.transform.IsChildOf(transform)) continue;

                    var controlBindInfo = new ControlBindInfo {
                        exposedName = exposedName,
                        trackAsset = owner.track,
                        playableAsset = owner.asset,
                    };
                    controlBindInfo.key = oldControlBindingMap.TryGetValue(exposedName, out var oldControlBindInfo)
                        ? oldControlBindInfo.key
                        : owner.label;

                    controlBindInfos.Add(controlBindInfo);
                }
            }
        }
        
        private static Dictionary<string, (TrackAsset track, PlayableAsset asset, string label)> CollectExposedReferences(
            TimelineAsset timelineAsset) {
            var owners = new Dictionary<string, (TrackAsset track, PlayableAsset asset, string label)>();
            foreach (var track in timelineAsset.GetOutputTracks()) {
                AddExposedReferences(owners, track, track, track, track.name);
                foreach (var timelineClip in track.GetClips()) {
                    var asset = (PlayableAsset) timelineClip.asset;
                    AddExposedReferences(owners, asset, track, asset, timelineClip.displayName);
                }
                foreach (var marker in track.GetMarkers()) {
                    if (marker is Object markerObject) {
                        AddExposedReferences(owners, markerObject, track, null, $"{track.name}.{marker.GetType().Name}");
                    }
                }
            }

            return owners;
        }

        private static void AddExposedReferences(Dictionary<string, (TrackAsset track, PlayableAsset asset, string label)> owners,
            Object source, TrackAsset track, PlayableAsset asset, string label) {
            if (source == null) return;

            var exposedProperties = new List<SerializedProperty>();
            var property = new SerializedObject(source).GetIterator();
            var enterChildren = true;
            while (property.Next(enterChildren)) {
                enterChildren = property.propertyType == SerializedPropertyType.Generic;
                if (property.propertyType == SerializedPropertyType.ExposedReference) {
                    exposedProperties.Add(property.Copy());
                }
            }

            foreach (var exposedProperty in exposedProperties) {
                var exposedName = exposedProperty.FindPropertyRelative("exposedName").stringValue;
                if (string.IsNullOrEmpty(exposedName) || owners.ContainsKey(exposedName)) continue;

                owners[exposedName] = (track, asset,
                    exposedProperties.Count > 1 ? $"{label}.{exposedProperty.displayName}" : label);
            }
        }
#endif
    }
}