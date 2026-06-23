using PinePie.SimpleJoystick;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private JoystickController joystick;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform modelRoot;

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
    }

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (modelRoot == null)
            modelRoot = transform;
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

        Vector3 moveDirection = new Vector3(input.x, 0f, input.y);

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            moveDirection.Normalize();
            Rotate(moveDirection);
        }

        ApplyGravity();

        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
    }

    private void Rotate(Vector3 moveDirection)
    {
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);

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
