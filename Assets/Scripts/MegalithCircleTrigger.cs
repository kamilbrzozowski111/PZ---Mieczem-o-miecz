using UnityEngine;

public class MegalithCircleTrigger : MonoBehaviour
{
    [SerializeField] private DogmanAI dogman;

    private void OnTriggerEnter(Collider other){
        if (other.GetComponentInParent<PlayerHealth>() != null){
            Transform playerTransform = PlayerTargetProvider.GetPlayerTransform();
            if (playerTransform == null) playerTransform = other.transform;

            if (dogman != null){
                dogman.TriggerCombat(playerTransform);
            }
        }
    }
}