using UnityEngine;

public class EnemyHitbox : MonoBehaviour
{
    private float currentDamage;
    private bool isActive = false;

    public void EnableHitbox(float damage)
    {
        currentDamage = damage;
        isActive = true;
        Debug.Log("🟢 HITBOX AKTYWNY");
    }

    public void DisableHitbox()
    {
        isActive = false;
        Debug.Log("🔴 HITBOX WYŁĄCZONY");

    }

private void OnTriggerEnter(Collider other)
{
    if (!isActive) return;

    IDamageable target = other.GetComponentInParent<IDamageable>();

    if (target != null && target is PlayerHealth)
    {
        target.TakeDamage(currentDamage, transform.position, -transform.forward);
        isActive = false; 
    }
}

}