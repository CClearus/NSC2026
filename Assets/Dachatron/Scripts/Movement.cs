using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Key Bindings")]
    public KeyCode moveLeftKey  = KeyCode.D;
    public KeyCode moveRightKey = KeyCode.A;
    public KeyCode moveForwardKey  = KeyCode.S;
    public KeyCode moveBackwardKey = KeyCode.W;
    public KeyCode jumpKey      = KeyCode.Space;

    [Header("Movement Settings")]
    public float moveSpeed  = 5f;
    public float jumpForce  = 7f;

    [Header("Camera Follow")]
    [Tooltip("Camera to follow the player with. Leave empty to auto-use Camera.main.")]
    public Transform cameraTransform;
    [Tooltip("How smoothly the camera eases toward the player - lower is snappier, higher is laggier.")]
    public float cameraSmoothTime = 0.15f;

    private Rigidbody rb;
    private bool isGrounded;

    // The camera's position relative to the player, captured once at Start so
    // whatever framing/distance is already set up in the Editor is preserved -
    // no need to hand-tune offset numbers here.
    private Vector3 cameraOffset;
    private Vector3 cameraVelocity;
    private bool hasCamera;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
        {
            cameraOffset = cameraTransform.position - transform.position;
            hasCamera = true;
        }
        else
        {
            Debug.LogWarning("[PlayerMovement] No camera assigned and no Camera.main found - camera won't follow the player.");
        }
    }

    // Runs after all Update/FixedUpdate movement has been applied this frame,
    // so the camera never lags a frame behind the player.
    private void LateUpdate()
    {
        if (!hasCamera) return;

        Vector3 targetPosition = transform.position + cameraOffset;
        cameraTransform.position = Vector3.SmoothDamp(cameraTransform.position, targetPosition, ref cameraVelocity, cameraSmoothTime);
    }

    private void Update()
    {
        if (Input.GetKeyDown(jumpKey))
        {
            if (isGrounded)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                isGrounded = false;
            }
        }
    }

    private void FixedUpdate()
    {
        float horizontal = 0f;

        if (Input.GetKey(moveRightKey))
        {
            horizontal = 1f;
        }

        if (Input.GetKey(moveLeftKey))
        {
            horizontal = -1f;
        }

        float depth = 0f;

        if (Input.GetKey(moveForwardKey))
        {
            depth = 1f;
        }

        if (Input.GetKey(moveBackwardKey))
        {
            depth = -1f;
        }

        Vector3 movement = new Vector3(horizontal, 0f, depth);

        if (movement.magnitude > 1f)
        {
            movement.Normalize();
        }
        Vector3 newVelocity = new Vector3(
            movement.x * moveSpeed,
            rb.linearVelocity.y,      
            movement.z * moveSpeed
        );

        rb.linearVelocity = newVelocity;
    }


    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }
}