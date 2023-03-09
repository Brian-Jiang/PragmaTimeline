using System;
using UnityEngine.Timeline;

namespace PragmaFramework.Timeline.Runtime {
    [Serializable]
    public struct ControlBindInfo {
        public string key;
        public int hash;
        public TrackAsset trackAsset;
        public ControlPlayableAsset playableAsset;
    }

    [Serializable]
    public struct TrackBindInfo {
        public string key;
        public TrackAsset trackAsset;
    }

    [Serializable]
    public struct SubPlayerBindInfo {
        public string key;
        public int hash;
        public ControlPlayableAsset playableAsset;
        public TimelinePlayer subPlayer;
    }
}