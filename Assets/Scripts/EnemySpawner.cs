using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class EnemySpawner : MonoBehaviour
{
    [Header("Prefabs & Data")]
    public GameObject enemyPrefab;
    public EnemyPreset[] possiblePresets; // Set these in the Inspector per Spawner

    public Transform[] spawnPoints;
    private bool _hasSpawned = false;

    private void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_hasSpawned && other.CompareTag("FakePlayer"))
        {
            SpawnEnemy();
            _hasSpawned = true;
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null || spawnPoints.Length == 0 || possiblePresets.Length == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] Spawner missing references! Check prefab, spawn points, or presets.");
            return;
        }

        // Pick random point and random preset
        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        EnemyPreset chosenPreset = possiblePresets[Random.Range(0, possiblePresets.Length)];

        // Spawn
        GameObject newEnemy = Instantiate(enemyPrefab, point.position, point.rotation);
        newEnemy.SetActive(true);

        // FIX: Look deep inside the hierarchy if EnemyAI isn't explicitly on the root object
        EnemyAI ai = newEnemy.GetComponentInChildren<EnemyAI>();

        if (ai != null)
        {
            // Pass the data down to handle weapon spawning and stat configuration
            ai.Initialize(chosenPreset);
            Debug.Log($"<color=green>[{gameObject.name}]</color> Successfully spawned enemy initialized with preset: <b>{chosenPreset.name}</b>");
        }
        else
        {
            Debug.LogError($"<color=red>[{gameObject.name}]</color> Spawned enemy prefab, but could not find the <b>EnemyAI</b> component on it or its children!");
        }
    }

    private void OnDrawGizmos()
    {
        if (spawnPoints == null) return;
        Gizmos.color = Color.red;
        foreach (Transform point in spawnPoints)
        {
            if (point != null)
            {
                Gizmos.DrawSphere(point.position, 0.1f);
                Gizmos.DrawLine(transform.position, point.position);
            }
        }
    }
}