using System.Collections.Generic;
using PragmaFramework.Timeline.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PragmaTimeline.Examples {
    public class ExampleController : MonoBehaviour {
        public List<GameObject> timelinePrefabs;

        public GameObject cube1;
        public GameObject cube2;
        public GameObject cube3;
    
        public GameObject cameraGO;

        public Button btnTimeline1;
        public Button btnTimeline2;
        public Button btnTimeline3;

        public Button btnStop;

        // Start is called before the first frame update
        void Start() {
            var map1 = new Dictionary<string, object> {
                {"Cube", cube1.GetComponent<Animator>()},
            };
            btnTimeline1.onClick.AddListener(() => {
                PlayTimeline(0, map1);
            });
        
            var map2 = new Dictionary<string, object> {
                {"Cube", cube2.GetComponent<Animator>()},
                {"Camera", cameraGO.GetComponent<Animator>()},
            };
            btnTimeline2.onClick.AddListener(() => {
                PlayTimeline(1, map2);
            });
        
            var map3 = new Dictionary<string, object> {
                {"Cube", cube3.GetComponent<Animator>()},
                {"Timeline1", map1},
                {"Timeline2", map2},
            };
            btnTimeline3.onClick.AddListener(() => {
                PlayTimeline(2, map3);
            });
        
            btnStop.onClick.AddListener(() => {
                var players = FindObjectsOfType<TimelinePlayer>();
                foreach (var player in players) {
                    player.ClearTimeline();
                }
            });
            // var instance = Instantiate(timelinePrefabs[0], Vector3.zero, Quaternion.identity);
            // var player = instance.GetComponent<TimelinePlayer>();
            // var map = new Dictionary<string, object> {
            //     {"Cube", cube1.GetComponent<Animator>()},
            //     {"Camera", cameraGO.GetComponent<Animator>()},
            // };
            // player.Init(map);
            // player.Stopped += playableDirector => {
            //     Debug.Log("Timeline stopped");
            // };
            //         
            // player.PlayTimeline(true);
        }
    
        private void PlayTimeline(int index, Dictionary<string, object> map) {
            var instance = Instantiate(timelinePrefabs[index], Vector3.zero, Quaternion.identity);
            var player = instance.GetComponent<TimelinePlayer>();
        
            player.Init(map);
            player.Stopped += playableDirector => {
                Debug.Log("Timeline stopped");
            };
                
            player.PlayTimeline(true);
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
