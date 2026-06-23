using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

/// <summary>
/// 플레이어가 자원을 내려놓거나 가져가는 공통 포인트입니다.
/// Deposit / Pickup / Unlock / DepositAndPickup 모드를 인스펙터에서 선택해 사용합니다.
/// 
/// 변경점:
/// - Coroutine 기반 Money Burst Pickup 제거
/// - DOTween 콜백 기반 PlayJump를 PlayJumpAsync 기반으로 변경
/// - UniTask + CancellationToken 기반으로 정리
/// </summary>
public class ResourcePoint : InteractionPointBase
{
    [Header("Point")]
    [SerializeField] private ResourcePointMode pointMode = ResourcePointMode.Deposit;
    [SerializeField] private ResourceType resourceType = ResourceType.Ore;

    [Header("Target")]
    [SerializeField] private GameObject targetObject;

    [Header("Stack View")]
    [SerializeField] private ResourceStackView stackView;

    [Header("Transfer Effect")]
    [SerializeField] private float jumpPower = 1.2f;
    [SerializeField] private float transferDuration = 0.35f;

    [Header("Pickup Burst")]
    [Tooltip("Money 타입일 때 여러 개를 짧은 간격으로 가져올지 여부입니다.")]
    [SerializeField] private bool useBurstPickupForMoney = true;

    [Tooltip("한 번에 시작할 최대 픽업 개수입니다.")]
    [SerializeField, Min(1)] private int pickupBurstCount = 6;

    [Tooltip("각 돈이 출발하는 간격입니다.")]
    [SerializeField] private float pickupBurstInterval = 0.025f;

    [Header("Capacity")]
    [Tooltip("true면 CarryLimitChanged 신호를 받아 stackView 최대치를 PointCapacity로 갱신합니다.")]
    [SerializeField] private bool useCarryLimitPointCapacity = true;

    [SerializeField] private bool logCapacityChanged;

    [Header("State Signal")]
    [Tooltip("튜토리얼/미션에서 이 포인트를 구분하기 위한 ID입니다. 비워두면 gameObject.name을 사용합니다.")]
    [SerializeField] private string pointTargetId;

    private IResourceReceiver receiver;
    private IResourceProvider provider;

    private bool isTransferring;
    private bool isPickupBurstRunning;

    private IDisposable carryLimitSubscription;
    private CancellationTokenSource pickupBurstCts;

    private bool ShouldUseBurstPickup =>
        useBurstPickupForMoney &&
        resourceType == ResourceType.Money &&
        pickupBurstCount > 1;

    private void Awake()
    {
        receiver = FindTargetInterface<IResourceReceiver>();
        provider = FindTargetInterface<IResourceProvider>();

        ValidateTarget();
    }

    private void OnEnable()
    {
        TrySubscribeCarryLimit();
        ApplyCurrentPointCapacityIfPossible();
    }

    private void Start()
    {
        TrySubscribeCarryLimit();
        ApplyCurrentPointCapacityIfPossible();
    }

    private void OnDisable()
    {
        carryLimitSubscription?.Dispose();
        carryLimitSubscription = null;

        CancelPickupBurst();
    }

    private void OnDestroy()
    {
        CancelPickupBurst();
    }

    protected override bool TryInteract(PlayerCarryStack playerCarryStack)
    {
        if (playerCarryStack == null)
            return false;

        switch (pointMode)
        {
            case ResourcePointMode.Deposit:
            case ResourcePointMode.Unlock:
                if (isTransferring)
                    return false;

                return TryDeposit(playerCarryStack);

            case ResourcePointMode.Pickup:
                if (ShouldUseBurstPickup)
                    return TryStartPickupBurst(playerCarryStack);

                if (isTransferring)
                    return false;

                return TryPickupSingle(playerCarryStack);

            case ResourcePointMode.DepositAndPickup:
                if (!isTransferring && TryDeposit(playerCarryStack))
                    return true;

                if (ShouldUseBurstPickup)
                    return TryStartPickupBurst(playerCarryStack);

                if (isTransferring)
                    return false;

                return TryPickupSingle(playerCarryStack);
        }

        return false;
    }

