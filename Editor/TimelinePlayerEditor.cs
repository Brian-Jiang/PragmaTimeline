using PragmaFramework.Timeline.Runtime;
using UnityEditor;
using UnityEngine;

namespace PragmaFramework.Timeline.Editor {
    [CustomEditor(typeof(TimelinePlayer))]
    public class TimelinePlayerEditor: UnityEditor.Editor {

        private TimelinePlayer TimelinePlayer => serializedObject.targetObject as TimelinePlayer;
        
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            if (GUILayout.Button("Update")) {
                Undo.RecordObject(TimelinePlayer, "Update TimelinePlayer");
                TimelinePlayer.SaveTimeline();
            }
        }
    }
}