using UnityEngine;

/// <summary>
/// 수갑 운반 자동화 유닛입니다.
///
/// 루틴:
/// - 수갑 PickupPoint로 이동
/// - UnitCarryStack 한도까지 수갑을 가져와 몸에 쌓음
/// - 최소 배달 수량 이상이면 DepositPoint로 이동 가능
/// - 수갑 DepositPoint로 이동
/// - 들고 있는 수갑을 하나씩 내려놓음
/// - 수갑이 0개가 되면 다시 PickupPoint로 이동
///
/// 시각 표시:
/// - UnitCarryStack + ResourceStackView를 사용합니다.
/// - HandcuffDeliveryWorkerUnit은 View를 직접 건드리지 않습니다.
/// </summary>
public sealed class HandcuffDeliveryWorkerUnit : MonoBehaviour, IUnitSpawnContextReceiver
{
    private enum WorkerState
    {
        Idle,
        MoveToPickup,
        TakeHandcuff,
        MoveToDeposit,
        DepositHandcuff,
        Wait
    }

    [Header("Resource")]
    [SerializeField] private ResourceType handcuffResourceType = ResourceType.Handcuff;

    [SerializeField] private ResourcePoint handcuffPickupPoint;
    [SerializeField] private ResourcePoint handcuffDepositPoint;

    [Header("Carry")]
    [SerializeField] private UnitCarryStack carryStack;

    [Tooltip("이 개수 이상 들고 있을 때만 내려놓으러 갑니다.")]
    [SerializeField, Min(1)] private int minDeliveryCount = 5;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float arriveDistance = 0.45f;
    [SerializeField] private float rotateSpeed = 12f;

    [Header("Work Speed")]
    [Tooltip("수갑을 하나 가져오는 간격입니다. 낮을수록 빠릅니다.")]
    [SerializeField] private float takeInterval = 0.15f;

    [Tooltip("수갑을 하나 내려놓는 간격입니다. 낮을수록 빠릅니다.")]
    [SerializeField] private float depositInterval = 0.15f;

    [Tooltip("가져갈 수갑이 없거나 내려놓을 곳이 꽉 찼을 때 재시도 대기 시간입니다.")]
    [SerializeField] private float retryDelay = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private WorkerState state = WorkerState.Idle;

    private float workTimer;
    private float waitTimer;
    private bool isInitialized;
    private bool warnedMissingContext;

    private int CurrentCarryCount => carryStack != null ? carryStack.CurrentCount : 0;
    private int CarryLimit => carryStack != null ? carryStack.Capacity : 0;

    private void Awake()
    {
        if (carryStack == null)
            carryStack = GetComponent<UnitCarryStack>();
    }

    private void OnEnable()
    {
        ResetState();

        // 씬에 직접 배치해서 테스트하는 경우를 위해,
        // 인스펙터 값이 이미 있으면 바로 동작할 수 있게 합니다.
        isInitialized = HasValidContext();
    }

    /// <summary>
    /// UnitSpawnController가 생성 직후 호출합니다.
    /// </summary>
    public void Initialize(UnitSpawnContext context)
    {
        if (context.HandcuffPickupPoint != null)
            handcuffPickupPoint = context.HandcuffPickupPoint;

        if (context.HandcuffDepositPoint != null)
            handcuffDepositPoint = context.HandcuffDepositPoint;

        if (carryStack == null)
            carryStack = GetComponent<UnitCarryStack>();

        if (carryStack != null && context.CarryLimit > 0)
            carryStack.SetCapacity(context.CarryLimit);

        ResetState();

        isInitialized = HasValidContext();
        warnedMissingContext = false;

        if (isInitialized)
        {
            Debug.Log(
                $"[HandcuffDeliveryWorkerUnit] Initialized. " +
                $"CarryLimit: {CarryLimit}, " +
                $"MinDelivery: {minDeliveryCount}, " +
                $"Pickup: {handcuffPickupPoint.name}, " +
                $"Deposit: {handcuffDepositPoint.name}",
                this
            );
        }
        else
        {
            Debug.LogWarning(
                "[HandcuffDeliveryWorkerUnit] Initialize failed. " +
                "PickupPoint, DepositPoint, or UnitCarryStack is missing.",
                this
            );
        }
    }

