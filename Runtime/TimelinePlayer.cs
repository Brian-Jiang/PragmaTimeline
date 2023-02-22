using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;
 
namespace PragmaFramework.Timeline.Runtime {
    public class TimelinePlayer : MonoBehaviour {
        public TimelineHolder holder;

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
            foreach (var track in timelineAsset.GetOutputTracks()) {
                if (track == timelineAsset.markerTrack) continue;
                
                if (holder.trackBindMap.TryGetValue(track, out var trackBindInfo)) {
                    var key = trackBindInfo.key;
                    if (bindingMap.TryGetValue(key, out var value)) {
                        Director.SetGenericBinding(track, value);
                    }
                }
                
                foreach (var timelineClip in track.GetClips()) {
                    if (timelineClip.asset is ControlPlayableAsset controlPlayableAsset) {
                        if (holder.controlBindMap.TryGetValue(controlPlayableAsset, out var controlBindInfo)) {
                            var key = controlBindInfo.key;
                            if (bindingMap.TryGetValue(key, out var value)) {
                                Director.SetReferenceValue(controlPlayableAsset.sourceGameObject.exposedName, value);
                            }
                        }
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

        [Button, ShowIn(PrefabKind.InstanceInScene)]
        public void SaveTimeline() {
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

            var timelineAsset = (TimelineAsset) Director.playableAsset;
            foreach (var track in timelineAsset.GetOutputTracks()) {
                if (track == timelineAsset.markerTrack) continue;
                
                var outputs = track.outputs;
                
                foreach (var binding in outputs) {
                    if (binding.outputTargetType != null) {
                        var trackBindInfo = new TrackBindInfo {
                            trackAsset = track,
                        };
                        if (oldTrackBindingMap.TryGetValue(track, out var oldTrackBindInfo)) {
                            trackBindInfo.key = oldTrackBindInfo.key;
                        }
                        holder.trackBindInfos.Add(trackBindInfo);
                    }
                }
                foreach (var timelineClip in track.GetClips()) {
                    var asset = (PlayableAsset) timelineClip.asset;
                    if (asset is ControlPlayableAsset controlPlayableAsset &&
                        controlPlayableAsset.sourceGameObject.Resolve(Director) != null) {
                        var controlBindInfo = new ControlBindInfo {
                            trackAsset = track,
                            playableAsset = controlPlayableAsset,
                        };
                        if (oldControlBindingMap.TryGetValue(controlPlayableAsset, out var oldControlBindInfo)) {
                            controlBindInfo.key = oldControlBindInfo.key;
                        }
                        holder.controlBindInfos.Add(controlBindInfo);
                    }
                }
            }
        }

    }
}