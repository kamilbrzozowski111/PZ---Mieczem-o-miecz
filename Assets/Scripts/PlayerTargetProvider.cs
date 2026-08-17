using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Dedykowany dostawca celu gracza dla systemów AI i interakcji w scenie.
/// Centralizuje pobieranie transformacji kamery VR oraz rzutowanie pozycji gracza na NavMesh.
/// </summary>
public class PlayerTargetProvider : MonoBehaviour
{
    private static PlayerTargetProvider _instance;
    public static PlayerTargetProvider Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<PlayerTargetProvider>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Ustawienia Detekcji Podłoża")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float raycastDownDistance = 20f;
    [SerializeField] private float navMeshSampleRadius = 4f;

    private Transform cachedPlayerTransform;
    private Camera cachedMainCamera;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (groundLayer.value == ~0 || groundLayer.value == 0)
        {
            int groundMask = LayerMask.GetMask("Ground");
            if (groundMask != 0) groundLayer = groundMask;
        }
    }

    /// <summary>
    /// Zwraca referencję do Transform gracza (głównej kamery VR).
    /// </summary>
    public static Transform GetPlayerTransform()
    {
        if (Instance != null && Instance.cachedPlayerTransform != null)
        {
            return Instance.cachedPlayerTransform;
        }

        if (Camera.main != null)
        {
            if (Instance != null)
            {
                Instance.cachedMainCamera = Camera.main;
                Instance.cachedPlayerTransform = Camera.main.transform;
            }
            return Camera.main.transform;
        }

        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            return playerHealth.transform;
        }

        return null;
    }

    /// <summary>
    /// Oblicza pozycję stóp gracza na NavMesh na podstawie rzutu pionowego w dół.
    /// </summary>
    public static bool TryGetPlayerNavMeshPosition(out Vector3 navMeshPos)
    {
        Transform player = GetPlayerTransform();
        if (player == null)
        {
            navMeshPos = Vector3.zero;
            return false;
        }

        return SampleNavMeshPosition(player.position, out navMeshPos);
    }

    /// <summary>
    /// Próbkuje pozycję docelową w świecie i rzutuje ją na podłoże NavMesh.
    /// </summary>
    public static bool SampleNavMeshPosition(Vector3 sourceWorldPos, out Vector3 navMeshPos)
    {
        Vector3 feetPos = sourceWorldPos;
        LayerMask mask = (Instance != null) ? Instance.groundLayer : LayerMask.GetMask("Ground");
        float rayDist = (Instance != null) ? Instance.raycastDownDistance : 20f;
        float sampleRadius = (Instance != null) ? Instance.navMeshSampleRadius : 4f;

        if (mask != 0 && Physics.Raycast(sourceWorldPos, Vector3.down, out RaycastHit hit, rayDist, mask))
        {
            feetPos = hit.point;
        }

        if (NavMesh.SamplePosition(feetPos, out NavMeshHit navHit, sampleRadius, NavMesh.AllAreas))
        {
            navMeshPos = navHit.position;
            return true;
        }

        navMeshPos = feetPos;
        return false;
    }
}
