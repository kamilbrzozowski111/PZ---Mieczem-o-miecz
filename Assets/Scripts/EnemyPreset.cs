using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

[System.Serializable]
public class EnemyAttack
{
    [Tooltip("Alternatively, use this if you trigger animations by string name in an Animator Controller.")]
    public string animatorTriggerName;
}

[CreateAssetMenu(fileName = "NewEnemyPreset", menuName = "ScriptableObjects/EnemyPreset")]
public class EnemyPreset : ScriptableObject
{
    [Header("Movement")]
    public float moveSpeed = 3.5f;
    public bool isMobileInDuel = false;
    public float duelMoveSpeed = 2.0f;
    public int minArcMovement = 30;
    public int maxArcMovement = 30;

    [Header("Combat Stats")]
    public int energyPoints = 3;
    public float duelRange = 3f;
    public GameObject meleeWeapon;

    [Header("Attacks & Animations")]
    public List<EnemyAttack> availableAttacks = new List<EnemyAttack>();

    [Header("Sequence Logic")]
    public int minAttackPerSequence = 1;
    public int maxAttackPerSequence = 2;

    [Tooltip("Time in seconds between attack sequences")]
    public float minTimeBetweenSequences = 3.0f;
    public float maxTimeBetweenSequences = 5.0f;

    [Tooltip("Time in seconds between individual attacks in a sequence")]
    public float minTimeBetweenAttacks = 0.7f;
    public float maxTimeBetweenAttacks = 1.2f;

    [Header("Special Behaviors")]
    public MonoScript specialBehavior;
    public List<MonoScript> specialProperties = new List<MonoScript>();
}