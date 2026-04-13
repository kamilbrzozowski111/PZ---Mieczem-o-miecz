using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyPreset", menuName = "ScriptableObjects/EnemyPreset")]
public class EnemyPreset : ScriptableObject
{
    public string enemyName;

    [Header("Movement Stats")]
    public float moveSpeed = 3.5f;
    public float angularSpeed = 120f;
    public float acceleration = 8f;

    [Header("Combat Stats")]
    public float duelRange = 3f;
    public int health = 100;

    [Header("Visuals")]
    public Color gizmoColor = Color.red;
}