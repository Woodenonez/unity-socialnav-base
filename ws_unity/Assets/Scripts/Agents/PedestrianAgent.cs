using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Realistic pedestrian agent with:
/// - Walk/run/idle animation driven by NavMeshAgent speed
/// - Varied movement speeds per agent
/// - Idle pauses at waypoints
/// - Social behaviors: yields to robot, maintains personal space from other pedestrians
///
/// Attach to: Pedestrian prefab (alongside NavMeshAgent and Animator)
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class PedestrianAgent : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Inspector settings                                                //
    // ------------------------------------------------------------------ //

    [Header("Identity")]
    public int    pedestrianId   = 0;
    public string pedestrianName = "pedestrian_0";

    [Header("Wandering")]
    public float wanderRadius  = 12f;
    public float minWaitTime   = 1.5f;
    public float maxWaitTime   = 4.0f;
    public float goalTolerance = 0.6f;

    [Header("Speed Profile")]
    [Tooltip("Base walk speed in m/s. Randomized per agent on Start.")]
    public float minWalkSpeed = 0.5f;
    public float maxWalkSpeed = 1.5f;
    [Tooltip("Chance (0-1) this agent will occasionally jog.")]
    public float jogChance    = 0.2f;
    public float jogSpeed     = 2.2f;
    public float runSpeed     = 3.5f;

    [Header("Social Behaviour")]
    [Tooltip("Distance at which pedestrian slows down near other pedestrians.")]
    public float personalSpaceRadius = 1.2f;
    [Tooltip("Distance at which pedestrian yields to the robot.")]
    public float robotYieldRadius    = 2.5f;
    [Tooltip("Speed multiplier when yielding to robot (0 = stop, 0.3 = slow creep).")]
    public float yieldSpeedMultiplier = 0.0f;
    [Tooltip("Tag used by the robot GameObject.")]
    public string robotTag = "Robot";
    [Tooltip("Whether this pedestrian uses external target positions or random wandering.")]
    public bool hasExternalTarget = false;

    // ------------------------------------------------------------------ //
    //  Private state                                                     //
    // ------------------------------------------------------------------ //

    private NavMeshAgent  agent;
    private Animator      animator;
    private Vector3       origin;

    private float         assignedWalkSpeed;
    private float         assignedJogSpeed;
    private float         waitTimer  = 0f;
    private bool          isWaiting  = false;
    private bool          isYielding = false;

    // Animator parameter hash (faster than string lookup every frame)
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    // Cache nearby robots and pedestrians
    private Transform     robotTransform;
    private List<PedestrianAgent> nearbyPedestrians = new List<PedestrianAgent>();
    private float         socialCheckTimer = 0f;
    private const float   SocialCheckInterval = 0.5f;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle                                                   //
    // ------------------------------------------------------------------ //

    void Start()
    {
        agent    = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        origin   = transform.position;

        // Randomise speed per agent for variety
        assignedWalkSpeed = Random.Range(minWalkSpeed, maxWalkSpeed);
        assignedJogSpeed  = (Random.value < jogChance) ? jogSpeed : assignedWalkSpeed;

        agent.speed           = assignedWalkSpeed;
        agent.angularSpeed    = 240f;
        agent.acceleration    = 8f;
        agent.stoppingDistance = goalTolerance;

        // Rotate character model to face movement direction smoothly
        agent.updateRotation = true;

        // Find robot in scene
        var robotObj = GameObject.FindWithTag(robotTag);
        if (robotObj != null)
            robotTransform = robotObj.transform;

        // Slight random delay before first move so all agents don't start simultaneously
        Invoke(nameof(SetNewDestination), Random.Range(0f, 1.5f));
    }

    void Update()
    {
        socialCheckTimer += Time.deltaTime;
        if (socialCheckTimer >= SocialCheckInterval)
        {
            CheckSocialBehaviours();
            socialCheckTimer = 0f;
        }

        if (isYielding)
        {
            // Completely stopped or slow creep while yielding
            agent.speed = assignedWalkSpeed * yieldSpeedMultiplier;
            UpdateAnimator();
            return;
        }

        if (isWaiting)
        {
            agent.speed = 0f;
            waitTimer  -= Time.deltaTime;
            UpdateAnimator();
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                SetNewDestination();
            }
            return;
        }

        // Check if reached destination
        if (!agent.pathPending &&
            agent.remainingDistance <= goalTolerance)
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
        if (hasExternalTarget) return;

        if (!isWaiting && !isYielding)
        {
            // Occasionally jog to destination
            agent.speed = (Random.value < jogChance)
                ? assignedJogSpeed
                : assignedWalkSpeed;
        }

        Vector3 randomDir = Random.insideUnitSphere * wanderRadius;
        randomDir   += origin;
        randomDir.y  = origin.y;  // stay on ground plane

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDir, out hit, wanderRadius, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(origin);
    }

    // ------------------------------------------------------------------ //
    //  Set target                                                        //
    // ------------------------------------------------------------------ //

    public bool SetTargetPosition(Vector3 target)
    {
        hasExternalTarget = true;
        isWaiting = false;
        isYielding = false;
        waitTimer = 0f;

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        NavMeshHit hit;
        if (NavMesh.SamplePosition(target, out hit, 2.0f, NavMesh.AllAreas))
        {
            agent.speed = assignedWalkSpeed;
            return agent.SetDestination(hit.position);
        }

        return false;
    }

    // ------------------------------------------------------------------ //
    //  Social behaviours                                                 //
    // ------------------------------------------------------------------ //

    void CheckSocialBehaviours()
    {
        // 1. Yield to robot if it's close
        if (robotTransform != null)
        {
            float distToRobot = Vector3.Distance(transform.position,
                                                  robotTransform.position);
            if (distToRobot < robotYieldRadius)
            {
                if (!isYielding)
                {
                    isYielding = true;
                    agent.ResetPath();  // stop current path
                    // Face away from robot slightly
                    Vector3 awayDir = (transform.position - robotTransform.position).normalized;
                    awayDir.y = 0f;
                    if (awayDir != Vector3.zero)
                        transform.rotation = Quaternion.Slerp(
                            transform.rotation,
                            Quaternion.LookRotation(awayDir),
                            0.3f
                        );
                }
                return;
            }
            else if (isYielding)
            {
                // Robot has passed — resume walking
                isYielding = false;
                SetNewDestination();
            }
        }

        // 2. Slow down in personal space of nearby pedestrians
        nearbyPedestrians.Clear();
        Collider[] hits = Physics.OverlapSphere(
            transform.position, personalSpaceRadius);

        foreach (var col in hits)
        {
            if (col.gameObject == gameObject) continue;
            var other = col.GetComponent<PedestrianAgent>();
            if (other != null)
                nearbyPedestrians.Add(other);
        }

        if (nearbyPedestrians.Count > 0 && !isWaiting && !isYielding)
        {
            // Reduce speed proportional to crowding
            float crowdFactor = Mathf.Clamp01(
                1f - (nearbyPedestrians.Count * 0.25f));
            agent.speed = assignedWalkSpeed * crowdFactor;
        }
        else if (!isWaiting && !isYielding)
        {
            agent.speed = assignedWalkSpeed;
        }
    }

    // ------------------------------------------------------------------ //
    //  Animation                                                           //
    // ------------------------------------------------------------------ //

    void UpdateAnimator()
    {
        // Feed actual agent speed to the animator
        // Animator transitions: Idle(0) <-> Walk(0.1-1.5) <-> Run(>1.5)
        float currentSpeed = agent.velocity.magnitude;
        animator.SetFloat(SpeedHash, currentSpeed, 0.1f, Time.deltaTime);
    }

    // ------------------------------------------------------------------ //
    //  Public accessors for PedestrianPublisher                           //
    // ------------------------------------------------------------------ //

    public Vector3 GetROSVelocity() => agent.velocity;
    public bool    IsYielding()     => isYielding;
    public bool    IsWaiting()      => isWaiting;

    // ------------------------------------------------------------------ //
    //  Debug                                                               //
    // ------------------------------------------------------------------ //

    void OnDrawGizmosSelected()
    {
        // Personal space radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, personalSpaceRadius);

        // Robot yield radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, robotYieldRadius);

        // Wander area
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            Application.isPlaying ? origin : transform.position,
            wanderRadius);
    }
}