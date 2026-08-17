using UnityEngine;

public class Bed : MonoBehaviour
{
    public Transform sleepAnchor;
    public bool IsOccupied { get; set; }
    public bool IsReserved { get; set; }

    public bool IsAvailable => !IsOccupied && !IsReserved;
}