using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Nav;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Geometry;

/// <summary>
/// Publishes odometry data from Unity to ROS2 /odom topic.
/// Uses ROSGeometry for correct Unity (Y-up left-hand) to ROS (Z-up right-hand) conversion.
/// Attach to: base_footprint GameObject
/// </summary>
public class OdometryPublisher : MonoBehaviour
{
    ROSConnection ros;

    [Header("ROS Settings")]
    public string topicName = "/odom";
    public float publishRate = 20f;

    [Header("Frame IDs")]
    public string odomFrameId = "odom";
    public string childFrameId = "base_footprint";

    private float timeElapsed;
    private Vector3 lastPosition;
    private Quaternion lastRotation;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<OdometryMsg>(topicName);
        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    void Update()
    {
        timeElapsed += Time.deltaTime;
        if (timeElapsed >= 1f / publishRate)
        {
            PublishOdometry();
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

    void PublishOdometry()
    {
        // Use ROSGeometry extensions for correct coordinate conversion:
        // Unity Vector3.To<RUF>() converts Unity (X-right, Y-up, Z-forward)
        // to ROS (X-forward, Y-left, Z-up)
        var rosPosition = transform.position.To<FLU>();
        var rosRotation = transform.rotation.To<FLU>();

        // Compute velocity from position delta
        float dt = 1f / publishRate;
        Vector3 deltaPos = transform.position - lastPosition;

        // Convert velocity to ROS frame
        var rosVelocity = (deltaPos / dt).To<FLU>();

        float deltaYaw = Quaternion.Angle(lastRotation, transform.rotation) * Mathf.Deg2Rad;
        float angularZ = deltaYaw / dt;

        var msg = new OdometryMsg
        {
            header = new HeaderMsg
            {
                stamp    = GetROSTime(),
                frame_id = odomFrameId
            },
            child_frame_id = childFrameId
        };

        // Position and orientation — properly converted by ROSGeometry
        msg.pose.pose.position.x    = rosPosition.x;
        msg.pose.pose.position.y    = rosPosition.y;
        msg.pose.pose.position.z    = 0.0;  // 2D navigation — force Z=0
        msg.pose.pose.orientation.x = rosRotation.x;
        msg.pose.pose.orientation.y = rosRotation.y;
        msg.pose.pose.orientation.z = rosRotation.z;
        msg.pose.pose.orientation.w = rosRotation.w;

        // Twist (velocity) in robot's local frame
        msg.twist.twist.linear.x  = rosVelocity.x;
        msg.twist.twist.linear.y  = rosVelocity.y;
        msg.twist.twist.linear.z  = 0.0;
        msg.twist.twist.angular.z = angularZ;

        ros.Publish(topicName, msg);

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }
}