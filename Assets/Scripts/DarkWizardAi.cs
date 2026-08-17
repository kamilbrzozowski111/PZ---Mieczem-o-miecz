using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum DarkWizardState { Idle, Chasing, Dead }

public class DarkWizardAI : MonoBehaviour, IDamageable
{
    [Header("Komponenty")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyHitbox magicHitbox;

    [Header("Post Processing")]
    [SerializeField] private Volume globalVolume;

    [Header("Typ Przeciwnika i Obrażenia")]
    [SerializeField] private EnemyType enemyType = EnemyType.DarkMage;
    [SerializeField] private float minDamage = 15f;
    [SerializeField] private float maxDamage = 25f;

    [Header("Statystyki Walki")]
    [SerializeField] private float health = 250f;
    [SerializeField] private float detectionRadius = 42.0f;
    [SerializeField] private float attackRange = 3.5f;
    [SerializeField] private float attackCooldown = 2.0f;
    [SerializeField] private float chaseSpeed = 8.0f;

    [Header("Infrastruktura Zamku")]
    [SerializeField] private List<CastleGate> gatesToClose;

    public DarkWizardState CurrentState { get; private set; } = DarkWizardState.Idle;

    private float lastAttackTime;
    private bool isDead = false;
    private bool fightStarted = false;

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

    private void Awake(){
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();
    }

    private void Start()
    {
        if (agent != null){
            agent.speed = chaseSpeed;
            agent.isStopped = true;
        }

        if (gatesToClose == null || gatesToClose.Count == 0){
            gatesToClose = new List<CastleGate>(FindObjectsByType<CastleGate>(FindObjectsSortMode.None));
        }

        if (globalVolume == null)
        {
            globalVolume = FindFirstObjectByType<Volume>();
        }
    }

    private void Update()
    {
        if (isDead || fightStarted) return;

        Transform player = Camera.main != null ? Camera.main.transform : null;
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer <= detectionRadius)
            {
                StartBossFight(player);
            }
        }
    }

    // --- AKTYWACJA WALKI ---

    public void StartBossFight(Transform target)
    {
        if (isDead || fightStarted || target == null) return;

        fightStarted = true;
        CurrentState = DarkWizardState.Chasing;

        foreach (var gate in gatesToClose){
            if (gate != null){
                StartCoroutine(gate.CloseGateRoutine());
            }
        }

        NotificationManager.Show("Zły czarodziej cię dostrzegł! Bramy zamku zostały zamknięte!", NotificationType.Danger);

        if (agent != null){
            agent.enabled = true;
            agent.speed = chaseSpeed;
            agent.autoBraking = true;
            agent.isStopped = false;
        }

        StartCoroutine(ChaseRoutine(target));
    }

    // --- PĘTLA POŚCIGU I ATAKU ---

    private IEnumerator ChaseRoutine(Transform target)
    {
        while (CurrentState == DarkWizardState.Chasing && target != null)
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

            if (distanceToPlayer <= attackRange){
                if (agent && agent.enabled){
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
                if (agent && agent.enabled){
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

        yield return new WaitForSeconds(0.8f);

        if (magicHitbox != null){
            magicHitbox.EnableHitbox(calculatedDamage);
        }

        yield return new WaitForSeconds(1.4f);

        if (magicHitbox != null){
            magicHitbox.DisableHitbox();
        }
    }

    // --- METODY ANIMACJI ---

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

    // --- OBRAŻENIA I ŚMIERĆ ---

    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (isDead) return;

        health -= damage;

        Transform player = Camera.main != null ? Camera.main.transform : null;
        if (player != null && !fightStarted){
            StartBossFight(player);
        }

        if (health <= 0f){
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        CurrentState = DarkWizardState.Dead;

        StopAllCoroutines();

        if (agent != null) agent.enabled = false;
        if (magicHitbox != null) magicHitbox.DisableHitbox();

        foreach (Collider c in GetComponentsInChildren<Collider>()){
            c.enabled = false;
        }

        if (animator != null) animator.SetTrigger("Die");

        StartCoroutine(WinGameSequence());
    }

    // --- ZAKOŃCZENIE GRY ---

private IEnumerator WinGameSequence()
{
    // 1. Powiadomienie końcowe
    NotificationManager.Show("Gratulacje! Pokonałeś wszystkie przeciwności i przeszedłeś całą misję!", NotificationType.Info, 12.0f);

    // 2. Ukrycie HUD-a
    if (PlayerUIWidget.Instance != null){
        PlayerUIWidget.Instance.HideHUD();
    }

    yield return new WaitForSeconds(4.0f);

    Transform mainCam = Camera.main != null ? Camera.main.transform : null;
    CanvasGroup fadeCanvasGroup = null;

    if (mainCam != null){
        GameObject fadeObj = new GameObject("VR_WinFadeOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(UnityEngine.UI.Image));
        
        fadeObj.transform.SetParent(mainCam, false);
        fadeObj.transform.localPosition = new Vector3(0f, 0f, 1.1f);
        fadeObj.transform.localRotation = Quaternion.identity;

        Canvas fadeCanvas = fadeObj.GetComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.WorldSpace;

        RectTransform rect = fadeObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100f, 100f);

        UnityEngine.UI.Image fadeImage = fadeObj.GetComponent<UnityEngine.UI.Image>();
        fadeImage.color = Color.black;

        fadeCanvasGroup = fadeObj.GetComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
    }

    // Płynne ściemnianie sceny poprzez CanvasGroup.alpha
    float fadeDuration = 6.0f;
    float elapsed = 0f;

    while (elapsed < fadeDuration){
        elapsed += Time.deltaTime;
        if (fadeCanvasGroup != null){
            fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
        }
        yield return null;
    }

    if (fadeCanvasGroup != null){
        fadeCanvasGroup.alpha = 1f;
    }

    // 4. Zatrzymanie czasu i zniszczenie obiektu maga
    Time.timeScale = 0f;
    Destroy(gameObject);
}
}