using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

/// <summary>
/// 플레이어가 들고 있는 자원 스택의 시각 표현과 보유 수량을 관리합니다.
///
/// 역할:
/// - Ore / Money / Handcuff 같은 자원별 스택 비주얼을 풀링합니다.
/// - GameStateSignals를 통해 보유 수량 변화를 외부 시스템에 알립니다.
/// - CarryLimit 변경 신호를 구독해 최대 보유량을 동적으로 갱신합니다.
/// </summary>
public class PlayerCarryStack : MonoBehaviour
{
    [System.Serializable]
    private class StackGroup
    {
        public ResourceType type = default;
        public Transform root = null;
        public GameObject prefab = null;
        public int maxCount = 10;

        [Header("Stack Layout")]
        public int columns = 2;
        public float xSpacing = 0.25f;
        public float ySpacing = 0.18f;
        public float zSpacing = 0f;

        [HideInInspector] public int currentCount;

        // Slots reserved by in-flight pickup animations before the resource becomes visible.
        [HideInInspector] public int reservedCount;

        [HideInInspector] public List<GameObject> visuals = new List<GameObject>();

        public bool HasItem => currentCount > 0;

        // Includes reservations so concurrent pickups cannot overfill the stack.
        public bool IsFull => currentCount + reservedCount >= maxCount;
    }

    [Header("Stack Groups")]
    [SerializeField] private List<StackGroup> stackGroups = new List<StackGroup>();

    [Header("Front Stack")]
    [SerializeField] private Vector3 handcuffPosition = new Vector3(0.602f, 0.6f, 0.968f);

    [Header("Back Stack")]
    [SerializeField] private Vector3 backBasePosition = new Vector3(0.16f, -0.149f, -0.71f);

    [Tooltip("Offset applied to the money stack when ore is already shown on the back stack.")]
    [SerializeField] private Vector3 moneyOffsetWhenOreExists = new Vector3(0f, 0f, -0.35f);

    [Header("State Signal")]
    [Tooltip("TargetId used when PlayerCarryStack raises resource state signals.")]
    [SerializeField] private string stateTargetId = "PlayerCarryStack";

    // Back-stack ordering currently supports Ore and Money.
    [SerializeField]
    private ResourceType[] backStackOrder =
    {
        ResourceType.Ore,
        ResourceType.Money
    };

    private IDisposable carryLimitSubscription;

    private void Awake()
    {
        CreateVisualPool();
        RefreshStackPositions();
    }

    private void OnEnable()
    {
        TrySubscribeCarryLimit();
        ApplyCurrentCarryLimitIfPossible();
    }

    private void Start()
    {
        // Retry after Awake so this still works when GameStateSignals initializes later.
        TrySubscribeCarryLimit();
        ApplyCurrentCarryLimitIfPossible();
    }

    private void OnDisable()
    {
        carryLimitSubscription?.Dispose();
        carryLimitSubscription = null;
    }

    public bool IsFull(ResourceType type)
    {
        StackGroup group = GetGroup(type);
        return group == null || group.IsFull;
    }

    public int GetCount(ResourceType type)
    {
        StackGroup group = GetGroup(type);
        return group != null ? group.currentCount : 0;
    }

    public bool TryAdd(ResourceType type)
    {
        StackGroup group = GetGroup(type);

        if (group == null || group.IsFull)
            return false;

        EnsureVisualPool(group);

        SetVisualActive(group, group.currentCount, true);
        group.currentCount++;

        RefreshStackPositions();
        RaiseResourceStateChanged(type);
        return true;
    }

    /// <summary>
    /// Applies a previously reserved stack slot to the visible carry stack.
    /// </summary>
    public bool TryAddReserved(ResourceType type)
    {
        StackGroup group = GetGroup(type);

        if (group == null)
            return false;

        EnsureVisualPool(group);

        // Without a reservation, fall back to the normal add flow.
        if (group.reservedCount <= 0)
            return TryAdd(type);

        if (group.currentCount < 0 || group.currentCount >= group.visuals.Count)
        {
            group.reservedCount--;
            RefreshStackPositions();
            return false;
        }

        SetVisualActive(group, group.currentCount, true);

        group.currentCount++;
        group.reservedCount--;

        RefreshStackPositions();
        RaiseResourceStateChanged(type);
        return true;
    }

