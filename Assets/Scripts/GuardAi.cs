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

    [Header("Komponenty Walki")]
    [SerializeField] private EnemyHitbox weaponHitbox;

    [Header("Ustawienia Magistrali")]
    [Tooltip("Gęstość punktów na trasie Spline. Większa wartość = dokładniejsze zakręty.")]
    [SerializeField] private int pathResolution = 50;

    [Header("Walka i Atak")]
    [SerializeField] private float attackRange = 3.5f;       // Zasięg ataku
    [SerializeField] private float attackCooldown = 2.0f;    // Czas (w sekundach) między uderzeniami
    [SerializeField] private float waitingRange = 8.0f;      // Dystans oczekiwania dla reszty armii


    [Header("Typ Przeciwnika i Obrażenia")]
    [SerializeField] private EnemyType enemyType = EnemyType.Guard;
    [SerializeField] private float minDamage = 2f;
    [SerializeField] private float maxDamage = 10f;

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


    private bool isDead = false;
    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (isDead) return;

        health -= damage;
        Debug.Log($"Strażnik otrzymał {damage} obrażeń! Pozostało HP: {health}");

        // Powrót do kamery jako celu
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

        // 1. Wyłączenie logiki AI
        StopAllCoroutines();

        // 2. Wyłączenie poruszania się i walki
        if (agent != null) agent.enabled = false;
        if (weaponHitbox != null) weaponHitbox.DisableHitbox();

        // 3. Wyłączenie wszystkich colliderow
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
        }

        // 4. Uruchomienie animacji
        if (animator != null) animator.SetTrigger("Die");

        // 5. Usuwanie ciała ze sceny po 6 sekundach
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

        StopAllCoroutines();
        CurrentState = GuardState.Chasing;

        if (wasSleeping)
        {
            StartCoroutine(WakeUpAndChaseRoutine(target));
        }
        else
        {
            if (agent)
            {
                agent.enabled = true;
                agent.Warp(transform.position); 
                agent.isStopped = false;
            }

            StartCoroutine(ChaseRoutine(target));
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

    // Start animacji wstawania
    if (animator) animator.SetBool("isSleeping", false);

    // KROK A: Czekanie na przejście do animacji wstawania
    while (animator != null && (animator.IsInTransition(0) || !animator.GetCurrentAnimatorStateInfo(0).IsName("guard_standup")))
    {
        yield return null;
    }

    // KROK B: Czekanie na zakończenie animacji wstawania
    while (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("guard_standup"))
    {
        yield return null;
    }

    // Ustawienie pozycji na kotwicy łóżka i obrót o 180° na zewnątrz
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

        // 1. Dociągnięcie pozycji gracza do podłogi
        if (Physics.Raycast(cameraPos, Vector3.down, out RaycastHit hit, 20f))
        {
            feetPos = hit.point;
        }

        Vector3 targetNavMeshPos = feetPos;
        if (NavMesh.SamplePosition(feetPos, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
        {
            targetNavMeshPos = navHit.position;
        }

        bool isPrimaryAttacker = IsClosestChasingGuard(targetNavMeshPos);
        
        float currentTargetRange = isPrimaryAttacker ? attackRange : waitingRange;

        if (agent) agent.stoppingDistance = currentTargetRange - 0.5f;

        float distanceToPlayer = Vector3.Distance(transform.position, targetNavMeshPos);

        // 2. LOGIKA RUCHU I ATAKU / GOTOWOŚCI
        if (distanceToPlayer <= currentTargetRange)
        {
            // Osiągnięto docelową pozycję
            if (agent && agent.enabled)
            {
                agent.isStopped = true;
            }

            SetAnimSpeed(0f);

            // Każdy strażnik w zasięgu zawsze patrzy na gracza
            Vector3 lookDir = targetNavMeshPos - transform.position;
            lookDir.y = 0f;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
            }

            // Atakuje jedynie główny napastnik, tylko gdy jest w ścisłym zasięgu ataku
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
            // Podchodzenie (atakujący do 3.5m, reszta 7m)
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

        transform.SetPositionAndRotation(post.Position.position, post.Position.rotation);
        if (agent)
        {
            agent.enabled = true;
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

        StopAllCoroutines();
        StartCoroutine(WakeUpAndGoRoutine(triggerDistance));
    }

    private IEnumerator WakeUpAndGoRoutine(float triggerDistance)
    {
        Vector3 bedPos = currentBed ? currentBed.sleepAnchor.position : transform.position;
        Quaternion bedRot = currentBed ? currentBed.sleepAnchor.rotation : transform.rotation;

        if (currentBed)
        {
            currentBed.IsOccupied = false;
            currentBed.IsReserved = false;
            currentBed = null;
        }

        // Start animacji wstawania
        if (animator) animator.SetBool("isSleeping", false);

        // KROK A: Czekanie na przejście do animacji wstawania
        while (animator != null && (animator.IsInTransition(0) || !animator.GetCurrentAnimatorStateInfo(0).IsName("guard_standup")))
        {
            yield return null;
        }

        // KROK B: Czekanie na zakończenie animacji wstawania
        while (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("guard_standup"))
        {
            yield return null;
        }

        // Ustawienie pozycji na kotwicy łóżka i obrót fizyczny Transform o 180° na zewnątrz
        transform.SetPositionAndRotation(bedPos, bedRot * Quaternion.Euler(0f, 180f, 0f));

        if (agent)
        {
            agent.enabled = true;
            agent.isStopped = false;
        }

        Vector3 startBedPos = transform.position;
        Vector3 finalPostPos = assignedPost.Position.position;

        // 1. Punkty magistrali na odcinku od Kwatery do Posterunku
        List<Vector3> splinePoints = GetSplinePathSegment(manager != null ? manager.SharedMainPath : null, startBedPos, finalPostPos, pathResolution);

        bool oldGuardRelieved = false;
        GuardAI oldGuard = assignedPost.currentGuard;

        // 2. FAZA MARSZU PO MAGISTRALI (Spline)
        for (int i = 0; i < splinePoints.Count; i++)
        {
            agent.SetDestination(splinePoints[i]);

            while (true)
            {
                UpdateAnimSpeed();

                // Przekazanie warty
                float distToFinalPost = Vector3.Distance(transform.position, finalPostPos);
                if (!oldGuardRelieved && distToFinalPost <= triggerDistance)
                {
                    oldGuardRelieved = true;
                    if (oldGuard != null && oldGuard.CurrentState == GuardState.OnDuty)
                    {
                        oldGuard.ReturnToQuarters();
                    }
                }

                // Osłona przejścia do kolejnego punktu na Splinie
                Vector3 flatAgentPos = new Vector3(transform.position.x, 0f, transform.position.z);
                Vector3 flatTargetPos = new Vector3(splinePoints[i].x, 0f, splinePoints[i].z);

                if (!agent.pathPending && Vector3.Distance(flatAgentPos, flatTargetPos) <= 0.6f)
                {
                    break;
                }

                yield return null;
            }
        }

        // 3. FAZA ZJAZDU ZE SPLINE
        agent.SetDestination(finalPostPos);

        while (true)
        {
            UpdateAnimSpeed();

            float distToFinalPost = Vector3.Distance(transform.position, finalPostPos);

            if (!oldGuardRelieved && distToFinalPost <= triggerDistance)
            {
                oldGuardRelieved = true;
                if (oldGuard != null && oldGuard.CurrentState == GuardState.OnDuty)
                {
                    oldGuard.ReturnToQuarters();
                }
            }

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f && distToFinalPost <= 1.5f)
            {
                break;
            }

            yield return null;
        }

        assignedPost.currentGuard = this;
        assignedPost.incomingGuard = null;
        CurrentState = GuardState.OnDuty;

        if (agent) agent.isStopped = true;
        transform.SetPositionAndRotation(finalPostPos, assignedPost.Position.rotation);
        SetAnimSpeed(0f);
    }

    // --- SEKWENCJA POWROTU DO KWATERY I ZAŚNIĘCIA ---

    public void ReturnToQuarters()
    {
        if (CurrentState == GuardState.Chasing || CurrentState == GuardState.Alerted) return;
        currentBed = manager.GetAndReserveFreeBed();
        if (currentBed == null) return;

        CurrentState = GuardState.WalkingToQuarters;

        if (agent)
        {
            agent.enabled = true;
            agent.isStopped = false;
        }

        StopAllCoroutines();
        StartCoroutine(ReturnToQuartersRoutine());
    }

    private IEnumerator ReturnToQuartersRoutine()
    {
        Vector3 startPostPos = transform.position;
        Vector3 bedPos = currentBed.sleepAnchor.position;

        // 1. Punkty magistrali na odcinku od Posterunku do Łóżka w Kwaterze
        List<Vector3> splinePoints = GetSplinePathSegment(manager != null ? manager.SharedMainPath : null, startPostPos, bedPos, pathResolution);

        // 2. FAZA MARSZU PO MAGISTRALI
        for (int i = 0; i < splinePoints.Count; i++)
        {
            agent.SetDestination(splinePoints[i]);

            while (true)
            {
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

        // 3. FAZA ZJAZDU Z MAGISTRALI DO ŁÓŻKA
        agent.SetDestination(bedPos);

        while (true)
        {
            UpdateAnimSpeed();

            float distToBed = Vector3.Distance(transform.position, bedPos);

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f && distToBed <= 1.5f)
            {
                break;
            }

            yield return null;
        }

        // 4. WEJŚCIE DO ŁÓŻKA I ZAŚNIĘCIE
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

    // 1. KNOT WEJŚCIOWY (NAJBLIŻSZY POZYCJI STARTOWEJ)
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

    // 2. KNOT WYJŚCIOWY (NAJBLIŻSZY POZYCJI KOŃCOWEJ)
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

    // 3. MAPOWANIE KNOTOW NA PUNKTY PRÓBKOWANIA SPLINE'A
    int startIndex = 0;
    int endIndex = 0;
    float minDistStartKnot = float.MaxValue;
    float minDistEndKnot = float.MaxValue;

    for (int i = 0; i < resolution; i++)
    {
        float t = (float)i / (resolution - 1);
        Vector3 worldPos = spline.EvaluatePosition(t);

        // Najbliższy punkt próbkowania dla Knota Wejściowego
        float distToStartKnot = Vector3.Distance(startKnotWorldPos, worldPos);
        if (distToStartKnot < minDistStartKnot)
        {
            minDistStartKnot = distToStartKnot;
            startIndex = i;
        }

        // Najbliższy punkt próbkowania dla Knota Wyjściowego
        float distToEndKnot = Vector3.Distance(endKnotWorldPos, worldPos);
        if (distToEndKnot < minDistEndKnot)
        {
            minDistEndKnot = distToEndKnot;
            endIndex = i;
        }
    }

    // 4. PUNKTY TRASY OD KNOTA WEJŚCIOWEGO DO KNOTA WYJŚCIOWEGO
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

    // 5. BOCZNY OFFSET (Z WYGASZANIEM NA WEJŚCIU I WYJŚCIU)
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
            float speed = agent.velocity.magnitude / agent.speed;
            animator.SetFloat("Speed", speed, 0.15f, Time.deltaTime);
        }
    }

    private void SetAnimSpeed(float speed)
    {
        if (animator) animator.SetFloat("Speed", speed);
    }
}