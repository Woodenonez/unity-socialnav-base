using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.Core;
using RosMessageTypes.Rosgraph;
using RosMessageTypes.BuiltinInterfaces;

/// <summary>
/// Publishes Unity sim time to /clock so ROS2 nodes using
/// use_sim_time:=true stay in sync with Unity.
/// Attach to: any persistent GameObject (e.g. ROSManager)
/// </summary>
public class ROSClockPublisher : MonoBehaviour
{
    ROSConnection ros;
    public float publishRate = 100f;
    private float timeElapsed;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<ClockMsg>("/clock");
    }

    void Update()
    {
        timeElapsed += Time.deltaTime;
        if (timeElapsed >= 1f / publishRate)
        {
            double t = Clock.time;
            ros.Publish("/clock", new ClockMsg
            {
                clock = new TimeMsg
                {
                    sec     = (int)t,
                    nanosec = (uint)((t - Math.Floor(t)) * 1e9)
                }
            });
            timeElapsed = 0f;
        }
    }
}