    private bool TryDeposit(PlayerCarryStack playerCarryStack)
    {
        if (receiver == null)
            return false;

        if (!receiver.CanReceive(resourceType, 1))
            return false;

        if (stackView != null && stackView.IsFull)
            return false;

        if (!playerCarryStack.TryGetTopWorldPosition(resourceType, out Vector3 startPosition))
            return false;

        if (!playerCarryStack.TryRemove(resourceType))
            return false;

        if (!receiver.TryReceive(resourceType, 1))
        {
            playerCarryStack.TryAdd(resourceType);
            return false;
        }

        RaisePointInteracted();
        GameStateSignals.RaiseResourceDeposited(GetSignalTargetId(), resourceType, 1);

        PlayDepositEffect(startPosition);
        return true;
    }

    public bool CanAddFromAutomation(ResourceType addResourceType, int amount)
    {
        if (amount <= 0)
            return false;

        if (resourceType != addResourceType)
            return false;

        if (receiver == null)
            return false;

        if (stackView != null && stackView.IsFull)
            return false;

        return receiver.CanReceive(resourceType, amount);
    }

    public bool TryAddFromAutomation(ResourceType addResourceType, int amount)
    {
        if (amount <= 0)
            return false;

        if (resourceType != addResourceType)
            return false;

        if (receiver == null)
            return false;

        int addedAmount = 0;

        for (int i = 0; i < amount; i++)
        {
            if (stackView != null && stackView.IsFull)
                break;

            if (!receiver.CanReceive(resourceType, 1))
                break;

            if (!receiver.TryReceive(resourceType, 1))
                break;

            addedAmount++;

            if (stackView != null)
                stackView.ShowNext();
        }

        if (addedAmount <= 0)
            return false;

        GameStateSignals.RaiseResourceDeposited(
            GetSignalTargetId(),
            resourceType,
            addedAmount
        );

        return true;
    }

    public bool CanTakeForAutomation(ResourceType takeResourceType, int amount)
    {
        if (amount <= 0)
            return false;

        if (resourceType != takeResourceType)
            return false;

        if (provider == null)
            return false;

        if (stackView == null || stackView.IsEmpty)
            return false;

        return provider.CanProvide(resourceType, amount);
    }

    public bool TryTakeForAutomation(ResourceType takeResourceType, int amount)
    {
        if (amount <= 0)
            return false;

        if (resourceType != takeResourceType)
            return false;

        if (provider == null)
            return false;

        int takenAmount = 0;

        for (int i = 0; i < amount; i++)
        {
            if (stackView == null || stackView.IsEmpty)
                break;

            if (!provider.CanProvide(resourceType, 1))
                break;

            if (!provider.TryProvide(resourceType, 1))
                break;

            if (!stackView.HideLast())
                break;

            takenAmount++;
        }

        if (takenAmount <= 0)
            return false;

        RaisePointInteracted();

        GameStateSignals.RaiseResourcePickedUp(
            GetSignalTargetId(),
            resourceType,
            takenAmount
        );

        return true;
    }

    private bool TryPickupSingle(PlayerCarryStack playerCarryStack)
    {
        return TryPickupOne(playerCarryStack, true);
    }

    private bool TryStartPickupBurst(PlayerCarryStack playerCarryStack)
    {
        if (isPickupBurstRunning)
            return false;

        if (!CanPickupOne(playerCarryStack))
            return false;

        CancelPickupBurst();

        pickupBurstCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy()
        );

        PickupBurstAsync(
            playerCarryStack,
            pickupBurstCts,
            pickupBurstCts.Token
        ).Forget();

