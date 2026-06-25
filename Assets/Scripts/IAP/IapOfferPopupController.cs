using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows one-time IAP offer popups at natural gameplay moments.
/// - First Money pickup: gold boost subscription offer.
/// - First worker unlock deposit: premium worker unlock offer.
/// </summary>
public sealed class IapOfferPopupController : MonoBehaviour
{
    private enum OfferType
    {
        None,
        GoldBoost,
        PremiumWorker
    }

    private static IapOfferPopupController instance;


    [Header("Timing")]
    [SerializeField, Min(0f)] private float firstMoneyOfferDelay = 0.35f;
    [SerializeField, Min(0f)] private float workerDepositOfferDelay = 0.15f;

    [Header("Worker Unlock Detection")]
    [SerializeField]
    private string[] workerUnlockIdKeywords =
    {
        "AutoMiner",
        "HandcuffDelivery",
        "Worker"
    };

    [Header("Debug")]
    [SerializeField] private bool logState;
    [SerializeField] private bool ignoreSavedOfferHistoryInDebugBuilds = true;

    private readonly CompositeDisposable disposables = new();

    private Canvas canvas;
    private GameObject root;
    private TMP_Text titleText;
    private TMP_Text messageText;
    private Button buyButton;
    private Button closeButton;
    private TMP_Text buyButtonText;
    private TMP_Text closeButtonText;

    private bool subscribed;
    private bool goldOfferShownThisSession;
    private bool workerOfferShownThisSession;
    private bool isShowing;
    private OfferType currentOffer;
    private float pendingShowTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (instance != null)
            return;

        IapOfferPopupController existing = FindFirstObjectByType<IapOfferPopupController>();
        if (existing != null)
            return;

        GameObject controllerObject = new GameObject(nameof(IapOfferPopupController));
        DontDestroyOnLoad(controllerObject);
        controllerObject.AddComponent<IapOfferPopupController>();
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
        BuildPopupIfNeeded();
        HidePopup();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void Update()
    {
        if (!subscribed)
            TrySubscribe();

        if (currentOffer == OfferType.None || isShowing)
            return;

        if (Time.unscaledTime < pendingShowTime)
            return;

        ShowPopup(currentOffer);
    }

    private void OnDisable()
    {
        disposables.Clear();
        subscribed = false;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        disposables.Dispose();
    }


    public static void ResetRuntimeStateForNewGame()
    {
        if (instance == null)
            return;

        instance.goldOfferShownThisSession = false;
        instance.workerOfferShownThisSession = false;
        instance.HidePopup();
    }

    private void TrySubscribe()
    {
        if (subscribed)
            return;

        if (GameStateSignals.Instance == null)
            return;

        disposables.Add(
            GameStateSignals.Instance.ResourcePickedUp
                .Subscribe(OnResourcePickedUp)
        );

        disposables.Add(
            GameStateSignals.Instance.ResourceDeposited
                .Subscribe(OnResourceDeposited)
        );

        subscribed = true;
        Log("Subscribed to gameplay signals.");
    }

    private void OnResourcePickedUp(ResourceTransactionSignal signal)
    {
        if (signal.ResourceType != ResourceType.Money)
            return;

        GameSaveData data = GetSaveData();
        if (data == null)
            return;

        if (HasAlreadyShownGoldOffer(data))
            return;

        if (IsEntitlementActive(IapProductIds.GoldBoostSubscription))
        {
            MarkGoldOfferShown(data);
            return;
        }

        MarkGoldOfferShown(data);
        goldOfferShownThisSession = true;
        QueueOffer(OfferType.GoldBoost, firstMoneyOfferDelay);
    }

    private void OnResourceDeposited(ResourceTransactionSignal signal)
    {
        if (signal.ResourceType != ResourceType.Money)
            return;

        if (!IsWorkerUnlockPoint(signal.TargetId))
            return;

        GameSaveData data = GetSaveData();
        if (data == null)
            return;

        if (HasAlreadyShownWorkerOffer(data))
            return;

        if (IsEntitlementActive(IapProductIds.PremiumWorkerUnlock))
        {
            MarkWorkerOfferShown(data);
            return;
        }

        MarkWorkerOfferShown(data);
        workerOfferShownThisSession = true;
        QueueOffer(OfferType.PremiumWorker, workerDepositOfferDelay);
    }

    private void QueueOffer(OfferType offerType, float delay)
    {
        if (isShowing)
            return;

        currentOffer = offerType;
        pendingShowTime = Time.unscaledTime + Mathf.Max(0f, delay);
        Log($"Queued offer: {offerType}");
    }

    private void ShowPopup(OfferType offerType)
    {
        BuildPopupIfNeeded();

        if (root == null)
            return;

        currentOffer = offerType;
        isShowing = true;

        switch (offerType)
        {
            case OfferType.GoldBoost:
                titleText.text = "Gold Boost";
                messageText.text = "Boost all gold earnings by 1.5x?";
                buyButtonText.text = "Buy";
                break;

            case OfferType.PremiumWorker:
                titleText.text = "Premium Worker";
                messageText.text = "Unlock a premium worker for faster automation?";
                buyButtonText.text = "Unlock";
                break;

            default:
                HidePopup();
                return;
        }

        closeButtonText.text = "Later";
        root.SetActive(true);
        Log($"Showing offer: {offerType}");
    }

