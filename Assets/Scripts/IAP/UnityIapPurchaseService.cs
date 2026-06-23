using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

/// <summary>
/// Bridges Google Play Billing/Unity IAP purchases to the existing game reward flow.
/// Editor and development builds can still use IapDebugPurchaseTester for instant test rewards.
/// </summary>
public sealed class UnityIapPurchaseService : MonoBehaviour, IDetailedStoreListener
{
    private static UnityIapPurchaseService instance;

    [Header("Reward")]
    [SerializeField] private IapRewardExecutor rewardExecutor;

    [Header("Startup")]
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private bool logState = true;

    private IStoreController storeController;
    private IExtensionProvider extensionProvider;
    private bool isInitializing;

    public static UnityIapPurchaseService Instance => instance;

    private bool IsInitialized => storeController != null && extensionProvider != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureServiceExistsOnSceneLoad()
    {
        GetOrCreate();
    }

    public static UnityIapPurchaseService GetOrCreate()
    {
        if (instance != null)
            return instance;

        UnityIapPurchaseService existing = FindFirstObjectByType<UnityIapPurchaseService>();
        if (existing != null)
            return existing;

        GameObject serviceObject = new GameObject(nameof(UnityIapPurchaseService));
        DontDestroyOnLoad(serviceObject);
        return serviceObject.AddComponent<UnityIapPurchaseService>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveRewardExecutorIfNeeded();
    }

    private void Start()
    {
        if (initializeOnStart)
            InitializePurchasing();
    }

    public void BuyGoldBoostSubscription()
    {
        BuyProduct(IapProductIds.GoldBoostSubscription);
    }

    public void BuyPremiumWorkerUnlock()
    {
        BuyProduct(IapProductIds.PremiumWorkerUnlock);
    }

    public void BuyProduct(string productId)
    {
        if (string.IsNullOrEmpty(productId))
        {
            Debug.LogWarning("[UnityIapPurchaseService] Product id is empty.", this);
            return;
        }

        if (!IsInitialized)
        {
            InitializePurchasing();
            Debug.LogWarning($"[UnityIapPurchaseService] Store is not ready yet. Try again after initialization: {productId}", this);
            return;
        }

        Product product = storeController.products.WithID(productId);
        if (product == null || !product.availableToPurchase)
        {
            Debug.LogWarning($"[UnityIapPurchaseService] Product is not available: {productId}", this);
            return;
        }

        Log($"Starting purchase: {productId}");
        storeController.InitiatePurchase(product);
    }

    public void InitializePurchasing()
    {
        if (IsInitialized || isInitializing)
            return;

        isInitializing = true;
        ConfigurationBuilder builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
        builder.AddProduct(IapProductIds.GoldBoostSubscription, ProductType.Subscription);
        builder.AddProduct(IapProductIds.PremiumWorkerUnlock, ProductType.NonConsumable);

        UnityPurchasing.Initialize(this, builder);
    }

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        storeController = controller;
        extensionProvider = extensions;
        isInitializing = false;
        Log("Unity IAP initialized.");
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        string productId = args.purchasedProduct.definition.id;
        ResolveRewardExecutorIfNeeded();

        if (rewardExecutor != null)
            rewardExecutor.ExecutePurchasedProduct(productId);
        else
            Debug.LogWarning($"[UnityIapPurchaseService] Purchase completed, but reward executor is missing: {productId}", this);

        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        string productId = product != null ? product.definition.id : "unknown";
        Debug.LogWarning($"[UnityIapPurchaseService] Purchase failed: {productId} / {failureDescription.reason} / {failureDescription.message}", this);
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        string productId = product != null ? product.definition.id : "unknown";
        Debug.LogWarning($"[UnityIapPurchaseService] Purchase failed: {productId} / {failureReason}", this);
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        isInitializing = false;
        Debug.LogWarning($"[UnityIapPurchaseService] Initialization failed: {error}", this);
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        isInitializing = false;
        Debug.LogWarning($"[UnityIapPurchaseService] Initialization failed: {error} / {message}", this);
    }

    private void ResolveRewardExecutorIfNeeded()
    {
        if (rewardExecutor == null)
            rewardExecutor = FindFirstObjectByType<IapRewardExecutor>();
    }

    private void Log(string message)
    {
        if (logState)
            Debug.Log($"[UnityIapPurchaseService] {message}", this);
    }
}