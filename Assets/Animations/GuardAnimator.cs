using UnityEngine;

public class GuardAnimator : MonoBehaviour
{
    private Animator animator;

    [SerializeField] private float speedDampTime = 0.15f; 

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void SetSpeed(float targetSpeed)
    {

        animator.SetFloat("Speed", targetSpeed, speedDampTime, Time.deltaTime);
    }


    public void PressButton()
    {
        animator.SetTrigger("PressButton");
    }


    public void Attack()
    {
        animator.SetTrigger("Attack");
    }

    public void ToggleSit(bool sit)
    {
        animator.SetBool("IsSitting", sit);
    }

    public void ToggleSleep(bool sleep)
    {
        animator.SetBool("IsSleeping", sleep);
    }
}