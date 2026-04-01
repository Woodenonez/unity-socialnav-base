using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArmRest : MonoBehaviour
{
    [System.Serializable]
    public class JointCmd
    {
        public string linkName;
        public float target;   // deg for revolute, m for prismatic
        public bool prismatic;
    }

    public float moveDuration = 5.0f;

    public JointCmd[] joints =
    {
        new JointCmd { linkName = "torso_lift_link", target = 0.00f, prismatic = true },

        new JointCmd { linkName = "arm_1_link", target =  23f,  prismatic = false },
        new JointCmd { linkName = "arm_2_link", target = -67f,  prismatic = false },
        new JointCmd { linkName = "arm_3_link", target = -109f, prismatic = false },
        new JointCmd { linkName = "arm_4_link", target = 132f,  prismatic = false },
        new JointCmd { linkName = "arm_5_link", target = -74f,  prismatic = false },
        new JointCmd { linkName = "arm_6_link", target = -26f,  prismatic = false },
        new JointCmd { linkName = "arm_7_link", target = 100f,  prismatic = false },

        new JointCmd { linkName = "gripper_left_finger_link",  target = 0.0f, prismatic = true },
        new JointCmd { linkName = "gripper_right_finger_link", target = 0.0f, prismatic = true },
    };

    class JointState
    {
        public ArticulationBody ab;
        public float startTarget;
        public float endTarget;
    }

    IEnumerator Start()
    {
        yield return null;
        yield return new WaitForFixedUpdate();

        List<JointState> active = new List<JointState>();

        foreach (var j in joints)
        {
            var t = FindDeepChild(transform, j.linkName);
            if (t == null)
            {
                Debug.LogWarning($"Not found: {j.linkName}");
                continue;
            }

            var ab = t.GetComponent<ArticulationBody>();
            if (ab == null)
            {
                Debug.LogWarning($"No ArticulationBody on: {j.linkName}");
                continue;
            }

            var drive = ab.xDrive;

            if (j.prismatic)
            {
                drive.stiffness = 30000f;
                drive.damping = 5000f;
                drive.forceLimit = 5000f;
                ab.maxJointVelocity = 0.08f;   // m/s
            }
            else
            {
                drive.stiffness = 12000f;
                drive.damping = 2500f;
                drive.forceLimit = 400f;
                ab.maxJointVelocity = 0.8f;    // rad/s
            }

            drive.targetVelocity = 0f;

            float clampedTarget = Mathf.Clamp(j.target, drive.lowerLimit, drive.upperLimit);
            float currentTarget = drive.target;

            ab.xDrive = drive;

            active.Add(new JointState
            {
                ab = ab,
                startTarget = currentTarget,
                endTarget = clampedTarget
            });
        }

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float u = Mathf.Clamp01(elapsed / moveDuration);

            // smoother than linear
            float s = u * u * (3f - 2f * u);

            foreach (var js in active)
            {
                var drive = js.ab.xDrive;
                drive.target = Mathf.Lerp(js.startTarget, js.endTarget, s);
                js.ab.xDrive = drive;
            }

            yield return new WaitForFixedUpdate();
        }

        foreach (var js in active)
        {
            var drive = js.ab.xDrive;
            drive.target = js.endTarget;
            js.ab.xDrive = drive;
        }
    }

    Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}