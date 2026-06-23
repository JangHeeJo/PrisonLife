using UnityEngine;

/// <summary>
/// Play Console/IAP 연동 전 에디터에서 상품 효과를 테스트하기 위한 디버그 버튼용 스크립트입니다.
/// 실제 출시 UI에는 노출하지 않는 것을 권장합니다.
/// </summary>
public sealed class IapDebugPurchaseTester : MonoBehaviour
{
    [SerializeField] private IapRewardExecutor rewardExecutor;

    private void Awake()
    {
        if (!CanRunTestActions())
        {
            enabled = false;
            gameObject.SetActive(false);
            return;
        }

        if (rewardExecutor == null)
            rewardExecutor = FindFirstObjectByType<IapRewardExecutor>();
    }

    public void SimulateGoldBoostPurchase()
    {
        if (!CanRunTestActions())
            return;

        if (rewardExecutor == null)
            return;

        rewardExecutor.ExecutePurchasedProduct(IapProductIds.GoldBoostSubscription);
    }

    public void SimulatePremiumWorkerPurchase()
    {
        if (!CanRunTestActions())
            return;

        if (rewardExecutor == null)
            return;

        rewardExecutor.ExecutePurchasedProduct(IapProductIds.PremiumWorkerUnlock);
    }

    public void ClearGoldBoostForTest()
    {
        if (!CanRunTestActions())
            return;

        GoldMultiplierProvider provider = GoldMultiplierProvider.Instance;

        if (provider != null)
            provider.DeactivateSubscriptionForTest();
    }

    public void ClearPremiumWorkerForTest()
    {
        if (!CanRunTestActions())
            return;

        PremiumWorkerUnlockController controller = FindFirstObjectByType<PremiumWorkerUnlockController>();

        if (controller != null)
            controller.ClearUnlockForTest();
    }

    private static bool CanRunTestActions()
    {
        return Debug.isDebugBuild || Application.isEditor;
    }
}
