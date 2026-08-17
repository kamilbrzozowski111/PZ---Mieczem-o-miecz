using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class DogmanAI : BaseEnemyAI
{
    public enum DogmanState { Patrol, Chasing, Dead }

    [Header("Komponenty Specjalne")]
    [SerializeField] private MegalithPass megalithPass;

    [Header("Płynny Patrol w Kręgu")]
    [SerializeField] private Transform centerBoulder;
    [SerializeField] private float patrolRadius = 18f;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float leadAngleOffset = 45f;

    public DogmanState CurrentState { get; private set; } = DogmanState.Patrol;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        if (agent != null)
        {
            agent.enabled = true;
            agent.speed = patrolSpeed;
            agent.autoBraking = false;
            agent.isStopped = false;
        }

        StartCoroutine(SmoothCirclePatrolRoutine());
    }

    // --- PŁYNNY PATROL PO OKRĘGU ---

    private IEnumerator SmoothCirclePatrolRoutine()
    {
        while (CurrentState == DogmanState.Patrol && !isDead)
        {
            if (centerBoulder != null && agent != null && agent.enabled)
            {
                Vector3 offsetFromCenter = transform.position - centerBoulder.position;
                offsetFromCenter.y = 0f;

                float currentAngle = Mathf.Atan2(offsetFromCenter.z, offsetFromCenter.x);
                float targetAngle = currentAngle + (leadAngleOffset * Mathf.Deg2Rad);

                Vector3 targetPos = centerBoulder.position + new Vector3(
                    Mathf.Cos(targetAngle) * patrolRadius,
                    0f,
                    Mathf.Sin(targetAngle) * patrolRadius
                );

                if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    targetPos = hit.position;
                }

                agent.SetDestination(targetPos);
                UpdateAnimSpeed();
            }

            yield return new WaitForSeconds(0.05f);
        }
    }

    // --- LOGIKA WALKI ---

    public void TriggerCombat(Transform target)
    {
        if (isDead || CurrentState == DogmanState.Chasing || target == null) return;

        StopAllCoroutines();
        CurrentState = DogmanState.Chasing;

        if (agent != null)
        {
            agent.enabled = true;
            agent.speed = chaseSpeed;
            agent.autoBraking = true;
            agent.isStopped = false;
        }

        StartCoroutine(ChaseRoutine(target));
    }

    private IEnumerator ChaseRoutine(Transform target)
    {
        while (CurrentState == DogmanState.Chasing && target != null && !isDead)
        {
            if (!TryGetTargetNavMeshPosition(target, out Vector3 targetNavMeshPos))
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            if (agent) agent.stoppingDistance = attackRange - 0.5f;

            float distanceToPlayer = Vector3.Distance(transform.position, targetNavMeshPos);

            if (distanceToPlayer <= attackRange)
            {
                if (agent && agent.enabled)
                {
                    agent.isStopped = true;
                }

                SetAnimSpeed(0f);
                FaceTarget(targetNavMeshPos);

                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    lastAttackTime = Time.time;
                    PerformBaseAttack(0.2f, 1.4f);
                }
            }
            else
            {
                if (agent && agent.enabled)
                {
                    agent.isStopped = false;
                    agent.SetDestination(targetNavMeshPos);
                    UpdateAnimSpeed();
                }
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    protected override void OnDamaged(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (CurrentState == DogmanState.Patrol)
        {
            Transform player = PlayerTargetProvider.GetPlayerTransform();
            if (player != null)
            {
                TriggerCombat(player);
            }
        }
    }

    protected override void OnDeath()
    {
        CurrentState = DogmanState.Dead;

        if (megalithPass != null)
        {
            megalithPass.UnlockPass();
        }

        base.OnDeath();
    }
}