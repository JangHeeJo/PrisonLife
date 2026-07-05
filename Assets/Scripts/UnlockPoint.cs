using System;
using UnityEngine;

/// <summary>
/// 해금 포인트의 실제 상호작용 로직입니다.
///
/// 변경점:
/// - Money 비용은 PlayerCarryStack의 Money가 아니라 GoldHudView의 저장 골드를 사용합니다.
/// - Ore/Handcuff 같은 다른 자원 비용은 기존처럼 PlayerCarryStack에서 소모합니다.
/// </summary>
public sealed class UnlockPoint : InteractionPointBase
{
    [Header("Runtime State")]
    [SerializeField] private string unlockId;

    [SerializeField] private ResourceType costResourceType = ResourceType.Money;

    [SerializeField, Min(1)] private int costAmount = 1;

    [Header("Gold Currency")]
    [Tooltip("Money 비용 결제에 사용할 GoldHudView입니다. 비워두면 자동 검색합니다.")]
    [SerializeField] private GoldHudView goldHudView;

    [Header("Options")]
    [Tooltip("해금 완료 후 상호작용을 비활성화할지 여부입니다.")]
    [SerializeField] private bool deactivateAfterUnlock = true;

    private Collider pointCollider;

    private int currentPaidAmount;
    private bool isAvailable;
    private bool isUnlocked;

    public event Action<int, int> ProgressChanged;
    public event Action<UnlockPoint> Unlocked;

    public string UnlockId => unlockId;
    public ResourceType CostResourceType => costResourceType;
    public int CostAmount => costAmount;
    public int CurrentPaidAmount => currentPaidAmount;
    public bool IsAvailable => isAvailable;
    public bool IsUnlocked => isUnlocked;

    private void Awake()
    {
        pointCollider = GetComponent<Collider>();

        if (goldHudView == null)
            goldHudView = FindFirstObjectByType<GoldHudView>();

        SetInteractable(false);
    }

    private void Start()
    {
        if (goldHudView == null)
            goldHudView = FindFirstObjectByType<GoldHudView>();
    }

    public void Bind(UnlockPointData data)
    {
        if (data == null)
            return;

        unlockId = data.unlockId;
        costResourceType = data.costResourceType;
        costAmount = Mathf.Max(1, data.costAmount);
        deactivateAfterUnlock = data.deactivateAfterUnlock;

        currentPaidAmount = 0;
        isUnlocked = false;
        isAvailable = false;

        SetInteractable(false);

        ProgressChanged?.Invoke(currentPaidAmount, costAmount);
    }

    public void Reveal()
    {
        if (isUnlocked)
            return;

        isAvailable = true;
        SetInteractable(true);

        ProgressChanged?.Invoke(currentPaidAmount, costAmount);

        GameStateSignals.RaiseUnlockPointStateChanged(
            unlockId,
            UnlockPointState.Available
        );
    }

    public void HidePoint()
    {
        isAvailable = false;
        SetInteractable(false);
    }

    protected override bool TryInteract(PlayerCarryStack playerCarryStack)
    {
        if (!isAvailable || isUnlocked)
            return false;

        if (currentPaidAmount >= costAmount)
            return false;

        if (!TryPayCost(playerCarryStack))
            return false;

        currentPaidAmount++;

        GameStateSignals.RaiseResourceDeposited(
            unlockId,
            costResourceType,
            1
        );

        ProgressChanged?.Invoke(currentPaidAmount, costAmount);

        if (currentPaidAmount >= costAmount)
            CompleteUnlock();

        return true;
    }

    private bool TryPayCost(PlayerCarryStack playerCarryStack)
    {
        if (costResourceType == ResourceType.Money)
            return TryPayMoneyCost(playerCarryStack);

        if (playerCarryStack != null &&
            playerCarryStack.GetCount(costResourceType) > 0 &&
            playerCarryStack.TryRemove(costResourceType))
        {
            return true;
        }

        return false;
    }

    private bool TryPayMoneyCost(PlayerCarryStack playerCarryStack)
    {
        if (goldHudView == null)
            goldHudView = FindFirstObjectByType<GoldHudView>();

        if (goldHudView == null || !goldHudView.TrySpendGold(1))
            return false;

        playerCarryStack?.TryRemove(ResourceType.Money);
        return true;
    }

    private void CompleteUnlock()
    {
        if (isUnlocked)
            return;

        isUnlocked = true;
        isAvailable = false;

        SetInteractable(false);

        GameStateSignals.RaiseUnlockPointStateChanged(
            unlockId,
            UnlockPointState.Unlocked
        );

        Unlocked?.Invoke(this);

        if (deactivateAfterUnlock)
            HidePoint();
    }

    private void SetInteractable(bool active)
    {
        if (pointCollider != null)
            pointCollider.enabled = active;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        costAmount = Mathf.Max(1, costAmount);

        if (string.IsNullOrEmpty(unlockId))
            unlockId = gameObject.name;

        if (pointCollider == null)
            pointCollider = GetComponent<Collider>();

        if (pointCollider != null)
            pointCollider.isTrigger = true;
    }
#endif
}