    private void OnBuyClicked()
    {
        UnityIapPurchaseService purchaseService = UnityIapPurchaseService.GetOrCreate();

        switch (currentOffer)
        {
            case OfferType.GoldBoost:
                purchaseService.BuyGoldBoostSubscription();
                break;

            case OfferType.PremiumWorker:
                purchaseService.BuyPremiumWorkerUnlock();
                break;
        }

        HidePopup();
    }

    private void HidePopup()
    {
        if (root != null)
            root.SetActive(false);

        isShowing = false;
        currentOffer = OfferType.None;
    }

    private void BuildPopupIfNeeded()
    {
        if (root != null)
            return;

        Canvas parentCanvas = FindFirstObjectByType<Canvas>();
        if (parentCanvas != null)
        {
            canvas = parentCanvas;
        }
        else
        {
            GameObject canvasObject = new GameObject("IapOfferCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasObject);

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        root = new GameObject("IapOfferPopup", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image overlay = root.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.55f);

        GameObject panel = CreateUiObject("Panel", root.transform, typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(560f, 360f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.09f, 0.11f, 0.96f);

        titleText = CreateText("Title", panel.transform, 34, FontStyles.Bold, TextAlignmentOptions.Center);
        SetTopStretch(titleText.rectTransform, 32f, 34f, 60f);

        messageText = CreateText("Message", panel.transform, 24, FontStyles.Normal, TextAlignmentOptions.Center);
        SetTopStretch(messageText.rectTransform, 42f, 112f, 100f);

        buyButton = CreateButton("BuyButton", panel.transform, new Color(0.1f, 0.46f, 1f, 1f));
        SetBottomButton(buyButton.GetComponent<RectTransform>(), -120f, 76f, 200f, 60f);
        buyButtonText = CreateButtonText("BuyText", buyButton.transform);
        buyButton.onClick.AddListener(OnBuyClicked);

        closeButton = CreateButton("CloseButton", panel.transform, new Color(0.22f, 0.23f, 0.25f, 1f));
        SetBottomButton(closeButton.GetComponent<RectTransform>(), 120f, 76f, 200f, 60f);
        closeButtonText = CreateButtonText("CloseText", closeButton.transform);
        closeButton.onClick.AddListener(HidePopup);
    }

    private static GameObject CreateUiObject(string objectName, Transform parent, params Type[] components)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);

        for (int i = 0; i < components.Length; i++)
            gameObject.AddComponent(components[i]);

        return gameObject;
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        int fontSize,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Button CreateButton(string objectName, Transform parent, Color color)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent, typeof(Image), typeof(Button));
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        return buttonObject.GetComponent<Button>();
    }

    private static TMP_Text CreateButtonText(string objectName, Transform parent)
    {
        TMP_Text text = CreateText(objectName, parent, 22, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero);
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax, Vector2 anchor)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor == Vector2.zero ? Vector2.one : anchor;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetTopStretch(RectTransform rect, float horizontalPadding, float top, float height)
    {
        rect.anchorMin = Vector2.up;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(horizontalPadding, -top - height);
        rect.offsetMax = new Vector2(-horizontalPadding, -top);
    }

    private static void SetBottomButton(RectTransform rect, float centerX, float bottom, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(centerX, bottom);
        rect.sizeDelta = new Vector2(width, height);
    }

    private bool IsWorkerUnlockPoint(string unlockId)
    {
        if (string.IsNullOrEmpty(unlockId))
            return false;

        if (workerUnlockIdKeywords == null || workerUnlockIdKeywords.Length == 0)
            return unlockId.IndexOf("Worker", StringComparison.OrdinalIgnoreCase) >= 0;

        for (int i = 0; i < workerUnlockIdKeywords.Length; i++)
        {
            string keyword = workerUnlockIdKeywords[i];
            if (string.IsNullOrEmpty(keyword))
                continue;

            if (unlockId.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private bool HasAlreadyShownGoldOffer(GameSaveData data)
    {
        if (ShouldIgnoreSavedOfferHistory())
            return goldOfferShownThisSession;

        return data.shownGoldBoostFirstMoneyOffer;
    }

    private bool HasAlreadyShownWorkerOffer(GameSaveData data)
    {
        if (ShouldIgnoreSavedOfferHistory())
            return workerOfferShownThisSession;

        return data.shownPremiumWorkerFirstDepositOffer;
    }

    private bool ShouldIgnoreSavedOfferHistory()
    {
        return ignoreSavedOfferHistoryInDebugBuilds &&
               (Application.isEditor || Debug.isDebugBuild);
    }

    private static GameSaveData GetSaveData()
    {
        if (SaveManager.Instance == null)
            return null;

        return SaveManager.Instance.CurrentData;
    }

    private static bool IsEntitlementActive(string productId)
    {
        GameSaveData data = GetSaveData();
        return data != null && IapEntitlementState.IsActive(data, productId);
    }

    private static void MarkGoldOfferShown(GameSaveData data)
    {
        data.shownGoldBoostFirstMoneyOffer = true;
        SaveManager.Instance?.MarkDirtyAndSave();
    }

    private static void MarkWorkerOfferShown(GameSaveData data)
    {
        data.shownPremiumWorkerFirstDepositOffer = true;
        SaveManager.Instance?.MarkDirtyAndSave();
    }

    private void Log(string message)
    {
        if (logState)
            Debug.Log($"[IapOfferPopupController] {message}", this);
    }
}