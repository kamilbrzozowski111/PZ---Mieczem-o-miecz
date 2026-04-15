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
    private int _currentEnergy;
    private bool _isProcessingSequence = false;
    private float _currentOrbitAngle = 0f;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
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

    private void HandleApproach()
    {
        _agent.SetDestination(playerTransform.position);
        _agent.isStopped = false;

        if (Vector3.Distance(transform.position, playerTransform.position) <= activePreset.duelRange)
        {
            // NEW LOG: Enter Duel State
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

        // 1. Wait randomized time
        float waitBefore = Random.Range(activePreset.minTimeBetweenSequences, activePreset.maxTimeBetweenSequences);

        // NEW LOG: Preparing Sequence
        Debug.Log($"<color=white>[{gameObject.name}]</color> Preparing sequence. Waiting {waitBefore:F2}s...");

        yield return new WaitForSeconds(waitBefore);

        // 2. Execute randomized amount of attacks
        int attackCount = Random.Range(activePreset.minAttackPerSequence, activePreset.maxAttackPerSequence + 1);

        for (int i = 0; i < attackCount; i++)
        {
            // UPDATED LOG: Now includes "Attack X of Y"
            PerformAttack(i + 1, attackCount);

            float waitBetween = Random.Range(activePreset.minTimeBetweenAttacks, activePreset.maxTimeBetweenAttacks);
            yield return new WaitForSeconds(waitBetween);
        }

        // 3. Orbit movement
        if (activePreset.isMobileInDuel)
        {
            yield return StartCoroutine(OrbitMovementRoutine());
        }

        _isProcessingSequence = false;
    }

    private IEnumerator OrbitMovementRoutine()
    {
        _agent.isStopped = false;
        _agent.speed = activePreset.duelMoveSpeed;

        // 1. Calculate the current vector from the player to the enemy
        Vector3 currentOffset = transform.position - playerTransform.position;
        currentOffset.y = 0; // Keep movement on the horizontal plane

        // 2. Determine the rotation amount
        float degreeOffset = Random.Range(activePreset.minArcMovement, activePreset.maxArcMovement);

        // 3. Rotate the offset vector using a Quaternion
        Vector3 rotatedOffset = Quaternion.Euler(0, degreeOffset, 0) * currentOffset;

        // 4. Set target position (Player pos + the newly rotated vector)
        // We normalize and multiply by duelRange to ensure they don't drift inward/outward
        Vector3 targetPos = playerTransform.position + (rotatedOffset.normalized * activePreset.duelRange);

        Debug.Log($"<color=yellow>[{gameObject.name}]</color> Orbiting player. Angle: {degreeOffset:F1}°");

        _agent.SetDestination(targetPos);

        // 5. Wait for arrival
        while (_agent.pathPending || _agent.remainingDistance > 0.5f)
        {
            // Optional: Keep looking at player while moving
            LookAtPlayer();
            yield return null;
        }

        _agent.isStopped = true;
        _agent.speed = activePreset.moveSpeed;
    }

    private void PerformAttack(int current, int total)
    {
        string weaponName = (activePreset.meleeWeapon != null) ? activePreset.meleeWeapon.name : "Debug Fists";

        // UPDATED LOG: Clearly shows sequence progress
        Debug.Log($"<color=orange>[{gameObject.name}]</color> Performing Attack ({current}/{total}) using {weaponName}");
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
        Vector3 direction = (playerTransform.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);
    }
}