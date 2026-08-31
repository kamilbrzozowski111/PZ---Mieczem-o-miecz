using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public enum DarkWizardState { Idle, Chasing, Dead }

public class DarkWizardAI : BaseEnemyAI{
    [Header("Postac")]
    [SerializeField] private EnemyHitbox magicHitbox;
    [SerializeField] private float detectionRadius = 42.0f;

    [Header("Infrastruktura Zamku")]
    [SerializeField] private List<CastleGate> gatesToClose;

    public DarkWizardState CurrentState { get; private set; } = DarkWizardState.Idle;

    public static event Action OnDarkWizardDefeated;

    private bool fightStarted = false;

    protected override void Awake(){
        base.Awake();
        if (weaponHitbox == null && magicHitbox != null){
            weaponHitbox = magicHitbox;
        }
    }

    private void Start(){
        if (agent != null){
            agent.speed = chaseSpeed;
            agent.isStopped = true;
        }

        if (gatesToClose == null || gatesToClose.Count == 0){
            gatesToClose = new List<CastleGate>(FindObjectsByType<CastleGate>(FindObjectsSortMode.None));
        }

    }

    private void Update(){
        if (isDead || fightStarted) return;

        Transform player = PlayerTargetProvider.GetPlayerTransform();
        if (player != null){
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer <= detectionRadius){
                StartBossFight(player);
            }
        }
    }

    // --- AKTYWACJA WALKI Z BOSSEM ---

    public void StartBossFight(Transform target){
        if (isDead || fightStarted || target == null) return;

        fightStarted = true;
        CurrentState = DarkWizardState.Chasing;

        foreach (var gate in gatesToClose){
            if (gate != null){
                StartCoroutine(gate.CloseGateRoutine());
            }
        }

        NotificationManager.Show("Zły czarodziej cię dostrzegł! Bramy zamku zostały zamknięte!", NotificationType.Danger);

        if (agent != null){
            agent.enabled = true;
            agent.speed = chaseSpeed;
            agent.autoBraking = true;
            agent.isStopped = false;
        }

        StartCoroutine(ChaseRoutine(target));
    }

    // --- PĘTLA POŚCIGU I ATAKU ---

    private IEnumerator ChaseRoutine(Transform target){
        while (CurrentState == DarkWizardState.Chasing && target != null && !isDead){
            if (!TryGetTargetNavMeshPosition(target, out Vector3 targetNavMeshPos)){
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            if (agent) agent.stoppingDistance = attackRange - 0.5f;

            float distanceToPlayer = Vector3.Distance(transform.position, targetNavMeshPos);

            if (distanceToPlayer <= attackRange){
                if (agent && agent.enabled){
                    agent.isStopped = true;
                }

                SetAnimSpeed(0f);
                FaceTarget(targetNavMeshPos);

                if (Time.time >= lastAttackTime + attackCooldown){
                    lastAttackTime = Time.time;
                    PerformBaseAttack(0.8f, 1.4f);
                }
            }
            else{
                if (agent && agent.enabled){
                    agent.isStopped = false;
                    agent.SetDestination(targetNavMeshPos);
                    UpdateAnimSpeed();
                }
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    protected override void OnDamaged(float damage, Vector3 hitPoint, Vector3 hitNormal){
        if (!fightStarted){
            Transform player = PlayerTargetProvider.GetPlayerTransform();
            if (player != null){
                StartBossFight(player);
            }
        }
    }

    protected override void OnDeath(){
        CurrentState = DarkWizardState.Dead;
        OnDarkWizardDefeated?.Invoke();
        GameEndController.TriggerVictorySequence();

        base.OnDeath();
    }
}