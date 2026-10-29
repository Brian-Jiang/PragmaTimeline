using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace PragmaFramework.Timeline.Runtime {
    /// <summary>
    /// Bind info for one entry of the director's exposed reference table.
    /// </summary>
    [Serializable]
    public struct ControlBindInfo {
        public string key;
        public PropertyName exposedName;
        /// <summary>
        /// The track that owns the reference, or the track of the clip or marker that owns it.
        /// </summary>
        public TrackAsset trackAsset;
        /// <summary>
        /// The track or clip asset that owns the reference; null for a marker.
        /// </summary>
        public PlayableAsset playableAsset;
    }

    [Serializable]
    public struct TrackBindInfo {
        public string key;
        public TrackAsset trackAsset;
    }

    [Serializable]
    public struct SubPlayerBindInfo {
        public string key;
        public PropertyName exposedName;
        public ControlPlayableAsset playableAsset;
        public TimelinePlayer subPlayer;
    }
}