    public bool TryRemove(ResourceType type)
    {
        StackGroup group = GetGroup(type);

        if (group == null || group.currentCount <= 0)
            return false;

        group.currentCount--;
        SetVisualActive(group, group.currentCount, false);

        RefreshStackPositions();
        RaiseResourceStateChanged(type);
        return true;
    }

    public void Clear(ResourceType type)
    {
        StackGroup group = GetGroup(type);

        if (group == null)
            return;

        for (int i = 0; i < group.visuals.Count; i++)
        {
            if (group.visuals[i] != null)
                group.visuals[i].SetActive(false);
        }

        group.currentCount = 0;
        group.reservedCount = 0;

        RefreshStackPositions();
        RaiseResourceStateChanged(type);
    }

    /// <summary>
    /// Returns whether the stack can reserve the requested amount before pickup completes.
    /// </summary>
    public bool CanReserve(ResourceType type, int amount)
    {
        StackGroup group = GetGroup(type);

        if (group == null || amount <= 0)
            return false;

        return group.currentCount + group.reservedCount + amount <= group.maxCount;
    }

    /// <summary>
    /// Reserves the next stack slot and returns its world position for pickup animations.
    /// </summary>
    public bool TryReserveNextWorldPosition(
        ResourceType type,
        out int stackIndex,
        out Vector3 position)
    {
        stackIndex = -1;
        position = transform.position;

        StackGroup group = GetGroup(type);

        if (group == null || group.root == null)
            return false;

        if (!CanReserve(type, 1))
            return false;

        EnsureVisualPool(group);

        stackIndex = group.currentCount + group.reservedCount;
        position = group.root.TransformPoint(GetStackLocalPosition(group, stackIndex));

        group.reservedCount++;
        return true;
    }

    /// <summary>
    /// Releases one pending reservation when a pickup flow is cancelled.
    /// </summary>
    public void CancelReserved(ResourceType type)
    {
        StackGroup group = GetGroup(type);

        if (group == null)
            return;

        if (group.reservedCount > 0)
            group.reservedCount--;

        RefreshStackPositions();
    }

    /// <summary>
    /// Returns the world position of a specific stack slot.
    /// </summary>
    public bool TryGetStackWorldPosition(ResourceType type, int stackIndex, out Vector3 position)
    {
        position = transform.position;

        StackGroup group = GetGroup(type);

        if (group == null || group.root == null)
            return false;

        if (stackIndex < 0 || stackIndex >= group.maxCount)
            return false;

        position = group.root.TransformPoint(GetStackLocalPosition(group, stackIndex));
        return true;
    }

    public bool TryGetTopWorldPosition(ResourceType type, out Vector3 position)
    {
        position = transform.position;

        StackGroup group = GetGroup(type);

        if (group == null || group.currentCount <= 0)
            return false;

        int index = group.currentCount - 1;

        if (index < 0 || index >= group.visuals.Count)
            return false;

        position = group.visuals[index].transform.position;
        return true;
    }

    public bool TryGetNextWorldPosition(ResourceType type, out Vector3 position)
    {
        position = transform.position;

        StackGroup group = GetGroup(type);

        if (group == null || group.root == null)
            return false;

        int index = group.currentCount;
        Vector3 localPosition = GetStackLocalPosition(group, index);
        position = group.root.TransformPoint(localPosition);

        return true;
    }

    /// <summary>
    /// Applies the current carry limit to every stack group and hides overflow visuals.
    /// </summary>
    public void ApplyCarryLimit(int carryLimit)
    {
        int safeLimit = Mathf.Max(1, carryLimit);

        for (int i = 0; i < stackGroups.Count; i++)
        {
            StackGroup group = stackGroups[i];

            if (group == null)
                continue;

            SetGroupCapacity(group, safeLimit);
            RaiseResourceStateChanged(group.type);
        }

        RefreshStackPositions();

        Debug.Log($"[PlayerCarryStack] ApplyCarryLimit: {safeLimit}", this);
    }

    private void TrySubscribeCarryLimit()
    {
        if (carryLimitSubscription != null)
            return;

        if (GameStateSignals.Instance == null)
            return;

        carryLimitSubscription = GameStateSignals.Instance.PlayerCarryLimitChanged
            .Subscribe(signal => OnPlayerCarryLimitChanged(signal));
    }

