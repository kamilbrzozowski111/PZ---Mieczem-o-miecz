using System.Collections;
using UnityEngine;
using Autohand;

public class MegalithPass : MonoBehaviour
{
    [Header("Komponenty")]
    [SerializeField] private ObjectRotator rotator;
    [SerializeField] private Grabbable grabbable;
    [SerializeField] private Collider passCollider;

    [Header("Obrot po odblokowaniu")]
    [SerializeField] private float targetYAngle = 90f;
    [SerializeField] private float rotationDuration = 1.0f;

    private bool isCollected = false;

    private void Awake()
    {
        if (grabbable != null) grabbable.enabled = false;
        if (passCollider != null) passCollider.enabled = false;
    }

    private void OnEnable()
    {
        if (grabbable != null)
        {
            grabbable.onGrab.AddListener(OnPassGrabbed);
        }
    }

    private void OnDisable()
    {
        if (grabbable != null)
        {
            grabbable.onGrab.RemoveListener(OnPassGrabbed);
        }
    }

    // Wywoływane po śmierci Dogmana
    public void UnlockPass()
    {
        // 1. Wyłączenie stałego obracania
        if (rotator != null)
        {
            rotator.DisableRotation();
        }

        // 2. Płynna rotacja do 90 stopni na osi Y
        StartCoroutine(RotateToTargetAngleRoutine());

        // 3. Odblokowanie chwytu VR
        if (grabbable != null) grabbable.enabled = true;
        if (passCollider != null) passCollider.enabled = true;

        // 4. Powiadomienie o sukcesie
        NotificationManager.Show("Gratulacje! Klucz do zamku znajduje się w samym środku kręgu!", NotificationType.Info, 5.0f);
    }

    private IEnumerator RotateToTargetAngleRoutine()
    {
        Quaternion startRot = transform.rotation;
        Vector3 currentEuler = transform.rotation.eulerAngles;
        Quaternion targetRot = Quaternion.Euler(currentEuler.x, targetYAngle, currentEuler.z);

        float elapsed = 0f;
        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(startRot, targetRot, elapsed / rotationDuration);
            yield return null;
        }
        transform.rotation = targetRot;
    }

    private void OnPassGrabbed(Hand hand, Grabbable grabbedObject)
    {
        if (isCollected) return;
        isCollected = true;

        StartCoroutine(CollectPassRoutine(hand));
    }

    private IEnumerator CollectPassRoutine(Hand hand)
    {
        yield return null;

        if (hand != null)
        {
            hand.Release();
        }

        // Ukrywanie przedmiotu i wyłączenie kolizji
        if (passCollider != null) passCollider.enabled = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.enabled = false;
        }

        // Aktualizacja UI
        PlayerUIWidget uiWidget = FindFirstObjectByType<PlayerUIWidget>();
        if (uiWidget != null)
        {
            uiWidget.UpdatePassStatus(true);
        }

        yield return null;

        gameObject.SetActive(false);
    }
}