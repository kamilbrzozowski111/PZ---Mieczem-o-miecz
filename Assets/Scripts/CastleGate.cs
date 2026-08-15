using System.Collections;
using UnityEngine;

public class CastleGate : MonoBehaviour
{
    [Header("Ustawienia Bramy")]
    [SerializeField] private Transform gateTransform;
    [SerializeField] private float openAngle = 92f;
    [SerializeField] private float openSpeed = 0.5f;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    public bool IsOpen { get; private set; } = false;
    public bool IsOpening { get; private set; } = false;

    public IEnumerator OpenGateRoutine()
    {
        if (IsOpen || IsOpening) yield break;

        IsOpening = true;
        Transform target = gateTransform != null ? gateTransform : transform;
        
        Quaternion startRotation = target.localRotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(rotationAxis * openAngle);

        float progress = 0f;
        while (progress < 1f)
        {
            progress += Time.deltaTime * openSpeed;
            target.localRotation = Quaternion.Slerp(startRotation, targetRotation, progress);
            yield return null;
        }

        IsOpen = true;
        IsOpening = false;
    }
}