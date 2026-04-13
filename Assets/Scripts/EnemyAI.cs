using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState { Approach, Duel, Spectate }

    [HideInInspector]
    public EnemyPreset activePreset;

    public AIState currentState = AIState.Approach;
    public Transform playerTransform;
    private NavMeshAgent _agent;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    public void Initialize(EnemyPreset preset)
    {
        activePreset = preset;

        // FIX: Find the player in the scene since prefabs lose scene references
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("FakePlayer");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                Debug.LogError($"Enemy spawned but couldn't find an object with the tag 'FakePlayer'!");
            }
        }

        // Apply stats to NavMesh
        _agent.speed = activePreset.moveSpeed;
        _agent.angularSpeed = activePreset.angularSpeed;
        _agent.acceleration = activePreset.acceleration;

        LookAtPlayer();
    }

    private void Update()
    {
        // If either of these is null, the enemy will do nothing.
        if (playerTransform == null || activePreset == null) return;

        switch (currentState)
        {
            case AIState.Approach:
                _agent.SetDestination(playerTransform.position);
                _agent.isStopped = false; // Ensure agent isn't paused

                if (Vector3.Distance(transform.position, playerTransform.position) <= activePreset.duelRange)
                {
                    currentState = AIState.Duel;
                }
                break;

            case AIState.Duel:
                _agent.isStopped = true;
                LookAtPlayer();
                break;
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