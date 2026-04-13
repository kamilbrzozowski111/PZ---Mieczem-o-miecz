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
        if (enemyPrefab == null || spawnPoints.Length == 0 || possiblePresets.Length == 0) return;

        // Pick random point and random preset
        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        EnemyPreset chosenPreset = possiblePresets[Random.Range(0, possiblePresets.Length)];

        // Spawn
        GameObject newEnemy = Instantiate(enemyPrefab, point.position, point.rotation);
        newEnemy.SetActive(true);

        // Inject the preset into the AI
        EnemyAI ai = newEnemy.GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.Initialize(chosenPreset);
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