    private void ApplyCurrentCarryLimitIfPossible()
    {
        if (GameStateSignals.Instance == null)
            return;

        ApplyCarryLimit(GameStateSignals.Instance.CurrentCarryLimit);
    }

    private void OnPlayerCarryLimitChanged(PlayerCarryLimitChangedSignal signal)
    {
        ApplyCarryLimit(signal.CarryLimit);
    }

    private void SetGroupCapacity(StackGroup group, int newCapacity)
    {
        if (group == null)
            return;

        group.maxCount = Mathf.Max(1, newCapacity);

        EnsureVisualPool(group);

        // Hide overflow visuals if a runtime limit change reduces capacity.
        if (group.currentCount > group.maxCount)
        {
            for (int i = group.maxCount; i < group.currentCount; i++)
                SetVisualActive(group, i, false);

            group.currentCount = group.maxCount;
        }

        int availableReserveCount = Mathf.Max(0, group.maxCount - group.currentCount);

        if (group.reservedCount > availableReserveCount)
            group.reservedCount = availableReserveCount;
    }

    private void CreateVisualPool()
    {
        for (int i = 0; i < stackGroups.Count; i++)
        {
            StackGroup group = stackGroups[i];

            if (group == null)
                continue;

            EnsureVisualPool(group);
        }
    }

    private void EnsureVisualPool(StackGroup group)
    {
        if (group == null || group.root == null || group.prefab == null)
            return;

        while (group.visuals.Count < group.maxCount)
        {
            int index = group.visuals.Count;

            GameObject visual = Instantiate(group.prefab, group.root);
            visual.transform.localPosition = GetStackLocalPosition(group, index);
            visual.transform.localRotation = Quaternion.identity;
            visual.SetActive(false);

            group.visuals.Add(visual);
        }

        for (int i = 0; i < group.visuals.Count; i++)
        {
            if (group.visuals[i] == null)
                continue;

            group.visuals[i].transform.localPosition = GetStackLocalPosition(group, i);
        }
    }

    private Vector3 GetStackLocalPosition(StackGroup group, int index)
    {
        int columns = Mathf.Max(1, group.columns);

        int column = index % columns;
        int layer = index / columns;

        float startX = -(columns - 1) * group.xSpacing * 0.5f;

        return new Vector3(
            startX + column * group.xSpacing,
            layer * group.ySpacing,
            layer * group.zSpacing
        );
    }

    private void SetVisualActive(StackGroup group, int index, bool active)
    {
        if (group == null)
            return;

        if (index < 0 || index >= group.visuals.Count)
            return;

        if (group.visuals[index] == null)
            return;

        group.visuals[index].SetActive(active);
    }

    private void RefreshStackPositions()
    {
        SetStackPosition(ResourceType.Handcuff, handcuffPosition);
        RefreshBackStackPositions();
    }

    private void RefreshBackStackPositions()
    {
        StackGroup oreGroup = GetGroup(ResourceType.Ore);
        StackGroup moneyGroup = GetGroup(ResourceType.Money);

        bool hasOre = oreGroup != null && oreGroup.HasItem;

        if (oreGroup != null && oreGroup.root != null)
            oreGroup.root.localPosition = backBasePosition;

        if (moneyGroup == null || moneyGroup.root == null)
            return;

        // Push money behind ore to reduce overlap; otherwise use the default back position.
        moneyGroup.root.localPosition = hasOre
            ? backBasePosition + moneyOffsetWhenOreExists
            : backBasePosition;
    }

    private void SetStackPosition(ResourceType type, Vector3 localPosition)
    {
        StackGroup group = GetGroup(type);

        if (group == null || group.root == null)
            return;

        group.root.localPosition = localPosition;
    }

    private StackGroup GetGroup(ResourceType type)
    {
        for (int i = 0; i < stackGroups.Count; i++)
        {
            if (stackGroups[i].type == type)
                return stackGroups[i];
        }

        return null;
    }

    private int GetCapacity(ResourceType type)
    {
        StackGroup group = GetGroup(type);
        return group != null ? group.maxCount : 0;
    }

    /// <summary>
    /// Broadcasts the current amount and capacity for the changed resource type.
    /// </summary>
    private void RaiseResourceStateChanged(ResourceType type)
    {
        GameStateSignals.RaiseResourceAmountChanged(
            ResourceOwnerType.Player,
            stateTargetId,
            type,
            GetCount(type),
            GetCapacity(type)
        );
    }
}