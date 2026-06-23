using UnityEngine;

/// <summary>
/// IAP 구매 성공 결과를 게임 내 보상 효과로 변환하는 실행기입니다.
///
/// 포트폴리오 관점의 역할:
/// - 결제 SDK와 실제 게임 보상 로직 사이를 분리합니다.
/// - 상품 ID별 효과를 한 곳에서 라우팅합니다.
/// - 씬에 대상 컨트롤러가 없더라도 저장 데이터의 권한 상태는 보존합니다.
/// </summary>
public sealed class IapRewardExecutor : MonoBehaviour
{
    [Header("Effect Targets")]
    [SerializeField] private GoldMultiplierProvider goldMultiplierProvider;
    [SerializeField] private PremiumWorkerUnlockController premiumWorkerUnlockController;

    [Header("Debug")]
    [SerializeField] private bool logState = true;

    private void Awake()
    {
        ResolveEffectTargetsIfNeeded();
    }

    /// <summary>
    /// Unity IAP 또는 Play Billing 연동부에서 구매 검증이 끝난 뒤 호출할 진입점입니다.
    /// </summary>
    public void ExecutePurchasedProduct(string productId)
    {
        if (string.IsNullOrEmpty(productId))
        {
            Debug.LogWarning("[IapRewardExecutor] ProductId is empty.", this);
            return;
        }

        ResolveEffectTargetsIfNeeded();

        switch (productId)
        {
            case IapProductIds.GoldBoostSubscription:
                ExecuteGoldBoostSubscription();
                break;

            case IapProductIds.PremiumWorkerUnlock:
                ExecutePremiumWorkerUnlock();
                break;

            default:
                Debug.LogWarning($"[IapRewardExecutor] Unknown ProductId: {productId}", this);
                break;
        }
    }

    private void ResolveEffectTargetsIfNeeded()
    {
        if (goldMultiplierProvider == null)
            goldMultiplierProvider = FindFirstObjectByType<GoldMultiplierProvider>();

        if (premiumWorkerUnlockController == null)
            premiumWorkerUnlockController = FindFirstObjectByType<PremiumWorkerUnlockController>();
    }

    private void ExecuteGoldBoostSubscription()
    {
        if (goldMultiplierProvider != null)
            goldMultiplierProvider.ActivateSubscription();
        else
            SetEntitlementOnly(IapProductIds.GoldBoostSubscription);

        Log("Gold boost subscription activated.");
    }

    private void ExecutePremiumWorkerUnlock()
    {
        if (premiumWorkerUnlockController != null)
            premiumWorkerUnlockController.UnlockPremiumWorker();
        else
            SetEntitlementOnly(IapProductIds.PremiumWorkerUnlock);

        Log("Premium worker unlocked.");
    }

    /// <summary>
    /// 보상 대상 오브젝트가 없는 씬에서도 구매 권한 자체는 잃지 않도록 저장 데이터만 갱신합니다.
    /// </summary>
    private void SetEntitlementOnly(string productId)
    {
        if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
            return;

        if (SaveManager.Instance.IsResetting)
            return;

        IapEntitlementState.SetActive(
            SaveManager.Instance.CurrentData,
            productId,
            true
        );

        SaveManager.Instance.MarkDirtyAndSave();
    }

    private void Log(string message)
    {
        if (logState)
            Debug.Log($"[IapRewardExecutor] {message}", this);
    }
}
