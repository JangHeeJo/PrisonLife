using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

/// <summary>
/// Runtime boundary between Unity IAP / Google Play Billing and game rewards.
///
/// Responsibilities:
/// - owns store initialization and product registration,
/// - queues a purchase request made before the store is ready,
/// - validates product availability before opening the native purchase sheet,
/// - forwards completed purchases to IapRewardExecutor.
/// </summary>
public sealed class UnityIapPurchaseService : MonoBehaviour, IDetailedStoreListener
{
    private readonly struct ProductRegistration
    {
        public ProductRegistration(string productId, ProductType productType)
        {
            ProductId = productId;
            ProductType = productType;
        }

        public string ProductId { get; }
        public ProductType ProductType { get; }
    }

    private static readonly ProductRegistration[] ProductCatalog =
    {
        new ProductRegistration(IapProductIds.GoldBoostSubscription, ProductType.Subscription),
        new ProductRegistration(IapProductIds.PremiumWorkerUnlock, ProductType.NonConsumable)
    };

    private static UnityIapPurchaseService instance;

    [Header("Reward")]
    [SerializeField] private IapRewardExecutor rewardExecutor;

    [Header("Startup")]
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private bool logState;

    private IStoreController storeController;
    private IExtensionProvider extensionProvider;
    private bool isInitializing;
    private string pendingProductId;

    public static UnityIapPurchaseService Instance => instance;
    public bool HasPendingPurchase => !string.IsNullOrEmpty(pendingProductId);

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

        // A gameplay popup can request a purchase before Unity IAP finishes initialization.
        // Queue that product and retry automatically after OnInitialized.
        if (!IsInitialized)
        {
            QueuePurchaseUntilInitialized(productId);
            return;
        }

        if (!TryGetPurchasableProduct(productId, out Product product))
            return;

        Log($"Starting purchase: {productId}");
        storeController.InitiatePurchase(product);
    }

    public void InitializePurchasing()
    {
        if (IsInitialized || isInitializing)
            return;

        isInitializing = true;
        ConfigurationBuilder builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
        RegisterProducts(builder);

        UnityPurchasing.Initialize(this, builder);
    }

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        storeController = controller;
        extensionProvider = extensions;
        isInitializing = false;
        Log("Unity IAP initialized.");

        TryStartPendingPurchase();
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        string productId = args.purchasedProduct.definition.id;
        ResolveRewardExecutorIfNeeded();

        // Billing success is converted into gameplay rewards by IapRewardExecutor.
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
        pendingProductId = null;
        Debug.LogWarning($"[UnityIapPurchaseService] Initialization failed: {error}", this);
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        isInitializing = false;
        pendingProductId = null;
        Debug.LogWarning($"[UnityIapPurchaseService] Initialization failed: {error} / {message}", this);
    }

    private void TryStartPendingPurchase()
    {
        if (string.IsNullOrEmpty(pendingProductId))
            return;

        string productId = pendingProductId;
        pendingProductId = null;
        BuyProduct(productId);
    }

    private void QueuePurchaseUntilInitialized(string productId)
    {
        pendingProductId = productId;
        InitializePurchasing();
        Log($"Store is initializing. Purchase queued: {productId}");
    }

    private bool TryGetPurchasableProduct(string productId, out Product product)
    {
        product = storeController.products.WithID(productId);
        if (product != null && product.availableToPurchase)
            return true;

        Debug.LogWarning($"[UnityIapPurchaseService] Product is not available: {productId}", this);
        return false;
    }

    private static void RegisterProducts(ConfigurationBuilder builder)
    {
        for (int i = 0; i < ProductCatalog.Length; i++)
        {
            ProductRegistration product = ProductCatalog[i];
            // Product type must match the product configured in Google Play Console.
            builder.AddProduct(product.ProductId, product.ProductType);
        }
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
