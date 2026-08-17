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

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Transform targetTransform;

    private void Awake()
    {
        targetTransform = gateTransform != null ? gateTransform : transform;
        closedRotation = targetTransform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(rotationAxis * openAngle);
    }

    public IEnumerator OpenGateRoutine()
    {
        if (IsOpen) yield break;

        Quaternion startRotation = targetTransform.localRotation;
        float progress = 0f;

        while (progress < 1f)
        {
            progress += Time.deltaTime * openSpeed;
            targetTransform.localRotation = Quaternion.Slerp(startRotation, openRotation, progress);
            yield return null;
        }

        targetTransform.localRotation = openRotation;
        IsOpen = true;
    }

    public IEnumerator CloseGateRoutine()
    {
        if (!IsOpen) yield break;

        Quaternion startRotation = targetTransform.localRotation;
        float progress = 0f;

        while (progress < 1f)
        {
            progress += Time.deltaTime * openSpeed;
            targetTransform.localRotation = Quaternion.Slerp(startRotation, closedRotation, progress);
            yield return null;
        }

        targetTransform.localRotation = closedRotation;
        IsOpen = false;
    }
}