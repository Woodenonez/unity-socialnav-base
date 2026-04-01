using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;

/// <summary>
/// Publishes LaserScan data from Unity raycasts to ROS2 /scan topic.
/// Uses ROSGeometry-aware raycasting in the horizontal plane.
///
/// Key coordinate notes:
/// - Unity forward = +Z, ROS forward = +X
/// - Rays are cast in Unity's XZ plane (horizontal ground plane)
/// - Angles follow ROS convention: counter-clockwise from robot forward (+X in ROS)
///
/// Attach to: base_scan GameObject (child of base_link)
/// </summary>
public class LaserScanPublisher : MonoBehaviour
{
    ROSConnection ros;

    [Header("ROS Settings")]
    public string topicName  = "/scan";
    public string frameId    = "base_scan";
    public float publishRate = 10f;

    [Header("LiDAR Parameters — TurtleBot3 Burger LDS-01")]
    public int   numRays  = 360;
    public float maxRange = 3.5f;
    public float minRange = 0.12f;

    private float timeElapsed;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<LaserScanMsg>(topicName);
    }

    void Update()
    {
        timeElapsed += Time.deltaTime;
        if (timeElapsed >= 1f / publishRate)
        {
            PublishScan();
            timeElapsed = 0f;
        }
    }

    TimeMsg GetROSTime()
    {
        double t = Unity.Robotics.Core.Clock.time;
        return new TimeMsg
        {
            sec     = (int)t,
            nanosec = (uint)((t - Math.Floor(t)) * 1e9)
        };
    }

    void PublishScan()
    {
        float[] ranges = new float[numRays];

        for (int i = 0; i < numRays; i++)
        {
            // ROS angle convention: counter-clockwise from forward
            // angle_min = 0, angle_max = 2*pi, going CCW
            // In Unity: CCW rotation is negative Y-axis rotation
            float rosAngleDeg = i * 360f / numRays;

            // Convert ROS angle to Unity world direction:
            // ROS 0 degrees = robot forward = Unity +Z
            // ROS 90 degrees (CCW) = robot left = Unity -X
            // Negate angle for Unity's left-hand coordinate system
            Vector3 dir = Quaternion.Euler(0f, -rosAngleDeg, 0f) * transform.forward;

            // Raycast in horizontal plane only (ignore Y component for 2D LiDAR)
            dir.y = 0f;
            dir.Normalize();

            RaycastHit hit;
            if (Physics.Raycast(transform.position, dir, out hit, maxRange))
            {
                ranges[i] = hit.distance < minRange
                    ? float.PositiveInfinity
                    : hit.distance;
            }
            else
            {
                ranges[i] = float.PositiveInfinity;
            }
        }

        var msg = new LaserScanMsg
        {
            header = new HeaderMsg
            {
                stamp    = GetROSTime(),
                frame_id = frameId
            },
            angle_min       =  0f,
            angle_max       =  2f * Mathf.PI,
            angle_increment =  2f * Mathf.PI / numRays,
            time_increment  =  0f,
            scan_time       =  1f / publishRate,
            range_min       =  minRange,
            range_max       =  maxRange,
            ranges          =  ranges,
            intensities     =  new float[0]
        };

        ros.Publish(topicName, msg);
    }
}