using UnityEngine;

public class GuardPost : MonoBehaviour
{
    public string postName = "Brama";
    public Transform standPoint;
    
    [HideInInspector] public GuardAI currentGuard;  // Strażnik stojący na posterunku
    [HideInInspector] public GuardAI incomingGuard; // Strażnik będący W DRODZE na ten posterunek

    // Posterunek jest zablokowany dla nowych zmian, jeśli ktoś już na niego idzie
    public bool IsTargeted => incomingGuard != null;

    public Transform Position => standPoint != null ? standPoint : transform;
}