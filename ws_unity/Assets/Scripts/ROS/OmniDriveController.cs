using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

namespace RosSharp.Control
{
    // public enum ControlMode { Keyboard, ROS }

    public class MecanumDriveController : MonoBehaviour
    {
        ROSConnection ros;

        [Header("Wheel objects")]
        public GameObject frontLeftWheel;
        public GameObject frontRightWheel;
        public GameObject rearLeftWheel;
        public GameObject rearRightWheel;

        [Header("Control")]
        public ControlMode mode = ControlMode.ROS;
        public string NameSpace = "";
        public string cmdVelTopic = "/cmd_vel";
        public float ROSTimeout = 0.5f;

        [Header("Limits")]
        public float maxLinearSpeedX = 1.0f;   // forward/backward m/s
        public float maxLinearSpeedY = 1.0f;   // left/right m/s
        public float maxRotationalSpeed = 1.0f; // rad/s

        [Header("Robot geometry")]
        public float wheelRadius = 0.05f;   // meters
        public float wheelBase = 0.35f;     // front-to-rear wheel center distance
        public float trackWidth = 0.30f;    // left-to-right wheel center distance

        [Header("Drive parameters")]
        public float forceLimit = 25f;
        public float damping = 10f;

        [Header("Wheel direction sign")]
        [Tooltip("Use +1 or -1 if a wheel spins the wrong way because of joint axis orientation.")]
        public int frontLeftSign  = 1;
        public int frontRightSign = 1;
        public int rearLeftSign   = 1;
        public int rearRightSign  = 1;

        private ArticulationBody flJoint;
        private ArticulationBody frJoint;
        private ArticulationBody rlJoint;
        private ArticulationBody rrJoint;

        private float rosVx = 0f;
        private float rosVy = 0f;
        private float rosWz = 0f;
        private float lastCmdReceived = 0f;

        void Start()
        {
            ros = ROSConnection.GetOrCreateInstance();
            ros.Subscribe<TwistMsg>(NameSpace + cmdVelTopic, ReceiveROSCmd);

            flJoint = frontLeftWheel.GetComponent<ArticulationBody>();
            frJoint = frontRightWheel.GetComponent<ArticulationBody>();
            rlJoint = rearLeftWheel.GetComponent<ArticulationBody>();
            rrJoint = rearRightWheel.GetComponent<ArticulationBody>();

            SetParameters(flJoint);
            SetParameters(frJoint);
            SetParameters(rlJoint);
            SetParameters(rrJoint);
        }

        void ReceiveROSCmd(TwistMsg cmdVel)
        {
            rosVx = (float)cmdVel.linear.x;
            rosVy = (float)cmdVel.linear.y;
            rosWz = (float)cmdVel.angular.z;
            lastCmdReceived = Time.time;
        }

        void FixedUpdate()
        {
            if (mode == ControlMode.Keyboard)
            {
                KeyboardUpdate();
            }
            else
            {
                ROSUpdate();
            }
        }

        private void SetParameters(ArticulationBody joint)
        {
            ArticulationDrive drive = joint.xDrive;
            drive.forceLimit = forceLimit;
            drive.damping = damping;
            joint.xDrive = drive;
        }

        private void SetWheelSpeed(ArticulationBody joint, float wheelAngularSpeedRad, int sign = 1)
        {
            ArticulationDrive drive = joint.xDrive;
            drive.targetVelocity = wheelAngularSpeedRad * Mathf.Rad2Deg * sign;
            joint.xDrive = drive;
        }

        private void KeyboardUpdate()
        {
            // Forward/backward: W/S or Up/Down
            float vx = Input.GetAxis("Vertical") * maxLinearSpeedX;

            // Strafe: A/D or Left/Right
            float vy = Input.GetAxis("Horizontal") * maxLinearSpeedY;

            // Rotate: Q/E
            float wz = 0f;
            if (Input.GetKey(KeyCode.Q))
                wz = maxRotationalSpeed;
            else if (Input.GetKey(KeyCode.E))
                wz = -maxRotationalSpeed;

            Drive(vx, vy, wz);
        }

        private void ROSUpdate()
        {
            if (Time.time - lastCmdReceived > ROSTimeout)
            {
                rosVx = 0f;
                rosVy = 0f;
                rosWz = 0f;
            }

            Drive(rosVx, rosVy, rosWz);
        }

        private void Drive(float vx, float vy, float wz)
        {
            vx = Mathf.Clamp(vx, -maxLinearSpeedX, maxLinearSpeedX);
            vy = Mathf.Clamp(vy, -maxLinearSpeedY, maxLinearSpeedY);
            wz = Mathf.Clamp(wz, -maxRotationalSpeed, maxRotationalSpeed);

            // Distance from robot center to wheel contact projection
            float k = (wheelBase / 2f) + (trackWidth / 2f);

            // Standard mecanum inverse kinematics
            // Assumes:
            //  - x forward
            //  - y left/right strafe
            //  - z yaw
            //  - wheel order: FL, FR, RL, RR
            float wFL = (vx - vy - k * wz) / wheelRadius;
            float wFR = (vx + vy + k * wz) / wheelRadius;
            float wRL = (vx + vy - k * wz) / wheelRadius;
            float wRR = (vx - vy + k * wz) / wheelRadius;

            SetWheelSpeed(flJoint, wFL, frontLeftSign);
            SetWheelSpeed(frJoint, wFR, frontRightSign);
            SetWheelSpeed(rlJoint, wRL, rearLeftSign);
            SetWheelSpeed(rrJoint, wRR, rearRightSign);
        }
    }
}