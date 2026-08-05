using PinePie.SimpleJoystick;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private JoystickController joystick;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform modelRoot;

    [Header("Camera Relative Movement")]
    [Tooltip("비워두면 Camera.main을 자동으로 사용합니다.")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("켜두면 조이스틱 방향을 카메라 화면 기준으로 변환합니다. 쿼터뷰에서는 켜두는 것을 추천합니다.")]
    [SerializeField] private bool useCameraRelativeMovement = true;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotateSpeed = 12f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -1f;

    private float verticalVelocity;

    // 튜토리얼 카메라 포커스, 강제 안내 연출 중 플레이어 입력을 막기 위한 플래그입니다.
    private bool isInputLocked;

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        modelRoot = transform;
        mainCamera = Camera.main;
    }

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (modelRoot == null)
            modelRoot = transform;

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Update()
    {
        Move();
    }

    /// <summary>
    /// 외부 시스템에서 플레이어 입력을 잠그거나 해제할 때 사용합니다.
    /// 현재는 튜토리얼 카메라 포커스 중 조이스틱 입력을 막는 용도입니다.
    /// </summary>
    public void SetInputLocked(bool locked)
    {
        isInputLocked = locked;
    }

    private void Move()
    {
        Vector2 input = Vector2.zero;

        // 입력 잠금 상태에서는 조이스틱 입력을 읽지 않습니다.
        // CharacterController의 중력 처리는 계속 해야 하므로 Move 자체는 계속 호출합니다.
        if (!isInputLocked && joystick != null)
            input = joystick.InputDirection;

        Vector3 moveDirection = GetMoveDirection(input);

        if (moveDirection.sqrMagnitude > 0.001f)
            Rotate(moveDirection);

        ApplyGravity();

        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
    }

    private Vector3 GetMoveDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.001f)
            return Vector3.zero;

        // 카메라 기준 이동을 사용하지 않거나 카메라가 없으면 기존 월드 기준 이동으로 처리합니다.
        if (!useCameraRelativeMovement || mainCamera == null)
            return new Vector3(input.x, 0f, input.y).normalized;

        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 cameraRight = mainCamera.transform.right;

        // 바닥 이동만 필요하므로 카메라의 상하 기울기는 제거합니다.
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        if (cameraForward.sqrMagnitude <= 0.001f || cameraRight.sqrMagnitude <= 0.001f)
            return new Vector3(input.x, 0f, input.y).normalized;

        cameraForward.Normalize();
        cameraRight.Normalize();

        // 조이스틱 X는 화면 오른쪽, Y는 화면 위쪽 방향으로 변환합니다.
        Vector3 moveDirection = cameraRight * input.x + cameraForward * input.y;

        if (moveDirection.sqrMagnitude <= 0.001f)
            return Vector3.zero;

        return moveDirection.normalized;
    }

    private void Rotate(Vector3 moveDirection)
    {
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);

        modelRoot.rotation = Quaternion.Slerp(
            modelRoot.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );
    }

    private void ApplyGravity()
    {
        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = groundedGravity;

        verticalVelocity += gravity * Time.deltaTime;
    }
}
