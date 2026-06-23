using System;
using UnityEngine;

/// <summary>
/// 골드 획득 배율을 제공하는 컨트롤러입니다.
///
/// IAP 구독 또는 광고 보상 부스터가 활성화되어 있으면
/// 골드 획득량 1.5배 효과를 적용합니다.
/// </summary>
public sealed class GoldMultiplierProvider : MonoBehaviour
{
    private const double AdBoostDurationHours = 24d;

    public static GoldMultiplierProvider Instance { get; private set; }

    [Header("Product")]
    [SerializeField] private string productId = IapProductIds.GoldBoostSubscription;

    [Header("Multiplier")]
    [SerializeField] private float defaultMultiplier = 1f;
    [SerializeField] private float subscriptionMultiplier = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private bool isSubscriptionActive;
    private long adBoostExpiresAtUtcTicks;
    private bool wasTimedBoostActive;

    public bool IsSubscriptionActive => isSubscriptionActive;
    public bool IsAdBoostActive => IsTimedBoostActive(DateTime.UtcNow);
    public DateTime AdBoostExpiresAtUtc => new DateTime(Math.Max(0L, adBoostExpiresAtUtcTicks), DateTimeKind.Utc);

    public float CurrentMultiplier =>
        IsSubscriptionActive || IsAdBoostActive ? subscriptionMultiplier : defaultMultiplier;

    public event Action<float> MultiplierChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        RefreshFromSave();
    }

    private void Update()
    {
        bool timedBoostActive = IsAdBoostActive;
        if (wasTimedBoostActive == timedBoostActive)
            return;

        wasTimedBoostActive = timedBoostActive;

        if (!timedBoostActive)
            ClearExpiredAdBoost();

        NotifyMultiplierChanged();
    }

    public void RefreshFromSave()
    {
        bool active = false;
        long expiresAtTicks = 0L;

        if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
        {
            GameSaveData data = SaveManager.Instance.CurrentData;
            active = IapEntitlementState.IsActive(data, productId);
            expiresAtTicks = Math.Max(0L, data.goldBoostAdExpiresAtUtcTicks);
        }

        isSubscriptionActive = active;
        adBoostExpiresAtUtcTicks = expiresAtTicks;
        wasTimedBoostActive = IsAdBoostActive;
        ClearExpiredAdBoost();
        NotifyMultiplierChanged();
    }

    public void ActivateSubscription()
    {
        SetSubscriptionActive(true, save: true);
    }

    public void GrantAdBoostForOneDay()
    {
        GrantTimedAdBoost(TimeSpan.FromHours(AdBoostDurationHours));
    }

    public void DeactivateSubscriptionForTest()
    {
        if (!CanRunTestActions())
            return;

        SetSubscriptionActive(false, save: true);
    }

    private static bool CanRunTestActions()
    {
        return Debug.isDebugBuild || Application.isEditor;
    }

    private void GrantTimedAdBoost(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            return;

        DateTime now = DateTime.UtcNow;
        DateTime currentExpiry = adBoostExpiresAtUtcTicks > 0L
            ? new DateTime(adBoostExpiresAtUtcTicks, DateTimeKind.Utc)
            : now;

        DateTime startTime = currentExpiry > now ? currentExpiry : now;
        adBoostExpiresAtUtcTicks = startTime.Add(duration).Ticks;
        wasTimedBoostActive = true;

        if (SaveManager.Instance != null &&
            SaveManager.Instance.CurrentData != null &&
            !SaveManager.Instance.IsResetting)
        {
            SaveManager.Instance.CurrentData.goldBoostAdExpiresAtUtcTicks = adBoostExpiresAtUtcTicks;
            SaveManager.Instance.MarkDirtyAndSave();
        }

        if (logState)
            Debug.Log($"[GoldMultiplierProvider] Ad boost granted until {AdBoostExpiresAtUtc:O}", this);

        NotifyMultiplierChanged();
    }

    private void SetSubscriptionActive(bool active, bool save)
    {
        bool changed = isSubscriptionActive != active;
        isSubscriptionActive = active;

        if (save &&
            SaveManager.Instance != null &&
            SaveManager.Instance.CurrentData != null &&
            !SaveManager.Instance.IsResetting)
        {
            IapEntitlementState.SetActive(
                SaveManager.Instance.CurrentData,
                productId,
                isSubscriptionActive
            );

            SaveManager.Instance.MarkDirtyAndSave();
        }

        if (logState)
        {
            Debug.Log(
                $"[GoldMultiplierProvider] Subscription: {isSubscriptionActive}, AdBoost: {IsAdBoostActive}, Multiplier: {CurrentMultiplier}",
                this
            );
        }

        if (changed || save)
            NotifyMultiplierChanged();
    }

    private bool IsTimedBoostActive(DateTime now)
    {
        return adBoostExpiresAtUtcTicks > now.Ticks;
    }

    private void ClearExpiredAdBoost()
    {
        if (adBoostExpiresAtUtcTicks <= 0L || IsAdBoostActive)
            return;

        adBoostExpiresAtUtcTicks = 0L;

        if (SaveManager.Instance != null &&
            SaveManager.Instance.CurrentData != null &&
            !SaveManager.Instance.IsResetting)
        {
            SaveManager.Instance.CurrentData.goldBoostAdExpiresAtUtcTicks = 0L;
            SaveManager.Instance.MarkDirtyAndSave();
        }
    }

    private void NotifyMultiplierChanged()
    {
        MultiplierChanged?.Invoke(CurrentMultiplier);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        defaultMultiplier = Mathf.Max(0f, defaultMultiplier);
        subscriptionMultiplier = Mathf.Max(defaultMultiplier, subscriptionMultiplier);

        if (string.IsNullOrEmpty(productId))
            productId = IapProductIds.GoldBoostSubscription;
    }
#endif
}