    private void Update()
    {
        if (!isInitialized)
        {
            WarnMissingContextOnce();
            return;
        }

        switch (state)
        {
            case WorkerState.Idle:
                UpdateIdle();
                break;

            case WorkerState.MoveToPickup:
                UpdateMoveToPickup();
                break;

            case WorkerState.TakeHandcuff:
                UpdateTakeHandcuff();
                break;

            case WorkerState.MoveToDeposit:
                UpdateMoveToDeposit();
                break;

            case WorkerState.DepositHandcuff:
                UpdateDepositHandcuff();
                break;

            case WorkerState.Wait:
                UpdateWait();
                break;
        }
    }

    private bool HasValidContext()
    {
        return handcuffPickupPoint != null &&
               handcuffDepositPoint != null &&
               carryStack != null;
    }

    private void WarnMissingContextOnce()
    {
        if (warnedMissingContext)
            return;

        warnedMissingContext = true;

        Debug.LogWarning(
            "[HandcuffDeliveryWorkerUnit] Missing context. " +
            "Check UnitSpawnController Entry: Handcuff Pickup Point / Handcuff Deposit Point, " +
            "and prefab UnitCarryStack.",
            this
        );
    }

    private void ResetState()
    {
        state = WorkerState.Idle;
        workTimer = 0f;
        waitTimer = 0f;
    }

    private void UpdateIdle()
    {
        if (CurrentCarryCount >= CarryLimit)
        {
            ChangeState(WorkerState.MoveToDeposit);
            return;
        }

        if (CurrentCarryCount >= minDeliveryCount &&
            !handcuffPickupPoint.CanTakeForAutomation(handcuffResourceType, 1))
        {
            ChangeState(WorkerState.MoveToDeposit);
            return;
        }

        ChangeState(WorkerState.MoveToPickup);
    }

    private void UpdateMoveToPickup()
    {
        if (MoveTo(handcuffPickupPoint.transform.position))
        {
            workTimer = 0f;
            ChangeState(WorkerState.TakeHandcuff);
        }
    }

    private void UpdateTakeHandcuff()
    {
        if (carryStack.IsFull)
        {
            ChangeState(WorkerState.MoveToDeposit);
            return;
        }

        bool canTakeFromPoint = handcuffPickupPoint.CanTakeForAutomation(handcuffResourceType, 1);
        bool canCarryMore = carryStack.CanAdd(handcuffResourceType, 1);

        if (!canTakeFromPoint || !canCarryMore)
        {
            // 5개 이상 들고 있으면 내려놓으러 갑니다.
            if (CurrentCarryCount >= minDeliveryCount)
            {
                ChangeState(WorkerState.MoveToDeposit);
                return;
            }

            // 5개 미만이면 수갑이 생길 때까지 근처에서 대기합니다.
            Wait(retryDelay);
            return;
        }

        workTimer += Time.deltaTime;

        if (workTimer < takeInterval)
            return;

        workTimer = 0f;

        // 먼저 실제 PickupPoint에서 수갑을 하나 제거합니다.
        bool taken = handcuffPickupPoint.TryTakeForAutomation(handcuffResourceType, 1);

        if (!taken)
        {
            Wait(retryDelay);
            return;
        }

        // 그 다음 유닛 CarryStack에 시각적으로 추가합니다.
        bool addedToCarry = carryStack.TryAdd(handcuffResourceType, 1);

        if (!addedToCarry)
        {
            Debug.LogWarning(
                "[HandcuffDeliveryWorkerUnit] Took handcuff but failed to add to UnitCarryStack.",
                this
            );

            Wait(retryDelay);
            return;
        }

        if (logState)
        {
            Debug.Log(
                $"[HandcuffDeliveryWorkerUnit] Take Handcuff. " +
                $"Carry: {CurrentCarryCount}/{CarryLimit}",
                this
            );
        }

        // 한도까지 채웠으면 내려놓으러 갑니다.
        if (carryStack.IsFull)
            ChangeState(WorkerState.MoveToDeposit);
    }

