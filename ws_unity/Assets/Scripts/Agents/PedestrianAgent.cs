using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Pedestrian agent with:
/// - Wander / external target navigation
/// - Walk / jog animation via NavMeshAgent velocity
/// - Waiting at destinations
/// - Personal-space slowdown near other pedestrians
/// - Configurable obstacle reaction modes:
///     * DoNothing
///     * Stop
///     * Avoid
/// - Optional tag list for objects this agent should react to, e.g. "robot", "agent"
///
/// Attach to a GameObject that has:
/// - NavMeshAgent
/// - Animator
/// - Collider
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class PedestrianAgent : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Enums                                                             //
    // ------------------------------------------------------------------ //

    public enum ObstacleReactionMode
    {
        DoNothing,
        Stop,
        Avoid
    }

    // ------------------------------------------------------------------ //
    //  Inspector settings                                                //
    // ------------------------------------------------------------------ //

    [Header("Identity")]
    public int pedestrianId = 0;
    public string pedestrianName = "pedestrian_0";

    [Header("Wandering")]
    public float wanderRadius = 12f;
    public float minWaitTime = 1.5f;
    public float maxWaitTime = 4.0f;
    public float goalTolerance = 0.6f;

    [Header("Speed Profile")]
    [Tooltip("Base walk speed in m/s. Randomized per agent on Start.")]
    public float minWalkSpeed = 0.5f;
    public float maxWalkSpeed = 1.5f;
    [Tooltip("Chance (0-1) this agent will occasionally jog.")]
    public float jogChance = 0.2f;
    public float jogSpeed = 2.2f;
    public float runSpeed = 3.5f;

    [Header("Crowd Behaviour")]
    [Tooltip("Distance at which pedestrian slows down near other pedestrians.")]
    public float personalSpaceRadius = 1.2f;

    [Header("Obstacle Reaction")]
    [Tooltip("How this pedestrian reacts when a tracked obstacle is detected.")]
    public ObstacleReactionMode obstacleReactionMode = ObstacleReactionMode.Avoid;

    [Tooltip("Tags of obstacles this agent should react to. Examples: robot, agent")]
    public List<string> avoidTags = new List<string> { "robot" };

    [Tooltip("Detection radius for tracked obstacles.")]
    public float obstacleDetectRadius = 2.5f;

    [Tooltip("Distance required before a stop/avoid interaction is considered finished. Should be larger than obstacleDetectRadius.")]
    public float obstacleResumeDistance = 3.2f;

    [Tooltip("Speed multiplier when in Stop mode. 0 = full stop, 0.2 = slow creep.")]
    public float stopSpeedMultiplier = 0.0f;

    [Tooltip("Only react when obstacle is roughly in front of the pedestrian. -1 = behind, 1 = directly ahead.")]
    [Range(-1f, 1f)]
    public float obstacleFrontDotThreshold = 0.1f;

    [Header("Avoid Settings")]
    [Tooltip("Sideways offset when computing the temporary detour point.")]
    public float avoidSideStep = 2.0f;

    [Tooltip("Forward offset beyond the obstacle when computing the detour point.")]
    public float avoidForward = 1.5f;

    [Tooltip("Cooldown after an avoid action before another tracked obstacle can trigger avoidance again.")]
    public float avoidCooldown = 1.2f;

    [Tooltip("Search radius used by NavMesh.SamplePosition for the computed avoid point.")]
    public float avoidSampleRadius = 1.5f;

    [Header("Navigation Source")]
    [Tooltip("Whether this pedestrian uses external target positions or random wandering.")]
    public bool hasExternalTarget = false;

    // ------------------------------------------------------------------ //
    //  Private state                                                     //
    // ------------------------------------------------------------------ //

    private NavMeshAgent agent;
    private Animator animator;
    private Vector3 origin;

    private float assignedWalkSpeed;
    private float assignedJogSpeed;

    private float waitTimer = 0f;
    private bool isWaiting = false;

    private bool isStoppingForObstacle = false;
    private bool isAvoidingObstacle = false;

    private float avoidCooldownTimer = 0f;

    // Main goal = real/original target
    private Vector3 mainGoal;
    private bool hasMainGoal = false;

    // Temporary detour target
    private Vector3 avoidGoal;

    // The obstacle currently being reacted to
    private Transform activeObstacleTransform;

    // Animator parameter hash
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    // Nearby pedestrian cache
    private readonly List<PedestrianAgent> nearbyPedestrians = new List<PedestrianAgent>();

    // Repeated sensing timer
    private float socialCheckTimer = 0f;
    private const float SocialCheckInterval = 0.25f;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle                                                   //
    // ------------------------------------------------------------------ //

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        origin = transform.position;

        // Randomise speed per agent for variety
        assignedWalkSpeed = Random.Range(minWalkSpeed, maxWalkSpeed);
        assignedJogSpeed = (Random.value < jogChance) ? jogSpeed : assignedWalkSpeed;

        agent.speed = assignedWalkSpeed;
        agent.angularSpeed = 240f;
        agent.acceleration = 8f;
        agent.stoppingDistance = goalTolerance;
        agent.updateRotation = true;

        // Better local avoidance between nav agents
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = Random.Range(40, 70);

        // Slight random delay before first move so all agents don't start simultaneously
        Invoke(nameof(SetNewDestination), Random.Range(0f, 1.5f));
    }

    void Update()
    {
        if (avoidCooldownTimer > 0f)
            avoidCooldownTimer -= Time.deltaTime;

        socialCheckTimer += Time.deltaTime;
        if (socialCheckTimer >= SocialCheckInterval)
        {
            CheckObstacleBehaviour();
            CheckPedestrianPersonalSpace();
            socialCheckTimer = 0f;
        }

        // Stop mode
        if (isStoppingForObstacle)
        {
            agent.speed = assignedWalkSpeed * stopSpeedMultiplier;
            UpdateAnimator();
            return;
        }

        // Avoid mode
        if (isAvoidingObstacle)
        {
            UpdateAnimator();

            if (!agent.pathPending && agent.remainingDistance <= goalTolerance)
            {
                isAvoidingObstacle = false;
                avoidCooldownTimer = avoidCooldown;
                activeObstacleTransform = null;

                ResumeMainGoal();
            }

            return;
        }

        // Waiting at destination
        if (isWaiting)
        {
            agent.speed = 0f;
            waitTimer -= Time.deltaTime;
            UpdateAnimator();

            if (waitTimer <= 0f)
            {
                isWaiting = false;
                SetNewDestination();
            }

            return;
        }

        // Reached destination
        if (!agent.pathPending && agent.remainingDistance <= goalTolerance)
        {
            if (hasExternalTarget)
            {
                agent.speed = 0f;
            }
            else
            {
                isWaiting = true;
                waitTimer = Random.Range(minWaitTime, maxWaitTime);
                agent.speed = 0f;
            }
        }

        UpdateAnimator();
    }

    // ------------------------------------------------------------------ //
    //  Navigation                                                        //
    // ------------------------------------------------------------------ //

    void SetNewDestination()
    {
        if (hasExternalTarget)
            return;

        if (!isWaiting && !isStoppingForObstacle && !isAvoidingObstacle)
        {
            agent.speed = (Random.value < jogChance)
                ? assignedJogSpeed
                : assignedWalkSpeed;
        }

        Vector3 randomDir = Random.insideUnitSphere * wanderRadius;
        randomDir += origin;
        randomDir.y = origin.y;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDir, out hit, wanderRadius, NavMesh.AllAreas))
        {
            mainGoal = hit.position;
            hasMainGoal = true;
            agent.SetDestination(mainGoal);
        }
        else
        {
            mainGoal = origin;
            hasMainGoal = true;
            agent.SetDestination(mainGoal);
        }
    }

    public bool SetTargetPosition(Vector3 target)
    {
        hasExternalTarget = true;
        isWaiting = false;
        isStoppingForObstacle = false;
        isAvoidingObstacle = false;
        activeObstacleTransform = null;
        waitTimer = 0f;

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        NavMeshHit hit;
        if (NavMesh.SamplePosition(target, out hit, 2.0f, NavMesh.AllAreas))
        {
            agent.speed = assignedWalkSpeed;
            mainGoal = hit.position;
            hasMainGoal = true;
            return agent.SetDestination(mainGoal);
        }

        return false;
    }

    private void ResumeMainGoal()
    {
        if (hasMainGoal)
        {
            agent.speed = assignedWalkSpeed;
            agent.SetDestination(mainGoal);
        }
        else if (!hasExternalTarget)
        {
            SetNewDestination();
        }
    }

    // ------------------------------------------------------------------ //
    //  Obstacle behaviour                                                //
    // ------------------------------------------------------------------ //

    void CheckObstacleBehaviour()
    {
        Transform nearestObstacle = FindNearestTrackedObstacle();

        // If currently stopping, release only when obstacle is sufficiently far away
        if (isStoppingForObstacle)
        {
            if (activeObstacleTransform == null)
            {
                isStoppingForObstacle = false;
                ResumeMainGoal();
            }
            else
            {
                float d = FlatDistance(transform.position, activeObstacleTransform.position);
                if (d > obstacleResumeDistance)
                {
                    isStoppingForObstacle = false;
                    activeObstacleTransform = null;
                    ResumeMainGoal();
                }
            }
        }

        // If currently avoiding, let Update() finish it
        if (isAvoidingObstacle)
            return;

        // If doing nothing, clear reaction state and leave
        if (obstacleReactionMode == ObstacleReactionMode.DoNothing)
        {
            activeObstacleTransform = null;
            isStoppingForObstacle = false;
            return;
        }

        if (nearestObstacle == null)
            return;

        if (avoidCooldownTimer > 0f && obstacleReactionMode == ObstacleReactionMode.Avoid)
            return;

        Vector3 toObstacle = nearestObstacle.position - transform.position;
        toObstacle.y = 0f;

        float distance = toObstacle.magnitude;
        if (distance < 0.001f)
            return;

        Vector3 dirToObstacle = toObstacle.normalized;
        float frontDot = Vector3.Dot(transform.forward, dirToObstacle);
        bool obstacleIsInFront = frontDot >= obstacleFrontDotThreshold;

        if (distance >= obstacleDetectRadius || !obstacleIsInFront)
            return;

        activeObstacleTransform = nearestObstacle;

        switch (obstacleReactionMode)
        {
            case ObstacleReactionMode.Stop:
                StartStopReaction();
                break;

            case ObstacleReactionMode.Avoid:
                TrySetAvoidDestination(nearestObstacle);
                break;
        }
    }

    private void StartStopReaction()
    {
        if (isStoppingForObstacle)
            return;

        isStoppingForObstacle = true;
        isAvoidingObstacle = false;

        // Stop current path, but keep mainGoal stored
        agent.ResetPath();

        if (activeObstacleTransform != null)
        {
            Vector3 awayDir = (transform.position - activeObstacleTransform.position).normalized;
            awayDir.y = 0f;

            if (awayDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(awayDir),
                    0.3f
                );
            }
        }
    }

    private void TrySetAvoidDestination(Transform obstacleTransform)
    {
        if (obstacleTransform == null || !hasMainGoal)
            return;

        Vector3 toObstacle = obstacleTransform.position - transform.position;
        toObstacle.y = 0f;

        if (toObstacle.sqrMagnitude < 0.001f)
            return;

        Vector3 dirToObstacle = toObstacle.normalized;

        // Two candidate side paths around the obstacle
        Vector3 sideA = Vector3.Cross(Vector3.up, dirToObstacle).normalized;
        Vector3 sideB = -sideA;

        Vector3 candidateA = obstacleTransform.position
                           + sideA * avoidSideStep
                           + dirToObstacle * avoidForward;

        Vector3 candidateB = obstacleTransform.position
                           + sideB * avoidSideStep
                           + dirToObstacle * avoidForward;

        // Prefer the side that is closer to the original goal
        float scoreA = Vector3.Distance(candidateA, mainGoal);
        float scoreB = Vector3.Distance(candidateB, mainGoal);
        Vector3 chosen = (scoreA <= scoreB) ? candidateA : candidateB;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(chosen, out hit, avoidSampleRadius, NavMesh.AllAreas))
        {
            avoidGoal = hit.position;
            isAvoidingObstacle = true;
            isStoppingForObstacle = false;
            agent.speed = assignedWalkSpeed;
            agent.SetDestination(avoidGoal);
        }
    }

    private Transform FindNearestTrackedObstacle()
    {
        if (avoidTags == null || avoidTags.Count == 0)
            return null;

        Collider[] hits = Physics.OverlapSphere(transform.position, obstacleDetectRadius);
        Transform nearest = null;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null)
                continue;

            Transform candidate = col.transform;
            Transform root = candidate.root;

            // Ignore self
            if (root == transform.root)
                continue;

            // Check both the collider object and the root object
            bool tagMatched = ShouldAvoidTransform(candidate) || ShouldAvoidTransform(root);
            if (!tagMatched)
                continue;

            // Use root as the tracked obstacle, not the child collider
            Vector3 delta = root.position - transform.position;
            delta.y = 0f;
            float distSqr = delta.sqrMagnitude;

            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                nearest = root;
            }
        }

        return nearest;
    }

    private bool ShouldAvoidTransform(Transform t)
    {
        if (t == null || avoidTags == null)
            return false;

        for (int i = 0; i < avoidTags.Count; i++)
        {
            string avoidTag = avoidTags[i];
            if (!string.IsNullOrEmpty(avoidTag) && t.CompareTag(avoidTag))
                return true;
        }

        return false;
    }

    private bool ShouldAvoidTag(string tagToCheck)
    {
        if (string.IsNullOrEmpty(tagToCheck) || avoidTags == null)
            return false;

        for (int i = 0; i < avoidTags.Count; i++)
        {
            if (!string.IsNullOrEmpty(avoidTags[i]) && tagToCheck == avoidTags[i])
                return true;
        }

        return false;
    }

    private float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ------------------------------------------------------------------ //
    //  Crowd behaviour                                                   //
    // ------------------------------------------------------------------ //

    void CheckPedestrianPersonalSpace()
    {
        nearbyPedestrians.Clear();

        Collider[] hits = Physics.OverlapSphere(transform.position, personalSpaceRadius);

        foreach (Collider col in hits)
        {
            if (col.gameObject == gameObject)
                continue;

            PedestrianAgent other = col.GetComponent<PedestrianAgent>();
            if (other != null)
                nearbyPedestrians.Add(other);
        }

        if (nearbyPedestrians.Count > 0 && !isWaiting && !isStoppingForObstacle && !isAvoidingObstacle)
        {
            // Reduce speed proportional to crowding
            float crowdFactor = Mathf.Clamp01(1f - (nearbyPedestrians.Count * 0.25f));
            agent.speed = assignedWalkSpeed * crowdFactor;
        }
        else if (!isWaiting && !isStoppingForObstacle && !isAvoidingObstacle)
        {
            agent.speed = assignedWalkSpeed;
        }
    }

    // ------------------------------------------------------------------ //
    //  Animation                                                         //
    // ------------------------------------------------------------------ //

    void UpdateAnimator()
    {
        float currentSpeed = agent.velocity.magnitude;
        animator.SetFloat(SpeedHash, currentSpeed, 0.1f, Time.deltaTime);
    }

    // ------------------------------------------------------------------ //
    //  Public accessors                                                  //
    // ------------------------------------------------------------------ //

    public Vector3 GetROSVelocity() => agent.velocity;
    public bool IsWaiting() => isWaiting;
    public bool IsStoppingForObstacle() => isStoppingForObstacle;
    public bool IsAvoidingObstacle() => isAvoidingObstacle;

    // ------------------------------------------------------------------ //
    //  Debug                                                             //
    // ------------------------------------------------------------------ //

    void OnDrawGizmosSelected()
    {
        // Personal space radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, personalSpaceRadius);

        // Obstacle detect radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, obstacleDetectRadius);

        // Resume distance
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, obstacleResumeDistance);

        // Wander area
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            Application.isPlaying ? origin : transform.position,
            wanderRadius);

        if (Application.isPlaying)
        {
            if (hasMainGoal)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(mainGoal, 0.15f);
                Gizmos.DrawLine(transform.position, mainGoal);
            }

            if (isAvoidingObstacle)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(avoidGoal, 0.2f);
                Gizmos.DrawLine(transform.position, avoidGoal);
            }

            if (activeObstacleTransform != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(transform.position, activeObstacleTransform.position);
            }
        }
    }

    void OnFootstep()
    {
        // Optional: play sound later
        // Debug.Log("Footstep");
    }
}