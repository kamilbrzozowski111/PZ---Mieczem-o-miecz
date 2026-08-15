using UnityEngine;
using UnityEngine.Splines;

public class GuardPost : MonoBehaviour
{
    public string postName = "Brama";
    public Transform standPoint;
    [SerializeField] private SplineContainer pathFromQuarters;
    public SplineContainer PathFromQuarters => pathFromQuarters;
    
    [HideInInspector] public GuardAI currentGuard;  // Strażnik stojący na posterunku
    [HideInInspector] public GuardAI incomingGuard;
    public bool IsTargeted => incomingGuard != null;

    public Transform Position => standPoint != null ? standPoint : transform;

    public Quaternion InitialRotation { get; private set; }

    private void Awake(){
        InitialRotation = Position.rotation;
    }
}