        return true;
    }

    private async UniTaskVoid PickupBurstAsync(
        PlayerCarryStack playerCarryStack,
        CancellationTokenSource localCts,
        CancellationToken token)
    {
        isPickupBurstRunning = true;

        try
        {
            int pickedCount = 0;

            while (pickedCount < pickupBurstCount)
            {
                token.ThrowIfCancellationRequested();

                if (playerCarryStack == null)
                    break;

                if (!TryPickupOne(playerCarryStack, false))
                    break;

                pickedCount++;

                if (pickupBurstInterval > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(pickupBurstInterval),
                        cancellationToken: token
                    );
                }
                else
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(pickupBurstCts, localCts))
                pickupBurstCts = null;

            localCts?.Dispose();
            isPickupBurstRunning = false;
        }
    }

    private void CancelPickupBurst()
    {
        if (pickupBurstCts == null)
        {
            isPickupBurstRunning = false;
            return;
        }

        CancellationTokenSource targetCts = pickupBurstCts;
        pickupBurstCts = null;

        targetCts.Cancel();
    }

    private bool CanPickupOne(PlayerCarryStack playerCarryStack)
    {
        if (provider == null || playerCarryStack == null)
            return false;

        if (stackView == null || stackView.IsEmpty)
            return false;

        if (!provider.CanProvide(resourceType, 1))
            return false;

        if (!playerCarryStack.CanReserve(resourceType, 1))
            return false;

        return true;
    }

    private bool TryPickupOne(PlayerCarryStack playerCarryStack, bool lockTransfer)
    {
        if (!CanPickupOne(playerCarryStack))
            return false;

        if (!stackView.TryGetTopWorldPosition(out Vector3 startPosition))
            return false;

        if (!playerCarryStack.TryReserveNextWorldPosition(
                resourceType,
                out int reservedStackIndex,
                out _))
        {
            return false;
        }

        if (!provider.TryProvide(resourceType, 1))
        {
            playerCarryStack.CancelReserved(resourceType);
            return false;
        }

        if (!stackView.HideLast())
        {
            playerCarryStack.CancelReserved(resourceType);
            return false;
        }

        RaisePointInteracted();

        PlayPickupEffect(
            playerCarryStack,
            startPosition,
            reservedStackIndex,
            lockTransfer
        );

        return true;
    }

    private void PlayDepositEffect(Vector3 startPosition)
    {
        PlayDepositEffectAsync(startPosition).Forget();
    }

    private async UniTaskVoid PlayDepositEffectAsync(Vector3 startPosition)
    {
        if (stackView == null)
        {
            isTransferring = false;
            return;
        }

        if (!stackView.TryGetNextWorldPosition(out Vector3 endPosition))
        {
            isTransferring = false;
            return;
        }

        GameObject visualPrefab = stackView.VisualPrefab;

        if (DoTweenManager.Instance == null || visualPrefab == null)
        {
            stackView.ShowNext();
            isTransferring = false;
            return;
        }

        isTransferring = true;

        try
        {
            await DoTweenManager.Instance.PlayJumpAsync(
                visualPrefab,
                startPosition,
                endPosition,
                jumpPower,
                transferDuration,
                this.GetCancellationTokenOnDestroy()
            );

            stackView.ShowNext();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isTransferring = false;
        }
    }

    private void PlayPickupEffect(
        PlayerCarryStack playerCarryStack,
        Vector3 startPosition,
        int reservedStackIndex,
        bool lockTransfer)
    {
        PlayPickupEffectAsync(
            playerCarryStack,
            startPosition,
            reservedStackIndex,
            lockTransfer
        ).Forget();
    }

    private async UniTaskVoid PlayPickupEffectAsync(
        PlayerCarryStack playerCarryStack,
        Vector3 startPosition,
        int reservedStackIndex,
        bool lockTransfer)
    {
        if (stackView == null)
            return;

        GameObject visualPrefab = stackView.VisualPrefab;

        if (lockTransfer)
            isTransferring = true;

        bool completed = false;

        try
        {
            if (DoTweenManager.Instance == null || visualPrefab == null)
            {
                CompletePickup(playerCarryStack, lockTransfer);
                completed = true;
                return;
            }

            await DoTweenManager.Instance.PlayJumpAsync(
                visualPrefab,
                startPosition,
                () =>
                {
                    if (playerCarryStack != null &&
                        playerCarryStack.TryGetStackWorldPosition(
                            resourceType,
                            reservedStackIndex,
                            out Vector3 dynamicEndPosition))
                    {
                        return dynamicEndPosition;
                    }

                    return playerCarryStack != null
                        ? playerCarryStack.transform.position
                        : startPosition;
                },
                jumpPower,
                transferDuration,
                this.GetCancellationTokenOnDestroy()
            );

            CompletePickup(playerCarryStack, lockTransfer);
            completed = true;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (lockTransfer && !completed)
                isTransferring = false;
        }
    }

    private void CompletePickup(PlayerCarryStack playerCarryStack, bool lockTransfer)
    {
        bool added = playerCarryStack != null && playerCarryStack.TryAddReserved(resourceType);

        if (added)
        {
            GameStateSignals.RaiseResourcePickedUp(
                GetSignalTargetId(),
                resourceType,
                1
            );
        }

        if (lockTransfer)
            isTransferring = false;
    }

    private void TrySubscribeCarryLimit()
    {
        if (!useCarryLimitPointCapacity)
            return;

        if (carryLimitSubscription != null)
            return;

        if (GameStateSignals.Instance == null)
            return;

        carryLimitSubscription = GameStateSignals.Instance.PlayerCarryLimitChanged
            .Subscribe(signal => OnPlayerCarryLimitChanged(signal));
    }

    private void ApplyCurrentPointCapacityIfPossible()
    {
        if (!useCarryLimitPointCapacity)
            return;

        if (GameStateSignals.Instance == null)
            return;

        ApplyPointCapacity(GameStateSignals.Instance.CurrentPointCapacity);
    }

    private void OnPlayerCarryLimitChanged(PlayerCarryLimitChangedSignal signal)
    {
        ApplyPointCapacity(signal.PointCapacity);
    }

    private void ApplyPointCapacity(int pointCapacity)
    {
        int safeCapacity = Mathf.Max(1, pointCapacity);

        if (stackView != null)
            stackView.SetMaxCount(safeCapacity);

        if (logCapacityChanged)
        {
            Debug.Log(
                $"[ResourcePoint] ApplyPointCapacity. " +
                $"Point: {GetSignalTargetId()}, Capacity: {safeCapacity}",
                this
            );
        }
    }

    private T FindTargetInterface<T>() where T : class
    {
        if (targetObject == null)
            return null;

        MonoBehaviour[] components = targetObject.GetComponents<MonoBehaviour>();

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is T result)
                return result;
        }

        return null;
    }

    private void ValidateTarget()
    {
        if (targetObject == null)
            return;

        bool needReceiver =
            pointMode == ResourcePointMode.Deposit ||
            pointMode == ResourcePointMode.Unlock ||
            pointMode == ResourcePointMode.DepositAndPickup;

        bool needProvider =
            pointMode == ResourcePointMode.Pickup ||
            pointMode == ResourcePointMode.DepositAndPickup;

        if (needReceiver && receiver == null)
        {
            Debug.LogError(
                $"{targetObject.name} must have a component that implements {nameof(IResourceReceiver)}.",
                this
            );
        }

        if (needProvider && provider == null)
        {
            Debug.LogError(
                $"{targetObject.name} must have a component that implements {nameof(IResourceProvider)}.",
                this
            );
        }
    }

    private string GetSignalTargetId()
    {
        return string.IsNullOrEmpty(pointTargetId) ? gameObject.name : pointTargetId;
    }

    private void RaisePointInteracted()
    {
        GameStateSignals.RaiseInteracted(GetSignalTargetId());
    }
}