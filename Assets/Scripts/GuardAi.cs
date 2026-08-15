using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Splines;

public enum GuardState { Sleeping, WalkingToPost, OnDuty, WalkingToQuarters, Alerted, Chasing }

public class GuardAI : MonoBehaviour, IDamageable
{
    [Header("Komponenty")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private float pathOffsetRange = 1.6f;

    [Header("Prędkości Poruszania się")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float chaseSpeed = 7f;  

    [Header("Komponenty Walki")]
    [SerializeField] private EnemyHitbox weaponHitbox;

    [Header("Ustawienia Magistrali")]
    [Tooltip("Gęstość punktów na trasie Spline. Większa wartość = dokładniejsze zakręty.")]
    [SerializeField] private int pathResolution = 50;

    [Header("Walka i Atak")]
    [SerializeField] private float attackRange = 3.5f;
    [SerializeField] private float attackCooldown = 2.0f;
    [SerializeField] private float waitingRange = 8.0f;

    [Header("Typ Przeciwnika i Obrażenia")]
    [SerializeField] private EnemyType enemyType = EnemyType.Guard;
    [SerializeField] private float minDamage = 2f;
    [SerializeField] private float maxDamage = 10f;

    public bool IsInteracting { get; private set; } = false;

    /// <summary>
    /// Flaga określająca, czy strażnik opuścił magistralę Spline i przebywa na końcowym odcinku NavMesh do posterunku.
    /// </summary>
    public bool IsOffSpline { get; private set; } = false;

    private Coroutine activeBehaviorCoroutine;

    private void StartBehaviorCoroutine(IEnumerator routine)
    {
        StopBehaviorCoroutine();
        activeBehaviorCoroutine = StartCoroutine(routine);
    }

    private void StopBehaviorCoroutine()
    {
        if (activeBehaviorCoroutine != null)
        {
            StopCoroutine(activeBehaviorCoroutine);
            activeBehaviorCoroutine = null;
        }
    }

    public void SetInteracting(bool value)
    {
        IsInteracting = value;

        if (agent != null && agent.enabled)
        {
            if (value)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                SetAnimSpeed(0f);
            }
            else
            {
                if (CurrentState == GuardState.WalkingToPost || 
                    CurrentState == GuardState.WalkingToQuarters || 
                    CurrentState == GuardState.Chasing)
                {
                    agent.isStopped = false;
                }
            }
        }
    }

    public void TriggerButtonPushAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger("PressBtn");
        }
    }

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

    private float lastAttackTime;

    [SerializeField] private float health = 100f;

    public GuardState CurrentState { get; private set; } = GuardState.Sleeping;

    private CastleShiftManager manager;
    private GuardPost assignedPost;
    private Bed currentBed;

    public bool isDead { get; private set; } = false;

    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (isDead) return;

        health -= damage;

        Transform player = Camera.main != null ? Camera.main.transform : null;

        if (player != null)
        {
            AlertGuard(player);
            AlertAllGuardsOnScene(player);
        }

        if (health <= 0)
        {
            Die();
        }
    }

    public static bool hasNotifiedAllAlerted = false;

    private void AlertAllGuardsOnScene(Transform playerTransform)
    {
        GuardAI[] allGuards = FindObjectsByType<GuardAI>(FindObjectsSortMode.None);
        foreach (GuardAI guard in allGuards)
        {
            guard.AlertGuard(playerTransform);
        }

        if (!hasNotifiedAllAlerted)
        {
            hasNotifiedAllAlerted = true;
            NotificationManager.Show("Wszyscy strażnicy zostali zaalarmowani!", NotificationType.Danger);
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        StopBehaviorCoroutine();

        if (agent != null) agent.enabled = false;
        if (weaponHitbox != null) weaponHitbox.DisableHitbox();

        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
        }

        if (animator != null) animator.SetTrigger("Die");

        Destroy(gameObject, 6.0f);
    }

    private void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();
    }
    
    public void AlertGuard(Transform target)
    {
        if (CurrentState == GuardState.Chasing || target == null) return;

        bool wasSleeping = (CurrentState == GuardState.Sleeping);

        if (!wasSleeping && currentBed != null)
        {
            currentBed.IsOccupied = false;
            currentBed.IsReserved = false;
            currentBed = null;
        }

        IsInteracting = false;
        CurrentState = GuardState.Chasing;

        if (agent)
        {
            agent.speed = chaseSpeed;
            agent.acceleration = chaseSpeed * 2.0f;
            agent.autoBraking = true;
        }

        if (wasSleeping)
        {
            StartBehaviorCoroutine(WakeUpAndChaseRoutine(target));
        }
        else
        {
            if (agent)
            {
                agent.enabled = true;
                agent.Warp(transform.position); 
                agent.isStopped = false;
            }

            StartBehaviorCoroutine(ChaseRoutine(target));
        }
    }

    private IEnumerator WakeUpAndChaseRoutine(Transform target)
    {
        Vector3 bedPos = currentBed ? currentBed.sleepAnchor.position : transform.position;
        Quaternion bedRot = currentBed ? currentBed.sleepAnchor.rotation : transform.rotation;

        if (currentBed)
        {
            currentBed.IsOccupied = false;
            currentBed.IsReserved = false;
            currentBed = null;
        }

        if (animator) animator.SetBool("isSleeping", false);

        while (animator != null && (animator.IsInTransition(0) || !animator.GetCurrentAnimatorStateInfo(0).IsName("guard_standup")))
        {
            yield return null;
        }

        while (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("guard_standup"))
        {
            yield return null;
        }

        transform.SetPositionAndRotation(bedPos, bedRot * Quaternion.Euler(0f, 180f, 0f));

        if (agent)
        {
            agent.enabled = true;
            if (NavMesh.SamplePosition(bedPos, out NavMeshHit bedHit, 10f, NavMesh.AllAreas))
            {
                agent.Warp(bedHit.position);
            }
            else
            {
                agent.Warp(transform.position);
            }
            agent.isStopped = false;
        }

        yield return StartCoroutine(ChaseRoutine(target));
    }

    private IEnumerator ChaseRoutine(Transform target)
    {
        while (CurrentState == GuardState.Chasing && target != null)
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

            bool isPrimaryAttacker = IsClosestChasingGuard(targetNavMeshPos);
            float currentTargetRange = isPrimaryAttacker ? attackRange : waitingRange;

            if (agent) agent.stoppingDistance = currentTargetRange - 0.5f;

            float distanceToPlayer = Vector3.Distance(transform.position, targetNavMeshPos);

            if (distanceToPlayer <= currentTargetRange)
            {
                if (agent && agent.enabled)
                {
                    agent.isStopped = true;
                }

                SetAnimSpeed(0f);

                Vector3 lookDir = targetNavMeshPos - transform.position;
                lookDir.y = 0f;
                if (lookDir != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
                }

                if (isPrimaryAttacker && distanceToPlayer <= attackRange)
                {
                    if (Time.time >= lastAttackTime + attackCooldown)
                    {
                        lastAttackTime = Time.time;
                        PerformAttack(target);
                    }
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

    private bool IsClosestChasingGuard(Vector3 targetPos)
    {
        GuardAI[] allGuards = FindObjectsByType<GuardAI>(FindObjectsSortMode.None);
        float myDistSqr = (transform.position - targetPos).sqrMagnitude;

        foreach (GuardAI guard in allGuards)
        {
            if (guard != this && guard.CurrentState == GuardState.Chasing)
            {
                float otherDistSqr = (guard.transform.position - targetPos).sqrMagnitude;
                if (otherDistSqr < myDistSqr)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void PerformAttack(Transform target)
    {
        if (animator) animator.SetTrigger("Attack");
        StartCoroutine(AttackHitboxRoutine());
    }

    private IEnumerator AttackHitboxRoutine()
    {
        float calculatedDamage = Random.Range(minDamage, maxDamage);

        yield return new WaitForSeconds(0.1f);

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

    // --- INICJALIZACJA ---

    public void InitDuty(GuardPost post, CastleShiftManager shiftManager)
    {
        manager = shiftManager;
        assignedPost = post;
        assignedPost.currentGuard = this;
        CurrentState = GuardState.OnDuty;
        IsOffSpline = true;

        transform.SetPositionAndRotation(post.Position.position, post.Position.rotation);
        if (agent)
        {
            agent.enabled = true;
            agent.speed = walkSpeed;
            agent.isStopped = true;
        }
        SetAnimSpeed(0f);
    }

    public void InitSleeping(Bed bed, CastleShiftManager shiftManager)
    {
        manager = shiftManager;
        currentBed = bed;
        currentBed.IsOccupied = true;
        CurrentState = GuardState.Sleeping;
        IsOffSpline = false;

        transform.SetPositionAndRotation(bed.sleepAnchor.position, bed.sleepAnchor.rotation);
        if (agent) agent.enabled = false;

        SetAnimSpeed(0f);
        if (animator)
        {
            animator.SetBool("isSleeping", true);
            animator.Play("guard_sleeping_loop", 0, 0f);
            animator.Update(0f);
        }
    }

    // --- SEKWENCJA WSTAWANIA I MARSZU NA POSTERUNEK ---

    public void WakeUpAndGoToPost(GuardPost targetPost, float triggerDistance)
    {
        if (CurrentState == GuardState.Chasing || CurrentState == GuardState.Alerted) return;
        assignedPost = targetPost;
        CurrentState = GuardState.WalkingToPost;

        StartBehaviorCoroutine(WakeUpAndGoRoutine());
    }

private IEnumerator WakeUpAndGoRoutine()
    {
        IsOffSpline = false;

        // 1. Wstawanie z łóżka
        Vector3 bedPos = currentBed ? currentBed.sleepAnchor.position : transform.position;
        Quaternion bedRot = currentBed ? currentBed.sleepAnchor.rotation : transform.rotation;

        if (currentBed)
        {
            currentBed.IsOccupied = false;
            currentBed.IsReserved = false;
            currentBed = null;
        }

        if (animator) animator.SetBool("isSleeping", false);

        while (animator != null && (animator.IsInTransition(0) || !animator.GetCurrentAnimatorStateInfo(0).IsName("guard_standup")))
        {
            yield return null;
        }

        while (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("guard_standup"))
        {
            yield return null;
        }

        transform.SetPositionAndRotation(bedPos, bedRot * Quaternion.Euler(0f, 180f, 0f));

        if (agent)
        {
            agent.enabled = true;
            agent.speed = walkSpeed;
            agent.stoppingDistance = 0f;
            agent.isStopped = false;
        }

        Vector3 startBedPos = transform.position;
        Vector3 finalPostPos = assignedPost.Position.position;

        List<Vector3> splinePoints = GetSplinePathSegment(manager != null ? manager.SharedMainPath : null, startBedPos, finalPostPos, pathResolution);

        // 2. MARSZ PO MAGISTRALI (SPLINE)
        IsOffSpline = false;
        for (int i = 0; i < splinePoints.Count; i++)
        {
            agent.SetDestination(splinePoints[i]);

            while (true)
            {
                while (IsInteracting)
                {
                    if (agent && agent.enabled) { agent.isStopped = true; agent.velocity = Vector3.zero; }
                    SetAnimSpeed(0f);
                    yield return null;
                }

                if (agent && agent.enabled && agent.isStopped)
                {
                    agent.isStopped = false;
                }

                UpdateAnimSpeed();

                Vector3 flatAgentPos = new Vector3(transform.position.x, 0f, transform.position.z);
                Vector3 flatTargetPos = new Vector3(splinePoints[i].x, 0f, splinePoints[i].z);

                if (!agent.pathPending && Vector3.Distance(flatAgentPos, flatTargetPos) <= 0.6f)
                {
                    break;
                }

                yield return null;
            }
        }

        // 3. FAZA ZJAZDU ZE SPLINE DO POSTERUNKU (NavMesh)
        IsOffSpline = true;
        agent.SetDestination(finalPostPos);

        while (true)
        {
            GuardAI oldGuard = assignedPost != null ? assignedPost.currentGuard : null;
            bool isOldGuardValid = (oldGuard != null && oldGuard != this && !oldGuard.isDead);

            if (isOldGuardValid)
            {
                bool isOldGuardBusy = oldGuard.IsInteracting;

                // A) Jeśli stary strażnik wykonuje interakcję (otwiera bramę), nowy zatrzymuje się
                if (isOldGuardBusy)
                {
                    if (!agent.pathPending && agent.remainingDistance <= 7.5f)
                    {
                        if (agent && agent.enabled)
                        {
                            agent.isStopped = true;
                            agent.velocity = Vector3.zero;
                        }
                        SetAnimSpeed(0f);
                        yield return null;
                        continue; // Czekamy w pętli aż IsInteracting starego straznika zmieni się na false
                    }
                }
                else if (oldGuard.CurrentState == GuardState.OnDuty)
                {
                    // B)  
                    if (!agent.pathPending && agent.remainingDistance <= 8.0f)
                    {
                        oldGuard.ReturnToQuarters();
                    }
                }
            }

            // Obsługa własnej interakcji nowego strażnika
            while (IsInteracting)
            {
                if (agent && agent.enabled) { agent.isStopped = true; agent.velocity = Vector3.zero; }
                SetAnimSpeed(0f);
                yield return null;
            }

            // Wznawiamy marsz po postoju
            if (agent && agent.enabled && agent.isStopped)
            {
                agent.isStopped = false;
            }

            UpdateAnimSpeed();

            // Osiągnięcie posterunku
            if (!agent.pathPending && agent.remainingDistance <= 0.2f)
            {
                break;
            }

            yield return null;
        }

        // 4. PRZEJĘCIE POSTERUNKU
        TakeDutyAtPost();
    }

    private void TakeDutyAtPost()
    {
        if (assignedPost == null) return;

        assignedPost.currentGuard = this;
        assignedPost.incomingGuard = null;
        CurrentState = GuardState.OnDuty;
        IsOffSpline = true;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        transform.rotation = assignedPost.InitialRotation;

        SetAnimSpeed(0f);
    }

    // --- SEKWENCJA POWROTU DO KWATERY I ZAŚNIĘCIA ---

    public void ReturnToQuarters()
    {
        if (CurrentState == GuardState.Chasing || CurrentState == GuardState.Alerted) return;
        if (CurrentState == GuardState.WalkingToQuarters) return;

        CurrentState = GuardState.WalkingToQuarters;
        currentBed = manager ? manager.GetAndReserveFreeBed() : null;

        StartBehaviorCoroutine(ReturnToQuartersRoutine());
    }

private IEnumerator ReturnToQuartersRoutine()
    {
        // 1. Czekamy na zakończenie ewentualnej animacji/interakcji
        while (IsInteracting)
        {
            if (agent && agent.enabled)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            SetAnimSpeed(0f);
            yield return null;
        }

        // 2. Przywrócenie pełnej kontroli NavMeshAgenta
        if (agent && agent.enabled)
        {
            agent.enabled = true;
            agent.isStopped = false;
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.speed = walkSpeed;
        }

        if (currentBed == null)
        {
            Debug.LogWarning($"{gameObject.name}: Brak dostępnego łóżka w kwaterze.");
            yield break;
        }

        Vector3 startPostPos = transform.position;
        Vector3 bedPos = currentBed.sleepAnchor.position;

        List<Vector3> splinePoints = GetSplinePathSegment(manager != null ? manager.SharedMainPath : null, startPostPos, bedPos, pathResolution);

        // Powrót z posterunku do węzła Spline
        IsOffSpline = true;

        for (int i = 0; i < splinePoints.Count; i++)
        {
            agent.SetDestination(splinePoints[i]);

            // Po osiągnięciu pierwszego punktu magistrali ustawiamy IsOffSpline = false
            if (i > 0) IsOffSpline = false;

            while (true)
            {
                while (IsInteracting)
                {
                    if (agent && agent.enabled)
                    {
                        agent.isStopped = true;
                        agent.velocity = Vector3.zero;
                    }
                    SetAnimSpeed(0f);
                    yield return null;
                }

                if (agent && agent.enabled && agent.isStopped)
                {
                    agent.isStopped = false;
                }

                UpdateAnimSpeed();

                Vector3 flatAgentPos = new Vector3(transform.position.x, 0f, transform.position.z);
                Vector3 flatTargetPos = new Vector3(splinePoints[i].x, 0f, splinePoints[i].z);

                if (!agent.pathPending && Vector3.Distance(flatAgentPos, flatTargetPos) <= 0.6f)
                {
                    break;
                }

                yield return null;
            }
        }

        // Zjazd ze Spline do kwatery
        IsOffSpline = true;
        agent.SetDestination(bedPos);

        while (true)
        {
            while (IsInteracting)
            {
                if (agent && agent.enabled)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                SetAnimSpeed(0f);
                yield return null;
            }

            if (agent && agent.enabled && agent.isStopped)
            {
                agent.isStopped = false;
            }

            UpdateAnimSpeed();

            float distToBed = Vector3.Distance(transform.position, bedPos);

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f && distToBed <= 1.5f)
            {
                break;
            }

            yield return null;
        }

        if (agent)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        transform.SetPositionAndRotation(bedPos, currentBed.sleepAnchor.rotation);

        currentBed.IsOccupied = true;
        currentBed.IsReserved = false;
        CurrentState = GuardState.Sleeping;

        SetAnimSpeed(0f);
        if (animator) animator.SetBool("isSleeping", true);
    }

    // --- METODA POMOCNICZA DLA MAGISTRALI (SPLINE) ---

    private List<Vector3> GetSplinePathSegment(SplineContainer spline, Vector3 startPos, Vector3 endPos, int resolution = 50)
    {
        List<Vector3> rawPoints = new List<Vector3>();

        if (spline == null || spline.Spline == null || spline.Spline.Count == 0) 
            return rawPoints;

        Spline mainSpline = spline.Spline;
        int knotCount = mainSpline.Count;

        int startKnotIndex = 0;
        float minStartKnotDist = float.MaxValue;
        Vector3 startKnotWorldPos = Vector3.zero;

        for (int k = 0; k < knotCount; k++)
        {
            Vector3 knotWorldPos = spline.transform.TransformPoint((Vector3)mainSpline[k].Position);
            float dist = Vector3.Distance(startPos, knotWorldPos);

            if (dist < minStartKnotDist)
            {
                minStartKnotDist = dist;
                startKnotIndex = k;
                startKnotWorldPos = knotWorldPos;
            }
        }

        int endKnotIndex = 0;
        float minEndKnotDist = float.MaxValue;
        Vector3 endKnotWorldPos = Vector3.zero;

        for (int k = 0; k < knotCount; k++)
        {
            Vector3 knotWorldPos = spline.transform.TransformPoint((Vector3)mainSpline[k].Position);
            float dist = Vector3.Distance(endPos, knotWorldPos);

            if (dist < minEndKnotDist)
            {
                minEndKnotDist = dist;
                endKnotIndex = k;
                endKnotWorldPos = knotWorldPos;
            }
        }

        int startIndex = 0;
        int endIndex = 0;
        float minDistStartKnot = float.MaxValue;
        float minDistEndKnot = float.MaxValue;

        for (int i = 0; i < resolution; i++)
        {
            float t = (float)i / (resolution - 1);
            Vector3 worldPos = spline.EvaluatePosition(t);

            float distToStartKnot = Vector3.Distance(startKnotWorldPos, worldPos);
            if (distToStartKnot < minDistStartKnot)
            {
                minDistStartKnot = distToStartKnot;
                startIndex = i;
            }

            float distToEndKnot = Vector3.Distance(endKnotWorldPos, worldPos);
            if (distToEndKnot < minDistEndKnot)
            {
                minDistEndKnot = distToEndKnot;
                endIndex = i;
            }
        }

        int step = (startIndex <= endIndex) ? 1 : -1;
        int currentIndex = startIndex;

        while (true)
        {
            float t = (float)currentIndex / (resolution - 1);
            rawPoints.Add(spline.EvaluatePosition(t));

            if (currentIndex == endIndex) break;
            currentIndex += step;
        }

        if (rawPoints.Count > 0)
        {
            rawPoints[0] = startKnotWorldPos;
            rawPoints[rawPoints.Count - 1] = endKnotWorldPos;
        }

        List<Vector3> offsetPoints = new List<Vector3>();
        float guardSideOffset = Random.Range(-pathOffsetRange, pathOffsetRange);

        int count = rawPoints.Count;
        for (int i = 0; i < count; i++)
        {
            Vector3 current = rawPoints[i];

            float blendFactor = 1f;
            if (count > 2)
            {
                float progress = (float)i / (count - 1);
                blendFactor = Mathf.Sin(progress * Mathf.PI); 
            }

            Vector3 forward = Vector3.zero;
            if (i < count - 1)
                forward = rawPoints[i + 1] - current;
            else if (i > 0)
                forward = current - rawPoints[i - 1];

            forward.y = 0f;

            if (forward.sqrMagnitude > 0.001f)
            {
                Vector3 sideDirection = Vector3.Cross(forward.normalized, Vector3.up).normalized;
                current += sideDirection * (guardSideOffset * blendFactor);
            }

            offsetPoints.Add(current);
        }

        return offsetPoints;
    }

    // --- FUNKCJE POMOCNICZE ---

    private void UpdateAnimSpeed()
    {
        if (animator && agent && agent.enabled)
        {
            float currentSpeed = agent.velocity.magnitude;
            animator.SetFloat("Speed", currentSpeed, 0.15f, Time.deltaTime);
        }
    }

    private void SetAnimSpeed(float speed)
    {
        if (animator) animator.SetFloat("Speed", speed);
    }
}