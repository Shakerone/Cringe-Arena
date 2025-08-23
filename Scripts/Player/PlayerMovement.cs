using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraViewPoint; // Точка обзора камеры (для вертикального поворота)
    private CharacterController charCon;
    private PlayerInput playerInput;

    [Header("Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 10f;
    [SerializeField] private float mouseSensitivity = 1f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float gravityMultiplier = 2.5f;
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float verticalLookLimit = 60f;
    [SerializeField] private float checkRadius = 0.4f;
    [SerializeField] private GameObject feet; // Точка проверки земли (ноги)
    [SerializeField] private Animator fpsAnimator; // Аниматор для FPS вида

    [Header("Knockback Settings")]
    [SerializeField] private float knockbackDrag = 5f; // Скорость затухания knockback в воздухе
    [SerializeField] private float knockbackGroundDrag = 10f; // Скорость затухания на земле
    [Range(0f, 1f)]
    [SerializeField] private float movementInertiaPreserve = 0.5f; // Сколько инерции сохраняется при knockback
    [SerializeField] private float maxKnockbackSpeed = 30f; // Максимальная скорость от knockback

    // Private variables
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float verticalVelocity;
    private bool isGrounded;
    private bool jumpRequested;
    private float currentSpeed;
    private float verticalRotation = 0f;
    private Camera mainCamera;

    // Knockback
    private Vector3 knockbackVelocity = Vector3.zero;
    private bool isBeingKnockedBack;

    // Network sync
    [SyncVar(hook = nameof(OnWalkingChanged))]
    private bool isWalking;

    [SyncVar(hook = nameof(OnRotationChanged))]
    private float networkYRotation;

    // Constants for optimization
    private const float RotationSyncThreshold = 1f; // Порог для синхронизации поворота (чтобы избежать спама)

    private void Awake()
    {
        charCon = GetComponent<CharacterController>();
        playerInput = new PlayerInput();

        // Bind input actions
        playerInput.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        playerInput.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        playerInput.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        playerInput.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        playerInput.Player.Sprint.performed += ctx => currentSpeed = runSpeed;
        playerInput.Player.Sprint.canceled += ctx => currentSpeed = walkSpeed;

        playerInput.Player.Jump.performed += ctx =>
        {
            if (isGrounded) jumpRequested = true;
        };

        // Cursor control
        playerInput.Player.Escape.performed += ctx => ToggleCursorLock(false);
        playerInput.Player.MouseClick.performed += ctx =>
        {
            if (Cursor.lockState == CursorLockMode.None) ToggleCursorLock(true);
        };
    }

    private void ToggleCursorLock(bool shouldLock)
    {
        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;
        if (!shouldLock) lookInput = Vector2.zero; // Stop look input when unlocked
    }

    private void OnEnable() => playerInput.Enable();
    private void OnDisable() => playerInput.Disable();

    private void Start()
    {
        currentSpeed = walkSpeed;

        if (!isLocalPlayer)
        {
            if (playerInput != null) playerInput.Disable();
            return; // Non-local players don't process input or camera
        }

        // Local player setup
        mainCamera = GetComponentInChildren<Camera>();
        ToggleCursorLock(true);
    }

    private void Update()
    {
        if (!isLocalPlayer) return; // Only local player updates movement

        HandleLook();
        HandleGravityAndJump(); // Includes ground check now
        HandleKnockback();
        MoveCharacter();
        UpdateWalkingAnimation();
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        // Horizontal rotation (Y-axis)
        float yRotation = lookInput.x * mouseSensitivity;
        transform.Rotate(Vector3.up * yRotation);

        // Sync Y rotation if changed significantly
        float newYRotation = transform.eulerAngles.y;
        if (Mathf.Abs(networkYRotation - newYRotation) > RotationSyncThreshold)
        {
            if (isServer)
            {
                networkYRotation = newYRotation;
            }
            else
            {
                CmdUpdateRotation(newYRotation);
            }
        }

        // Vertical rotation (X-axis, clamped)
        verticalRotation -= lookInput.y * mouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -verticalLookLimit, verticalLookLimit);
        cameraViewPoint.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    private void HandleGravityAndJump()
    {
        // Ground check
        isGrounded = Physics.CheckSphere(feet.transform.position, checkRadius, groundLayers);

        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f; // Snap to ground
        }

        if (jumpRequested)
        {
            verticalVelocity = jumpForce;
            jumpRequested = false;
        }

        verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
    }

    private void HandleKnockback()
    {
        if (knockbackVelocity.magnitude > 0.1f)
        {
            isBeingKnockedBack = true;

            float currentDrag = isGrounded ? knockbackGroundDrag : knockbackDrag;

            // Dampen horizontal knockback
            knockbackVelocity.x = Mathf.Lerp(knockbackVelocity.x, 0, currentDrag * Time.deltaTime);
            knockbackVelocity.z = Mathf.Lerp(knockbackVelocity.z, 0, currentDrag * Time.deltaTime);

            // Vertical dampen
            if (!isGrounded)
            {
                knockbackVelocity.y = Mathf.Lerp(knockbackVelocity.y, 0, knockbackDrag * 0.3f * Time.deltaTime);
            }
            else
            {
                knockbackVelocity.y = 0;
            }

            // Fast dampen for small velocities
            if (knockbackVelocity.magnitude < 0.5f)
            {
                knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 10f * Time.deltaTime);
            }
        }
        else
        {
            isBeingKnockedBack = false;
            knockbackVelocity = Vector3.zero;
        }
    }

    private void MoveCharacter()
    {
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        move *= currentSpeed;

        // Blend movement during knockback (allow partial control)
        if (isBeingKnockedBack && knockbackVelocity.magnitude >= 5f)
        {
            float blendFactor = 1f - (knockbackVelocity.magnitude / 10f);
            move *= Mathf.Clamp01(blendFactor);
        }

        // Combine with knockback and vertical velocity
        Vector3 velocity = move + knockbackVelocity;
        velocity.y = verticalVelocity + knockbackVelocity.y;

        charCon.Move(velocity * Time.deltaTime);
    }

    [ClientRpc]
    public void RpcApplyKnockback(Vector3 force, float upwardModifier, bool horizontalOnly)
    {
        if (!isLocalPlayer) return;

        Vector3 knockbackForce = force;

        if (horizontalOnly)
        {
            knockbackForce.y = 0;
        }
        else
        {
            knockbackForce.y = Mathf.Abs(knockbackForce.y) + (force.magnitude * upwardModifier);
        }

        // Preserve some current momentum
        Vector3 currentHorizontalVelocity = new Vector3(charCon.velocity.x, 0, charCon.velocity.z);
        knockbackVelocity += knockbackForce + currentHorizontalVelocity * movementInertiaPreserve;

        // Cap max speed
        if (knockbackVelocity.magnitude > maxKnockbackSpeed)
        {
            knockbackVelocity = knockbackVelocity.normalized * maxKnockbackSpeed;
        }

        // Apply upward to vertical velocity if needed
        if (knockbackForce.y > 0)
        {
            verticalVelocity = knockbackForce.y;
        }
    }

    private void UpdateWalkingAnimation()
    {
        Vector3 horizontalVelocity = new Vector3(charCon.velocity.x, 0, charCon.velocity.z);
        bool newIsWalking = moveInput.magnitude > 0.1f && horizontalVelocity.magnitude > 0.1f && isGrounded && !isBeingKnockedBack;

        // Update local animator
        if (fpsAnimator != null)
        {
            fpsAnimator.SetBool("walking", newIsWalking);
        }

        // Sync if changed
        if (isWalking != newIsWalking)
        {
            if (isServer)
            {
                isWalking = newIsWalking;
            }
            else
            {
                CmdUpdateWalking(newIsWalking);
            }
        }
    }

    [Command]
    private void CmdUpdateWalking(bool walking)
    {
        isWalking = walking;
    }

    [Command]
    private void CmdUpdateRotation(float yRotation)
    {
        networkYRotation = yRotation;
    }

    private void OnWalkingChanged(bool oldValue, bool newValue)
    {
        if (fpsAnimator != null)
        {
            fpsAnimator.SetBool("walking", newValue);
        }
    }

    private void OnRotationChanged(float oldValue, float newValue)
    {
        if (!isLocalPlayer)
        {
            transform.rotation = Quaternion.Euler(0f, newValue, 0f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (feet != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(feet.transform.position, checkRadius);
        }

        if (isBeingKnockedBack)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, knockbackVelocity);
        }
    }
}