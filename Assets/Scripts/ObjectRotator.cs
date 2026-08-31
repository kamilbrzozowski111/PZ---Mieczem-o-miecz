using UnityEngine;

public class ObjectRotator : MonoBehaviour{
    [Header("Ustawienia Obrotu")]
    [Tooltip("Oś obrotu (domyślnie Vector3.up, czyli oś Y)")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    
    [Tooltip("Prędkość obrotu w stopniach na sekundę")]
    [SerializeField] private float rotationSpeed = 50f;
    
    [Tooltip("Space.Self = obrót względem własnej osi, Space.World = obrót względem świata")]
    [SerializeField] private Space rotationSpace = Space.Self;

    [Header("Stan")]
    [SerializeField] private bool isRotating = true;

    public bool IsRotating{
        get => isRotating;
        set => isRotating = value;
    }

    private void Update(){
        if (!isRotating) return;
        transform.Rotate(rotationAxis * (rotationSpeed * Time.deltaTime), rotationSpace);
    }


    public void EnableRotation(){
        isRotating = true;
    }

    public void DisableRotation(){
        isRotating = false;
    }

    public void ToggleRotation(){
        isRotating = !isRotating;
    }
}