using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Geometry;

/// <summary>
/// Publishes all pedestrian positions to ROS2 as a MarkerArray
/// on /pedestrians/markers (for RViz2 visualization)
/// and as a custom PoseArray on /pedestrians/poses (for the social cost layer).
///
/// Attach to: any persistent GameObject in the scene (e.g. a ROSManager empty object)
/// </summary>
public class PedestrianPublisher : MonoBehaviour
{
    ROSConnection ros;

    [Header("ROS Settings")]
    public string markerTopic = "/pedestrians/markers";
    public string posesTopic  = "/pedestrians/poses";
    public string frameId     = "map";
    public float  publishRate = 10f;

    [Header("Pedestrians")]
    public List<PedestrianAgent> pedestrians = new List<PedestrianAgent>();

    private float timeElapsed;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<RosMessageTypes.Visualization.MarkerArrayMsg>(markerTopic);
        ros.RegisterPublisher<RosMessageTypes.Geometry.PoseArrayMsg>(posesTopic);

        // Auto-find all pedestrians in scene if not manually assigned
        if (pedestrians.Count == 0)
        {
            pedestrians.AddRange(
                FindObjectsByType<PedestrianAgent>(FindObjectsSortMode.None)
            );
            Debug.Log($"[PedestrianPublisher] Found {pedestrians.Count} pedestrians.");
        }
    }

    void Update()
    {
        timeElapsed += Time.deltaTime;
        if (timeElapsed >= 1f / publishRate)
        {
            PublishPedestrians();
            timeElapsed = 0f;
        }
    }

    TimeMsg GetROSTime()
    {
        double unixNow = (DateTime.UtcNow -
                          new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc))
                         .TotalSeconds;
        return new TimeMsg
        {
            sec     = (int)unixNow,
            nanosec = (uint)((unixNow - Math.Floor(unixNow)) * 1e9)
        };
    }

    void PublishPedestrians()
    {
        if (pedestrians.Count == 0) return;

        var stamp  = GetROSTime();
        var header = new HeaderMsg { stamp = stamp, frame_id = frameId };

        // --- MarkerArray for RViz2 visualization ---
        var markers = new RosMessageTypes.Visualization.MarkerMsg[pedestrians.Count];
        var poses   = new PoseMsg[pedestrians.Count];

        for (int i = 0; i < pedestrians.Count; i++)
        {
            var ped    = pedestrians[i];
            var rosPos = ped.transform.position.To<FLU>();
            var rosRot = ped.transform.rotation.To<FLU>();

            // Cylinder marker representing a person
            markers[i] = new RosMessageTypes.Visualization.MarkerMsg
            {
                header    = header,
                ns        = "pedestrians",
                id        = i,
                type      = 3,  // CYLINDER
                action    = 0,  // ADD
                pose = new PoseMsg
                {
                    position = new PointMsg
                    {
                        x = rosPos.x,
                        y = rosPos.y,
                        z = 0.9   // half human height
                    },
                    orientation = new QuaternionMsg { x = 0, y = 0, z = 0, w = 1 }
                },
                scale = new RosMessageTypes.Geometry.Vector3Msg
                {
                    x = 0.5,
                    y = 0.5,
                    z = 1.8   // full human height
                },
                color = new RosMessageTypes.Std.ColorRGBAMsg
                {
                    r = 1.0f,
                    g = 0.4f,
                    b = 0.0f,
                    a = 0.9f
                },
                lifetime = new DurationMsg { sec = 1, nanosec = 0 }
            };

            // Pose for the social cost layer
            poses[i] = new PoseMsg
            {
                position = new PointMsg
                {
                    x = rosPos.x,
                    y = rosPos.y,
                    z = 0.0
                },
                orientation = new QuaternionMsg
                {
                    x = rosRot.x,
                    y = rosRot.y,
                    z = rosRot.z,
                    w = rosRot.w
                }
            };
        }

        ros.Publish(markerTopic,
            new RosMessageTypes.Visualization.MarkerArrayMsg { markers = markers });

        ros.Publish(posesTopic,
            new RosMessageTypes.Geometry.PoseArrayMsg
            {
                header = header,
                poses  = poses
            });
    }
}