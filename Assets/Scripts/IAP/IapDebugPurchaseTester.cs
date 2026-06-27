using UnityEngine;

/// <summary>
/// Purchase button bridge.
/// Editor/development builds grant rewards instantly for testing; release builds open the real store flow.
/// </summary>
public sealed class IapDebugPurchaseTester : MonoBehaviour
{
    [SerializeField] private IapRewardExecutor rewardExecutor;
    [SerializeField] private UnityIapPurchaseService purchaseService;

    private void Awake()
    {
        if (!CanRunTestActions())
        {
            gameObject.SetActive(false);
            return;
        }

        ResolveReferencesIfNeeded();
    }

    public void SimulateGoldBoostPurchase()
    {
        if (CanRunTestActions())
        {
            ExecuteDebugReward(IapProductIds.GoldBoostSubscription);
            return;
        }

        ResolveReferencesIfNeeded();
        purchaseService?.BuyGoldBoostSubscription();
    }

    public void SimulatePremiumWorkerPurchase()
    {
        if (CanRunTestActions())
        {
            ExecuteDebugReward(IapProductIds.PremiumWorkerUnlock);
            return;
        }

        ResolveReferencesIfNeeded();
        purchaseService?.BuyPremiumWorkerUnlock();
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

    private void ExecuteDebugReward(string productId)
    {
        ResolveReferencesIfNeeded();

        if (rewardExecutor != null)
            rewardExecutor.ExecutePurchasedProduct(productId);
    }

    private void ResolveReferencesIfNeeded()
    {
        if (rewardExecutor == null)
            rewardExecutor = FindFirstObjectByType<IapRewardExecutor>();

        if (purchaseService == null)
            purchaseService = UnityIapPurchaseService.GetOrCreate();
    }

    private static bool CanRunTestActions()
    {
        return Debug.isDebugBuild || Application.isEditor;
    }
}
