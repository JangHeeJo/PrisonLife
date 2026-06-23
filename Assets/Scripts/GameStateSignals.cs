using R3;
using UnityEngine;

/// <summary>
/// 특정 오브젝트와 상호작용이 성공했을 때 발생하는 신호입니다.
/// 예: 광석 최초 채굴, Deposit 포인트 최초 사용, Pickup 포인트 최초 사용 등.
/// </summary>
public readonly struct InteractionSignal
{
    public readonly string TargetId;

    public InteractionSignal(string targetId)
    {
        TargetId = targetId;
    }
}

/// <summary>
/// 특정 자원의 수량이 변경되었을 때 발생하는 신호입니다.
/// PlayerCarryStack, ResourceStorage, Machine 등에서 공통으로 사용할 수 있습니다.
/// </summary>
public readonly struct ResourceAmountSignal
{
    public readonly ResourceOwnerType OwnerType;
    public readonly string TargetId;
    public readonly ResourceType ResourceType;
    public readonly int CurrentAmount;
    public readonly int Capacity;

    public ResourceAmountSignal(
        ResourceOwnerType ownerType,
        string targetId,
        ResourceType resourceType,
        int currentAmount,
        int capacity)
    {
        OwnerType = ownerType;
        TargetId = targetId;
        ResourceType = resourceType;
        CurrentAmount = currentAmount;
        Capacity = capacity;
    }

    public bool IsFull => Capacity > 0 && CurrentAmount >= Capacity;
}

/// <summary>
/// Deposit / Pickup처럼 자원이 한 시스템에서 다른 시스템으로 이동했을 때 발생하는 신호입니다.
/// </summary>
public readonly struct ResourceTransactionSignal
{
    public readonly string TargetId;
    public readonly ResourceType ResourceType;
    public readonly int Amount;

    public ResourceTransactionSignal(string targetId, ResourceType resourceType, int amount)
    {
        TargetId = targetId;
        ResourceType = resourceType;
        Amount = amount;
    }
}

/// <summary>
/// 해금 포인트 상태가 변경되었을 때 발생하는 신호입니다.
/// </summary>
public readonly struct UnlockPointStateSignal
{
    public readonly string UnlockPointId;
    public readonly UnlockPointState State;

    public UnlockPointStateSignal(string unlockPointId, UnlockPointState state)
    {
        UnlockPointId = unlockPointId;
        State = state;
    }
}

/// <summary>
/// 감옥 수용량 관련 상태가 변경되었을 때 발생하는 신호입니다.
/// </summary>
public readonly struct PrisonStateSignal
{
    public readonly string PrisonId;
    public readonly int CurrentCount;
    public readonly int MaxCount;

    public PrisonStateSignal(string prisonId, int currentCount, int maxCount)
    {
        PrisonId = prisonId;
        CurrentCount = currentCount;
        MaxCount = maxCount;
    }

    public bool IsFull => MaxCount > 0 && CurrentCount >= MaxCount;
}

/// <summary>
/// 카메라 포커스가 끝나고 플레이어 추적으로 복귀했을 때 발생하는 신호입니다.
/// </summary>
public readonly struct CameraFocusSignal
{
    public readonly string TargetId;

    public CameraFocusSignal(string targetId)
    {
        TargetId = targetId;
    }
}

/// <summary>
/// 플레이어 CarryLimit이 변경되었을 때 발생하는 신호입니다.
///
/// CarryLimit:
/// - 플레이어가 직접 들 수 있는 최대 수량
/// - 자동화 유닛의 운반 한도 기준
///
/// PointCapacity:
/// - DepositPoint / PickupPoint / MoneyPoint 등 월드 포인트의 최대 표시/저장 수량
/// - 현재 규칙상 CarryLimit의 2배
/// </summary>
public readonly struct PlayerCarryLimitChangedSignal
{
    public readonly int CarryLimit;
    public readonly int PointCapacity;

    public PlayerCarryLimitChangedSignal(int carryLimit)
    {
        CarryLimit = Mathf.Max(1, carryLimit);
        PointCapacity = CarryLimit * 2;
    }
}

/// <summary>
/// 게임 상태 변화 스트림을 한 곳에서 관리하는 허브입니다.
/// Tutorial 전용 EventBus가 아니라,
/// 업적/퀘스트/미션에서도 재사용 가능한 Gameplay Signal Layer입니다.
/// </summary>
public sealed class GameStateSignals : MonoBehaviour
{
    public static GameStateSignals Instance { get; private set; }

    private readonly Subject<InteractionSignal> interacted = new();
    private readonly Subject<ResourceAmountSignal> resourceAmountChanged = new();
    private readonly Subject<ResourceAmountSignal> resourceFull = new();
    private readonly Subject<ResourceTransactionSignal> resourceDeposited = new();
    private readonly Subject<ResourceTransactionSignal> resourcePickedUp = new();
    private readonly Subject<UnlockPointStateSignal> unlockPointStateChanged = new();
    private readonly Subject<PrisonStateSignal> prisonStateChanged = new();
    private readonly Subject<PrisonStateSignal> prisonFull = new();
    private readonly Subject<CameraFocusSignal> cameraReturned = new();
    private readonly Subject<PlayerCarryLimitChangedSignal> playerCarryLimitChanged = new();

