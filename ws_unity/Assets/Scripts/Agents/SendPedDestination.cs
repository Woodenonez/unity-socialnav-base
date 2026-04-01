using UnityEngine;

[RequireComponent(typeof(PedestrianAgent))]
public class SendPedDestination : MonoBehaviour
{
    [Header("2D target in Unity ground plane")]
    public float targetX = 0f;
    public float targetZ = 0f;

    [Header("Options")]
    public bool sendOnStart = true;
    public bool continuousGroundSnap = true;
    public float targetY = 0f;
    public float changeTolerance = 0.001f;

    private PedestrianAgent pedestrianAgent;
    private Vector2 lastSentTarget;
    private bool hasSentAtLeastOnce = false;

    void Start()
    {
        pedestrianAgent = GetComponent<PedestrianAgent>();

        if (sendOnStart)
        {
            SendCurrentTarget();
        }
    }

    void Update()
    {
        Vector2 currentTarget = new Vector2(targetX, targetZ);

        if (!hasSentAtLeastOnce || Vector2.Distance(currentTarget, lastSentTarget) > changeTolerance)
        {
            SendCurrentTarget();
        }
    }

    private void SendCurrentTarget()
    {
        Vector3 worldTarget = new Vector3(targetX, targetY, targetZ);

        if (continuousGroundSnap)
        {
            worldTarget.y = transform.position.y;
        }

        bool ok = pedestrianAgent.SetTargetPosition(worldTarget);

        if (ok)
        {
            lastSentTarget = new Vector2(targetX, targetZ);
            hasSentAtLeastOnce = true;
            Debug.Log($"{name}: new pedestrian target set to ({targetX:F2}, {targetZ:F2})");
        }
        else
        {
            Debug.LogWarning($"{name}: failed to set pedestrian target near ({targetX:F2}, {targetZ:F2})");
        }
    }
}