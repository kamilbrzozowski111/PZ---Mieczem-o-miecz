using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SphereController : MonoBehaviour
{
    [Tooltip("How much force to apply to the ball")]
    public float rollSpeed = 5f;

    private Rigidbody rb;

    void Start()
    {
        // Cache the Rigidbody component
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // Use FixedUpdate for physics-based movement
        float moveHorizontal = Input.GetAxis("Horizontal"); // A/D or Left/Right
        float moveVertical = Input.GetAxis("Vertical");     // W/S or Up/Down

        // Create a movement vector
        // X = East/West, Z = North/South
        Vector3 movement = new Vector3(moveHorizontal, 0.0f, moveVertical);

        // Apply force to the Rigidbody
        rb.AddForce(movement * rollSpeed);
    }
}