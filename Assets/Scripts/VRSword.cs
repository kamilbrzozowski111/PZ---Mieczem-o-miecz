using UnityEngine;
using Autohand;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Grabbable))]
public class VRSword : MonoBehaviour
{
    [Header("Ustawienia Walki")]
    [SerializeField] private float baseDamage = 20f;
    [SerializeField] private float minSwingVelocity = 1.8f; // Minimalna prędkość zamachu
    [SerializeField] private float hitCooldown = 0.2f;       // Czas odnowienia między uderzeniami

    [Header("Efekty (Opcjonalnie)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitSound;

    private Rigidbody rb;
    private Grabbable grabbable;
    private float lastHitTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Time.time < lastHitTime + hitCooldown) return;

        // Sprawdzamy prędkość fizyczną samego miecza
        float swingSpeed = rb.linearVelocity.magnitude;

        // Jeśli miecz jest trzymany, sprawdzamy też prędkość Rigidbody dłoni
        if (grabbable != null && grabbable.IsHeld())
        {
            foreach (var hand in grabbable.heldBy)
            {
                if (hand != null)
                {
                    Rigidbody handRb = hand.GetComponent<Rigidbody>();
                    if (handRb != null)
                    {
                        float handSpeed = handRb.linearVelocity.magnitude;
                        swingSpeed = Mathf.Max(swingSpeed, handSpeed);
                    }
                }
            }
        }

        // Jeśli prędkość zamachu przekracza próg minimalny
        if (swingSpeed >= minSwingVelocity)
        {
            IDamageable target = collision.gameObject.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                ContactPoint contact = collision.contacts[0];
                float damageMultiplier = Mathf.Clamp(swingSpeed / minSwingVelocity, 1f, 2.5f);
                float finalDamage = baseDamage * damageMultiplier;

                target.TakeDamage(finalDamage, contact.point, contact.normal);
                lastHitTime = Time.time;

                if (audioSource != null && hitSound != null)
                {
                    audioSource.PlayOneShot(hitSound);
                }
            }
        }
    }
}