using UnityEngine;

public class MegalithCircleTrigger : MonoBehaviour
{
    [SerializeField] private DogmanAI dogman;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerHealth>() != null)
        {
            Transform playerTransform = Camera.main != null ? Camera.main.transform : other.transform;
            if (dogman != null)
            {
                dogman.TriggerCombat(playerTransform);
            }
        }
    }
}