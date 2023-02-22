using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

namespace PragmaFramework.Timeline.Runtime {
    [Serializable]
    public struct ControlBindInfo {
        public string key;
        public TrackAsset trackAsset;
        public ControlPlayableAsset playableAsset;
    }

    [Serializable]
    public struct TrackBindInfo {
        public string key;
        public TrackAsset trackAsset;
    }
    
    [Serializable]
    public class TimelineHolder: ISerializationCallbackReceiver {
        public List<ControlBindInfo> controlBindInfos;
        public List<TrackBindInfo> trackBindInfos;
        public Dictionary<TrackAsset, TrackBindInfo> trackBindMap;
        public Dictionary<ControlPlayableAsset, ControlBindInfo> controlBindMap;
        
        public void OnBeforeSerialize() {
            
        }

        public void OnAfterDeserialize() {
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