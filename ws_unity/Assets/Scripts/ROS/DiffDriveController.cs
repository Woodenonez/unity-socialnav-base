using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;
using Unity.Robotics.UrdfImporter.Control;

namespace RosSharp.Control
{
    public enum ControlMode {Keyboard, ROS};

    public class DiffDriveController : MonoBehaviour
    {
        ROSConnection ros;

        public GameObject[] leftWheels;
        public GameObject[] rightWheels;
        public ControlMode mode = ControlMode.ROS;
        public string NameSpace = "";
        public string cmdVelTopic = "/cmd_vel";

        private ArticulationBody[] leftJoints;
        private ArticulationBody[] rightJoints;

        public float ROSTimeout = 0.5f;

        public float maxLinearSpeed = 2; //  m/s
        public float maxRotationalSpeed = 3; // rad/s
        public float wheelRadius = 0.033f; //meters
        public float trackWidth = 0.288f; // meters Distance between tyres
        public float forceLimit = 10;
        public float damping = 10;


        private RotationDirection direction;
        private float rosLinear = 0f;
        private float rosAngular = 0f;
        private float lastCmdReceived = 0f;

        void Start()
        {
            ros = ROSConnection.GetOrCreateInstance();
            ros.Subscribe<TwistMsg>(NameSpace + cmdVelTopic, ReceiveROSCmd);

            leftJoints = new ArticulationBody[leftWheels.Length];
            rightJoints = new ArticulationBody[rightWheels.Length];
            if (leftWheels.Length != rightWheels.Length)
            {
                Debug.LogError("Number of left and right wheels must be the same.");
            }

            for (int i = 0; i < leftWheels.Length; i++)
            {
                leftJoints[i] = leftWheels[i].GetComponent<ArticulationBody>();
                SetParameters(leftJoints[i]);
                rightJoints[i] = rightWheels[i].GetComponent<ArticulationBody>();
                SetParameters(rightJoints[i]);
            }
        }

        void ReceiveROSCmd(TwistMsg cmdVel)
        {
            rosLinear = (float)cmdVel.linear.x;
            rosAngular = (float)cmdVel.angular.z;
            lastCmdReceived = Time.time;
        }

        void FixedUpdate()
        {
            if (mode == ControlMode.Keyboard)
            {
                KeyBoardUpdate();
            }
            else if (mode == ControlMode.ROS)
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

        private void SetSpeed(ArticulationBody joint, float wheelSpeed = float.NaN)
        {
            ArticulationDrive drive = joint.xDrive;

            float currentVel = joint.jointVelocity[0]; // as an encoder
            float targetVel;

            if (float.IsNaN(wheelSpeed))
            {
                targetVel = ((2 * maxLinearSpeed) / wheelRadius) * Mathf.Rad2Deg * (int)direction;
            }
            else
            {
                targetVel = wheelSpeed;
            }
            drive.targetVelocity = targetVel;
            joint.xDrive = drive;
            // Debug.Log(
            //     $"{joint.name} | cmdWheelSpeed: {wheelSpeed:F2} | targetVel: {targetVel:F2} | actualVel: {joint.jointVelocity[0]:F2}"
            // );
        }

        private void KeyBoardUpdate()
        {
            float moveDirection = Input.GetAxis("Vertical");
            float inputSpeed;
            float inputRotationSpeed;
            if (moveDirection > 0)
            {
                inputSpeed = maxLinearSpeed;
            }
            else if (moveDirection < 0)
            {
                inputSpeed = maxLinearSpeed * -1;
            }
            else
            {
                inputSpeed = 0;
            }

            float turnDirction = Input.GetAxis("Horizontal");
            if (turnDirction > 0)
            {
                inputRotationSpeed = maxRotationalSpeed;
            }
            else if (turnDirction < 0)
            {
                inputRotationSpeed = maxRotationalSpeed * -1;
            }
            else
            {
                inputRotationSpeed = 0;
            }
            RobotInput(inputSpeed, inputRotationSpeed);
        }


        private void ROSUpdate()
        {
            if (Time.time - lastCmdReceived > ROSTimeout)
            {
                rosLinear = 0f;
                rosAngular = 0f;
            }
            RobotInput(rosLinear, -rosAngular);
        }

        private void RobotInput(float speed, float rotSpeed) // m/s and rad/s
        {
            if (Mathf.Abs(speed) < 0.01f && Mathf.Abs(rotSpeed) < 0.01f)
            {
                for (int i = 0; i < leftJoints.Length; i++)
                {
                    SetSpeed(leftJoints[i], 0f);
                    SetSpeed(rightJoints[i], 0f);
                }
                // Debug.Log("Robot stopped.");
                return;
            }
            // Debug.Log($"Received cmd_vel | linear: {speed:F2} m/s, angular: {rotSpeed:F2} rad/s");
            if (speed > maxLinearSpeed)
            {
                speed = maxLinearSpeed;
            }
            if (rotSpeed > maxRotationalSpeed)
            {
                rotSpeed = maxRotationalSpeed;
            }
            float wheel1Rotation = (speed / wheelRadius);
            float wheel2Rotation = wheel1Rotation;
            float wheelSpeedDiff = ((rotSpeed * trackWidth) / wheelRadius);
            if (rotSpeed != 0)
            {
                wheel1Rotation = (wheel1Rotation + (wheelSpeedDiff / 1)) * Mathf.Rad2Deg;
                wheel2Rotation = (wheel2Rotation - (wheelSpeedDiff / 1)) * Mathf.Rad2Deg;
            }
            else
            {
                wheel1Rotation *= Mathf.Rad2Deg;
                wheel2Rotation *= Mathf.Rad2Deg;
            }
            for (int i = 0; i < leftJoints.Length; i++)
            {
                SetSpeed(leftJoints[i], wheel1Rotation);
                SetSpeed(rightJoints[i], wheel2Rotation);
            }
        }
    }
}
