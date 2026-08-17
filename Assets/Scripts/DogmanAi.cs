using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class DogmanAI : MonoBehaviour, IDamageable
{
    public enum DogmanState { Patrol, Chasing, Dead }

    [Header("Komponenty")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyHitbox weaponHitbox;
    [SerializeField] private MegalithPass megalithPass;

    [Header("Typ Przeciwnika i Obrażenia")]
    [SerializeField] private EnemyType enemyType = EnemyType.Dogman;
    [SerializeField] private float minDamage = 5f;
    [SerializeField] private float maxDamage = 15f;

    [Header("Statystyki Walki")]
    [SerializeField] private float health = 150f;
    [SerializeField] private float attackRange = 3.5f;
    [SerializeField] private float attackCooldown = 1.8f;

    [Header("Płynny Patrol w Kręgu")]
    [SerializeField] private Transform centerBoulder;
    [SerializeField] private float patrolRadius = 18f;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 8f;
    [SerializeField] private float leadAngleOffset = 45f;

    public DogmanState CurrentState { get; private set; } = DogmanState.Patrol;

    private float lastAttackTime;
    private bool isDead = false;

    private void OnValidate()
    {
        switch (enemyType)
        {
            case EnemyType.Guard:
                minDamage = 2f;
                maxDamage = 10f;
                break;
            case EnemyType.Dogman:
                minDamage = 5f;
                maxDamage = 15f;
                break;
            case EnemyType.DarkMage:
                minDamage = 15f;
                maxDamage = 20f;
                break;
        }
    }

    private void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();
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

    // --- PŁYNNY PATROL ---

    private IEnumerator SmoothCirclePatrolRoutine()
    {
        while (CurrentState == DogmanState.Patrol)
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
        while (CurrentState == DogmanState.Chasing && target != null)
        {
            Vector3 cameraPos = target.position;
            Vector3 feetPos = cameraPos;

            int groundLayerMask = LayerMask.GetMask("Ground"); 
            if (Physics.Raycast(cameraPos, Vector3.down, out RaycastHit hit, 20f, groundLayerMask)){
                feetPos = hit.point;
            }

            Vector3 targetNavMeshPos = feetPos;
            if (NavMesh.SamplePosition(feetPos, out NavMeshHit navHit, 4f, NavMesh.AllAreas)){
                targetNavMeshPos = navHit.position;
            }

            if (agent) agent.stoppingDistance = attackRange - 0.5f;

            float distanceToPlayer = Vector3.Distance(transform.position, targetNavMeshPos);

            // LOGIKA ZTRZYMANIA I ATAKU
            if (distanceToPlayer <= attackRange)
            {
                if (agent && agent.enabled)
                {
                    agent.isStopped = true;
                }

                SetAnimSpeed(0f);

                Vector3 lookDir = targetNavMeshPos - transform.position;
                lookDir.y = 0f;
                if (lookDir != Vector3.zero){
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
                }

                if (Time.time >= lastAttackTime + attackCooldown){
                    lastAttackTime = Time.time;
                    PerformAttack();
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

    private void PerformAttack()
    {
        if (animator) animator.SetTrigger("Attack");
        StartCoroutine(AttackHitboxRoutine());
    }

    private IEnumerator AttackHitboxRoutine()
    {
        float calculatedDamage = Random.Range(minDamage, maxDamage);

        yield return new WaitForSeconds(0.2f);

        if (weaponHitbox != null)
        {
            weaponHitbox.EnableHitbox(calculatedDamage);
        }

        yield return new WaitForSeconds(1.4f);

        if (weaponHitbox != null)
        {
            weaponHitbox.DisableHitbox();
        }
    }


    private void UpdateAnimSpeed()
    {
        if (animator && agent && agent.enabled)
        {
            float currentSpeed = agent.velocity.magnitude;
            animator.SetFloat("Speed", currentSpeed, 0.15f, Time.deltaTime);
        }
    }

    private void SetAnimSpeed(float speed){
        if (animator) animator.SetFloat("Speed", speed);
    }

    // --- OBRAŻENIA I ŚMIERĆ ---

    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (isDead) return;

        health -= damage;

        Transform player = Camera.main != null ? Camera.main.transform : null;
        if (player != null && CurrentState == DogmanState.Patrol){
            TriggerCombat(player);
        }

        if (health <= 0f){
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        CurrentState = DogmanState.Dead;

        StopAllCoroutines();

        if (agent != null) agent.enabled = false;
        if (weaponHitbox != null) weaponHitbox.DisableHitbox();

        foreach (Collider c in GetComponentsInChildren<Collider>()){
            c.enabled = false;
        }

        if (animator != null) animator.SetTrigger("Die");

        if (megalithPass != null){
            megalithPass.UnlockPass();
        }

        Destroy(gameObject, 6.0f);
    }
}