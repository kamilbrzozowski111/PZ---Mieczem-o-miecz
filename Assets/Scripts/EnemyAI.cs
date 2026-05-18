using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState { Approach, Duel, Spectate }

    [HideInInspector]
    public EnemyPreset activePreset;

    public AIState currentState = AIState.Approach;
    public Transform playerTransform;

    private NavMeshAgent _agent;
    private Animator _animator;
    private int _currentEnergy;
    private bool _isProcessingSequence = false;
    private float _currentOrbitAngle = 0f;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();

        // FIX: Prevent the NavMeshAgent from automatically snapping the enemy's 
        // face forward along the movement path. We will handle rotation manually.
        _agent.updateRotation = false;
    }

    public void Initialize(EnemyPreset preset)
    {
        activePreset = preset;
        _currentEnergy = activePreset.energyPoints;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("FakePlayer");
            if (player != null) playerTransform = player.transform;
            else Debug.LogError($"[{gameObject.name}] couldn't find 'FakePlayer'!");
        }

        _agent.speed = activePreset.moveSpeed;

        SetupWeapon();
        LookAtPlayer();
    }

    private void Update()
    {
        if (playerTransform == null || activePreset == null) return;

        switch (currentState)
        {
            case AIState.Approach:
                HandleApproach();
                break;

            case AIState.Duel:
                HandleDuel();
                break;
        }
    }

    private void SetupWeapon()
    {
        Transform weaponContainer = FindChildByName(transform, "EnemyWeaponContainer");

        if (weaponContainer == null)
        {
            Debug.LogError($"[{gameObject.name}] Hierarchy Error: Could not find a child named 'EnemyWeaponContainer'!");
            return;
        }

        foreach (Transform child in weaponContainer)
        {
            Destroy(child.gameObject);
        }

        if (activePreset.meleeWeapon != null)
        {
            GameObject spawnedWeapon = Instantiate(activePreset.meleeWeapon, weaponContainer);
            spawnedWeapon.transform.localPosition = Vector3.zero;
            spawnedWeapon.transform.localRotation = Quaternion.identity;
            spawnedWeapon.transform.localScale = activePreset.meleeWeapon.transform.localScale;
        }
    }

    private Transform FindChildByName(Transform parent, string targetName)
    {
        if (parent.name == targetName) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildByName(parent.GetChild(i), targetName);
            if (result != null) return result;
        }

        return null;
    }

    private void HandleApproach()
    {
        _agent.SetDestination(playerTransform.position);
        _agent.isStopped = false;

        // FIX: Constantly forces the enemy to stay backward relative to the player 
        // while tracking them down across the NavMesh
        LookAtPlayer();

        if (Vector3.Distance(transform.position, playerTransform.position) <= activePreset.duelRange)
        {
            Debug.Log($"<color=cyan>[{gameObject.name}]</color> Entered Duel Range. Switching to Duel State.");

            currentState = AIState.Duel;
            _agent.isStopped = true;
            Vector3 dir = transform.position - playerTransform.position;
            _currentOrbitAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }
    }

    private void HandleDuel()
    {
        LookAtPlayer();

        if (!_isProcessingSequence)
        {
            StartCoroutine(DuelSequenceRoutine());
        }
    }

    private IEnumerator DuelSequenceRoutine()
    {
        _isProcessingSequence = true;

        SwitchToIdleState();

        float waitBefore = Random.Range(activePreset.minTimeBetweenSequences, activePreset.maxTimeBetweenSequences);
        Debug.Log($"<color=white>[{gameObject.name}]</color> Preparing sequence. Waiting {waitBefore:F2}s in Idle...");
        yield return new WaitForSeconds(waitBefore);

        int attackCount = Random.Range(activePreset.minAttackPerSequence, activePreset.maxAttackPerSequence + 1);

        for (int i = 0; i < attackCount; i++)
        {
            PerformAttack(i + 1, attackCount);

            yield return StartCoroutine(WaitForAttackToReachEnd());

            SwitchToIdleState();

            float waitBetween = Random.Range(activePreset.minTimeBetweenAttacks, activePreset.maxTimeBetweenAttacks);
            yield return new WaitForSeconds(waitBetween);
        }

        if (activePreset.isMobileInDuel)
        {
            yield return StartCoroutine(OrbitMovementRoutine());
            SwitchToIdleState();
        }

        _isProcessingSequence = false;
    }

    private IEnumerator OrbitMovementRoutine()
    {
        _agent.isStopped = false;
        _agent.speed = activePreset.duelMoveSpeed;

        Vector3 currentOffset = transform.position - playerTransform.position;
        currentOffset.y = 0;

        float degreeOffset = Random.Range(activePreset.minArcMovement, activePreset.maxArcMovement);
        Vector3 rotatedOffset = Quaternion.Euler(0, degreeOffset, 0) * currentOffset;
        Vector3 targetPos = playerTransform.position + (rotatedOffset.normalized * activePreset.duelRange);

        Debug.Log($"<color=yellow>[{gameObject.name}]</color> Orbiting player. Angle: {degreeOffset:F1}°");
        _agent.SetDestination(targetPos);

        while (_agent.pathPending || _agent.remainingDistance > 0.5f)
        {
            LookAtPlayer();
            yield return null;
        }

        _agent.isStopped = true;
        _agent.speed = activePreset.moveSpeed;
    }

    private void PerformAttack(int current, int total)
    {
        string weaponName = (activePreset.meleeWeapon != null) ? activePreset.meleeWeapon.name : "Debug Fists";

        if (activePreset.availableAttacks != null && activePreset.availableAttacks.Count > 0)
        {
            int randomIndex = Random.Range(0, activePreset.availableAttacks.Count);
            string triggerName = activePreset.availableAttacks[randomIndex].animatorTriggerName;

            if (_animator != null && !string.IsNullOrEmpty(triggerName))
            {
                _animator.SetTrigger(triggerName);
                Debug.Log($"<color=orange>[{gameObject.name}]</color> Performing Attack ({current}/{total}) using {weaponName} -> Triggering: <b>{triggerName}</b>");
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] Missing Animator component or trigger string is empty!");
            }
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] No available attacks found in the active preset!");
        }
    }

    private IEnumerator WaitForAttackToReachEnd()
    {
        if (_animator == null) yield break;

        yield return null;
        yield return null;

        while (_animator.IsInTransition(0))
        {
            yield return null;
        }

        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

        while (stateInfo.normalizedTime < 0.95f && !stateInfo.IsName("Idle"))
        {
            stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            yield return null;
        }
    }

    private void SwitchToIdleState()
    {
        if (_animator != null)
        {
            _animator.CrossFade("Idle", 0.15f);
        }
    }

    public void OnWeaponHit()
    {
        _currentEnergy--;
        Debug.Log($"<color=red>[{gameObject.name}]</color> Hit! Energy: {_currentEnergy}");

        if (_currentEnergy <= 0)
        {
            Debug.Log($"<color=black>[{gameObject.name}]</color> Energy depleted.");
        }
    }

    private void LookAtPlayer()
    {
        if (playerTransform == null) return;

        Vector3 direction = (transform.position - playerTransform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}