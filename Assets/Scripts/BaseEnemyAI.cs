using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Abstrakcyjna klasa bazowa dla wszystkich przeciwników sceny.
/// </summary>
public abstract class BaseEnemyAI : MonoBehaviour, IDamageable
{
    [Header("Komponenty Bazowe")]
    [SerializeField] protected NavMeshAgent agent;
    [SerializeField] protected Animator animator;
    [SerializeField] protected EnemyHitbox weaponHitbox;

    [Header("Typ Przeciwnika i Obrażenia")]
    [SerializeField] protected EnemyType enemyType = EnemyType.Guard;
    [SerializeField] protected float minDamage = 2f;
    [SerializeField] protected float maxDamage = 10f;

    [Header("Statystyki Walki")]
    [SerializeField] protected float health = 100f;
    [SerializeField] protected float attackRange = 3.5f;
    [SerializeField] protected float attackCooldown = 2.0f;
    [SerializeField] protected float chaseSpeed = 7f;

    public bool isDead { get; protected set; } = false;
    public float Health => health;
    public EnemyType Type => enemyType;

    protected float lastAttackTime = -10f;
    protected Coroutine activeHitboxCoroutine;

    protected virtual void Awake(){
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();
        if (!weaponHitbox) weaponHitbox = GetComponentInChildren<EnemyHitbox>();
    }

    protected virtual void OnValidate(){
        switch (enemyType){
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

    /// <summary>
    /// Implementacja interfejsu IDamageable - redukcja życia i obsługa śmierci.
    /// </summary>
    public virtual void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal){
        if (isDead) return;

        health -= damage;
        OnDamaged(damage, hitPoint, hitNormal);

        if (health <= 0f){
            Die();
        }
    }

    /// <summary>
    /// Główna procedura zgonu przeciwnika.
    /// </summary>
    protected virtual void Die(){
        if (isDead) return;
        isDead = true;

        StopAllCoroutines();

        if (agent != null) agent.enabled = false;
        if (weaponHitbox != null) weaponHitbox.DisableHitbox();

        foreach (Collider c in GetComponentsInChildren<Collider>()){
            c.enabled = false;
        }

        if (animator != null){
            animator.SetTrigger("Die");
        }

        OnDeath();
    }

    /// <summary>
    /// Wirtualny punkt rozszerzenia wywoływany po otrzymaniu obrażeń.
    /// </summary>
    protected virtual void OnDamaged(float damage, Vector3 hitPoint, Vector3 hitNormal) { }

    /// <summary>
    /// Wirtualny punkt rozszerzenia wywoływany w momencie śmierci jednostki.
    /// </summary>
    protected virtual void OnDeath(){
        Destroy(gameObject, 6.0f);
    }

    /// <summary>
    /// Aktualizuje parametr prędkości w Animatorze na podstawie prędkości NavMeshAgenta.
    /// </summary>
    protected void UpdateAnimSpeed(){
        if (animator && agent && agent.enabled){
            float currentSpeed = agent.velocity.magnitude;
            animator.SetFloat("Speed", currentSpeed, 0.15f, Time.deltaTime);
        }
    }

    /// <summary>
    /// Ustawia sztywną wartość parametru prędkości w Animatorze.
    /// </summary>
    protected void SetAnimSpeed(float speed){
        if (animator){
            animator.SetFloat("Speed", speed);
        }
    }

    /// <summary>
    /// Obraca postać w osi Y w stronę wyznaczonego punktu w przestrzeni.
    /// </summary>
    protected void FaceTarget(Vector3 targetWorldPos, float turnSpeed = 10f){
        Vector3 lookDir = targetWorldPos - transform.position;
        lookDir.y = 0f;
        if (lookDir != Vector3.zero){
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * turnSpeed);
        }
    }

    /// <summary>
    /// Pobiera pozycję celu na NavMesh za pomocą PlayerTargetProvider.
    /// </summary>
    protected bool TryGetTargetNavMeshPosition(Transform target, out Vector3 targetNavMeshPos){
        if (target == null){
            targetNavMeshPos = Vector3.zero;
            return false;
        }

        return PlayerTargetProvider.SampleNavMeshPosition(target.position, out targetNavMeshPos);
    }

    /// <summary>
    /// Wykonuje bazowy atak animacyjny i aktywuje hitbox na określony czas z wylosowanymi obrażeniami.
    /// </summary>
    protected virtual void PerformBaseAttack(float enableDelay = 0.2f, float disableDelay = 1.4f){
        if (animator) animator.SetTrigger("Attack");

        if (activeHitboxCoroutine != null){
            StopCoroutine(activeHitboxCoroutine);
        }
        activeHitboxCoroutine = StartCoroutine(AttackHitboxRoutine(enableDelay, disableDelay));
    }

    /// <summary>
    /// Procedura włączania i wyłączania hitboxa broni z kalkulacją losowych obrażeń.
    /// </summary>
    protected virtual IEnumerator AttackHitboxRoutine(float enableDelay = 0.2f, float disableDelay = 1.4f){
        float calculatedDamage = Random.Range(minDamage, maxDamage);

        if (enableDelay > 0f){
            yield return new WaitForSeconds(enableDelay);
        }

        if (weaponHitbox != null){
            weaponHitbox.EnableHitbox(calculatedDamage);
        }

        if (disableDelay > 0f){
            yield return new WaitForSeconds(disableDelay);
        }

        if (weaponHitbox != null){
            weaponHitbox.DisableHitbox();
        }

        activeHitboxCoroutine = null;
    }
}
