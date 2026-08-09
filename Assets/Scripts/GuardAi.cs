using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public enum GuardState { Sleeping, WalkingToPost, OnDuty, WalkingToQuarters }

public class GuardAI : MonoBehaviour
{
    [Header("Komponenty")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    public GuardState CurrentState { get; private set; } = GuardState.Sleeping;

    private CastleShiftManager manager;
    private GuardPost assignedPost;
    private Bed currentBed;

    private void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();
    }

    // --- INICJALIZACJA ---

    public void InitDuty(GuardPost post, CastleShiftManager shiftManager)
    {
        manager = shiftManager;
        assignedPost = post;
        assignedPost.currentGuard = this;
        CurrentState = GuardState.OnDuty;

        transform.SetPositionAndRotation(post.Position.position, post.Position.rotation);
        if (agent) 
        { 
            agent.enabled = true; 
            agent.isStopped = true; 
        }
        SetAnimSpeed(0f);
    }

    public void InitSleeping(Bed bed, CastleShiftManager shiftManager)
    {
        manager = shiftManager;
        currentBed = bed;
        currentBed.IsOccupied = true;
        CurrentState = GuardState.Sleeping;

        // Czysta pozycja i rotacja dokładnie z punktu sleepAnchor
        transform.SetPositionAndRotation(bed.sleepAnchor.position, bed.sleepAnchor.rotation);
        if (agent) agent.enabled = false;

        SetAnimSpeed(0f);
        if (animator)
        {
            animator.SetBool("isSleeping", true);
            animator.Play("guard_sleeping_loop", 0, 0f);
            animator.Update(0f);
        }
    }

    // --- SEKWENCJA WSTAWANIA I MARSZU NA POSTERUNEK ---

    public void WakeUpAndGoToPost(GuardPost targetPost, float triggerDistance)
    {
        if (currentBed)
        {
            currentBed.IsOccupied = false;
            currentBed.IsReserved = false;
            currentBed = null;
        }

        assignedPost = targetPost;
        CurrentState = GuardState.WalkingToPost;

        StopAllCoroutines();
        StartCoroutine(WakeUpAndGoRoutine(triggerDistance));
    }

    private IEnumerator WakeUpAndGoRoutine(float triggerDistance)
{
    // Pozycja i rotacja łóżka przed wyzerowaniem pola currentBed
    Vector3 bedPos = currentBed ? currentBed.sleepAnchor.position : transform.position;
    Quaternion bedRot = currentBed ? currentBed.sleepAnchor.rotation : transform.rotation;

    if (currentBed)
    {
        currentBed.IsOccupied = false;
        currentBed.IsReserved = false;
        currentBed = null;
    }

    //Start animacji wstawania
    if (animator) animator.SetBool("isSleeping", false);

    // KROK A
    while (animator != null && (animator.IsInTransition(0) || !animator.GetCurrentAnimatorStateInfo(0).IsName("guard_standup")))
    {
        yield return null;
    }

    // KROK B
    while (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("guard_standup"))
    {
        yield return null;
    }

    // Ustawienie pozycji na kotwicy łóżka i obrot fizyczny Transform o 180° na zewnątrz
    transform.SetPositionAndRotation(bedPos, bedRot * Quaternion.Euler(0f, 180f, 0f));

    if (agent) 
    { 
        agent.enabled = true; 
        agent.isStopped = false; 
    }

    Vector3 targetPos = assignedPost.Position.position;
    agent.SetDestination(targetPos);

    bool oldGuardRelieved = false;
    GuardAI oldGuard = assignedPost.currentGuard;

    while (true)
    {
        UpdateAnimSpeed();

        float distToTarget = Vector3.Distance(transform.position, targetPos);

        if (!oldGuardRelieved && distToTarget <= triggerDistance)
        {
            oldGuardRelieved = true;
            if (oldGuard != null && oldGuard.CurrentState == GuardState.OnDuty)
            {
                oldGuard.ReturnToQuarters();
            }
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f && distToTarget <= 1.5f)
        {
            break;
        }

        yield return null;
    }

    assignedPost.currentGuard = this;
    assignedPost.incomingGuard = null;
    CurrentState = GuardState.OnDuty;

    if (agent) agent.isStopped = true;
    transform.SetPositionAndRotation(targetPos, assignedPost.Position.rotation);
    SetAnimSpeed(0f);
}

    // --- SEKWENCJA POWROTU DO KWATERY I ZAŚNIĘCIA ---

    public void ReturnToQuarters()
    {
        currentBed = manager.GetAndReserveFreeBed();
        if (currentBed == null) return;

        CurrentState = GuardState.WalkingToQuarters;

        if (agent) 
        { 
            agent.enabled = true; 
            agent.isStopped = false; 
        }

        StopAllCoroutines();
        StartCoroutine(ReturnToQuartersRoutine());
    }

    private IEnumerator ReturnToQuartersRoutine()
    {
        Vector3 bedPos = currentBed.sleepAnchor.position;
        agent.SetDestination(bedPos);

        while (true)
        {
            UpdateAnimSpeed();

            float distToBed = Vector3.Distance(transform.position, bedPos);

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f && distToBed <= 1.5f)
            {
                break;
            }

            yield return null;
        }

        if (agent) 
        { 
            agent.isStopped = true; 
            agent.enabled = false; 
        }

        //oryginalny układ łóżka
        transform.SetPositionAndRotation(bedPos, currentBed.sleepAnchor.rotation);

        currentBed.IsOccupied = true;
        currentBed.IsReserved = false;
        CurrentState = GuardState.Sleeping;

        SetAnimSpeed(0f);
        if (animator) animator.SetBool("isSleeping", true);
    }

    // --- FUNKCJE POMOCNICZE ---

    private void UpdateAnimSpeed()
    {
        if (animator && agent && agent.enabled)
        {
            float speed = agent.velocity.magnitude / agent.speed;
            animator.SetFloat("Speed", speed, 0.15f, Time.deltaTime);
        }
    }

    private void SetAnimSpeed(float speed)
    {
        if (animator) animator.SetFloat("Speed", speed);
    }
}