    private void UpdateMoveToDeposit()
    {
        if (carryStack.IsEmpty)
        {
            ChangeState(WorkerState.MoveToPickup);
            return;
        }

        if (MoveTo(handcuffDepositPoint.transform.position))
        {
            workTimer = 0f;
            ChangeState(WorkerState.DepositHandcuff);
        }
    }

    private void UpdateDepositHandcuff()
    {
        if (carryStack.IsEmpty)
        {
            ChangeState(WorkerState.MoveToPickup);
            return;
        }

        if (!handcuffDepositPoint.CanAddFromAutomation(handcuffResourceType, 1))
        {
            Wait(retryDelay);
            return;
        }

        workTimer += Time.deltaTime;

        if (workTimer < depositInterval)
            return;

        workTimer = 0f;

        // 유닛이 실제로 들고 있는 수갑이 있는지 먼저 확인합니다.
        if (!carryStack.CanRemove(handcuffResourceType, 1))
        {
            ChangeState(WorkerState.MoveToPickup);
            return;
        }

        // DepositPoint에 내려놓습니다.
        bool deposited = handcuffDepositPoint.TryAddFromAutomation(handcuffResourceType, 1);

        if (!deposited)
        {
            Wait(retryDelay);
            return;
        }

        // 내려놓기에 성공했으므로 유닛 CarryStack에서 제거합니다.
        bool removedFromCarry = carryStack.TryRemove(handcuffResourceType, 1);

        if (!removedFromCarry)
        {
            Debug.LogWarning(
                "[HandcuffDeliveryWorkerUnit] Deposited handcuff but failed to remove from UnitCarryStack.",
                this
            );
        }

        if (logState)
        {
            Debug.Log(
                $"[HandcuffDeliveryWorkerUnit] Deposit Handcuff. " +
                $"Carry: {CurrentCarryCount}/{CarryLimit}",
                this
            );
        }

        if (carryStack.IsEmpty)
            ChangeState(WorkerState.MoveToPickup);
    }

    private void Wait(float duration)
    {
        waitTimer = Mathf.Max(0f, duration);
        ChangeState(WorkerState.Wait);
    }

    private void UpdateWait()
    {
        waitTimer -= Time.deltaTime;

        if (waitTimer > 0f)
            return;

        ChangeState(WorkerState.Idle);
    }

    private bool MoveTo(Vector3 targetPosition)
    {
        Vector3 currentPosition = transform.position;

        targetPosition.y = currentPosition.y;

        Vector3 direction = targetPosition - currentPosition;
        float distance = direction.magnitude;

        if (distance <= arriveDistance)
            return true;

        Vector3 moveDirection = direction.normalized;

        transform.position += moveDirection * moveSpeed * Time.deltaTime;
        RotateTo(moveDirection);

        return false;
    }

    private void RotateTo(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );
    }

    private void ChangeState(WorkerState nextState)
    {
        if (state == nextState)
            return;

        state = nextState;

        if (logState)
            Debug.Log($"[HandcuffDeliveryWorkerUnit] State: {state}", this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        minDeliveryCount = Mathf.Max(1, minDeliveryCount);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        arriveDistance = Mathf.Max(0.05f, arriveDistance);
        takeInterval = Mathf.Max(0.01f, takeInterval);
        depositInterval = Mathf.Max(0.01f, depositInterval);
        retryDelay = Mathf.Max(0.01f, retryDelay);
    }
#endif
}