    public Observable<InteractionSignal> Interacted => interacted;
    public Observable<ResourceAmountSignal> ResourceAmountChanged => resourceAmountChanged;
    public Observable<ResourceAmountSignal> ResourceFull => resourceFull;
    public Observable<ResourceTransactionSignal> ResourceDeposited => resourceDeposited;
    public Observable<ResourceTransactionSignal> ResourcePickedUp => resourcePickedUp;
    public Observable<UnlockPointStateSignal> UnlockPointStateChanged => unlockPointStateChanged;
    public Observable<PrisonStateSignal> PrisonStateChanged => prisonStateChanged;
    public Observable<PrisonStateSignal> PrisonFull => prisonFull;
    public Observable<CameraFocusSignal> CameraReturned => cameraReturned;
    public Observable<PlayerCarryLimitChangedSignal> PlayerCarryLimitChanged => playerCarryLimitChanged;

    public int CurrentCarryLimit { get; private set; } = 10;
    public int CurrentPointCapacity => CurrentCarryLimit * 2;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        interacted.Dispose();
        resourceAmountChanged.Dispose();
        resourceFull.Dispose();
        resourceDeposited.Dispose();
        resourcePickedUp.Dispose();
        unlockPointStateChanged.Dispose();
        prisonStateChanged.Dispose();
        prisonFull.Dispose();
        cameraReturned.Dispose();
        playerCarryLimitChanged.Dispose();
    }

    public static void RaiseInteracted(string targetId)
    {
        if (Instance == null || string.IsNullOrEmpty(targetId))
            return;

        Instance.interacted.OnNext(new InteractionSignal(targetId));
    }

    public static void RaiseResourceAmountChanged(
        ResourceOwnerType ownerType,
        string targetId,
        ResourceType resourceType,
        int currentAmount,
        int capacity)
    {
        if (Instance == null)
            return;

        ResourceAmountSignal signal = new(
            ownerType,
            targetId,
            resourceType,
            currentAmount,
            capacity
        );

        Instance.resourceAmountChanged.OnNext(signal);

        if (signal.IsFull)
            Instance.resourceFull.OnNext(signal);
    }

    public static void RaiseResourceDeposited(
        string targetId,
        ResourceType resourceType,
        int amount)
    {
        if (Instance == null || string.IsNullOrEmpty(targetId))
            return;

        Instance.resourceDeposited.OnNext(
            new ResourceTransactionSignal(targetId, resourceType, amount)
        );
    }

    public static void RaiseResourcePickedUp(
        string targetId,
        ResourceType resourceType,
        int amount)
    {
        if (Instance == null || string.IsNullOrEmpty(targetId))
            return;

        Instance.resourcePickedUp.OnNext(
            new ResourceTransactionSignal(targetId, resourceType, amount)
        );
    }

    public static void RaiseUnlockPointStateChanged(
        string unlockPointId,
        UnlockPointState state)
    {
        if (Instance == null || string.IsNullOrEmpty(unlockPointId))
            return;

        Instance.unlockPointStateChanged.OnNext(
            new UnlockPointStateSignal(unlockPointId, state)
        );
    }

    public static void RaisePrisonStateChanged(
        string prisonId,
        int currentCount,
        int maxCount)
    {
        if (Instance == null || string.IsNullOrEmpty(prisonId))
            return;

        PrisonStateSignal signal = new(prisonId, currentCount, maxCount);

        Instance.prisonStateChanged.OnNext(signal);

        if (signal.IsFull)
            Instance.prisonFull.OnNext(signal);
    }

    public static void RaiseCameraReturned(string targetId)
    {
        if (Instance == null || string.IsNullOrEmpty(targetId))
            return;

        Instance.cameraReturned.OnNext(new CameraFocusSignal(targetId));
    }

    /// <summary>
    /// 플레이어 CarryLimit 변경 신호를 발행합니다.
    ///
    /// 호출 위치:
    /// - PlayerWeaponUpgradeController.ApplyUpgrade()
    ///
    /// 구독 대상:
    /// - PlayerCarryStack
    /// - ResourcePoint
    /// - HandcuffMachine
    /// - NpcProcessArea
    /// - MoneyRewardPoint
    /// - UnitCarryStack
    /// </summary>
    public static void RaisePlayerCarryLimitChanged(int carryLimit)
    {
        if (Instance == null)
            return;

        int safeLimit = Mathf.Max(1, carryLimit);

        Instance.CurrentCarryLimit = safeLimit;

        PlayerCarryLimitChangedSignal signal = new(safeLimit);
        Instance.playerCarryLimitChanged.OnNext(signal);

        Debug.Log(
            $"[GameStateSignals] PlayerCarryLimitChanged. " +
            $"CarryLimit: {signal.CarryLimit}, PointCapacity: {signal.PointCapacity}",
            Instance
        );
    }
}