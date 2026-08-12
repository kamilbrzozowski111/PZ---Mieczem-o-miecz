using UnityEngine;
using UnityEngine.Splines;

public class GuardPost : MonoBehaviour
{
    public string postName = "Brama";
    public Transform standPoint;
    [SerializeField] private SplineContainer pathFromQuarters;
    public SplineContainer PathFromQuarters => pathFromQuarters;
    
    [HideInInspector] public GuardAI currentGuard;  // Strażnik stojący na posterunku
    [HideInInspector] public GuardAI incomingGuard; // Strażnik będący w drodze na ten posterunek

    // Posterunek jest zablokowany dla nowych zmian, jeśli ktoś już na niego idzie
    public bool IsTargeted => incomingGuard != null;

    public Transform Position => standPoint != null ? standPoint : transform;
}