# Pragma Timeline
#### version 0.7.0

## Summary
Pragma Timeline is a tool that can let you easily play timeline in runtime and bind to your scene objects. 
See it in [asset store](https://u3d.as/33T7).

## Quick Start
1. Create a scene by click `File -> New Scene -> PragmaTimelineEdit`. This scene is only used for editing timeline.
2. Copy paste objects that you want to use in timeline to your new scene.
3. Create a timeline asset by right click in Assets panel and click `Create -> Timeline`. 
Then drag it to playable director of the Timeline game object in the scene.
4. Drag Timeline game object to the Assets panel to make it a prefab.
5. You can now start editing your timeline. 
See [Unity Timeline](https://docs.unity3d.com/Manual/TimelineSection.html) for more information.
6. When you finish, click the Update button on the inspector of the Timeline game object's TimelinePlayer component. 
This will create several records of objects that you need to bind in runtime. 
Change name of the records to something meaningful. Note that these are the names you will use in script.
7. At runtime, play your timeline using following code:
```csharp
    var instance = Instantiate(prefab);
    var player = instance.GetComponent<TimelinePlayer>();

    var map = new Dictionary<string, object> {
        {"name1", object1},
        {"name2", object2},
    };
    player.Init(map);
    
    player.Stopped += playableDirector => {
        Debug.Log("Timeline stopped");
    };
    
    player.PlayTimeline(true);
```

## Supports
If you have any questions, please comment at [Asset Store](https://u3d.as/33T7)  
Or email me directly at: [bjjx1999@live.com](mailto:bjjx1999@live.com)  
Thank you for your support!