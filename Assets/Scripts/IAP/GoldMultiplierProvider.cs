using System;
using UnityEngine;

/// <summary>
/// Owns the game's gold earning multiplier policy.
///
/// Paid subscription and rewarded-ad boosts intentionally share the same public
/// multiplier so reward calculation code does not need to know where the boost
/// came from. Persistence stays here as well, which keeps GoldHudView focused on
/// currency display and arithmetic.
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

    // Reward calculation only asks for the current multiplier.
    // It does not need to know whether the boost came from IAP or a rewarded ad.
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
        GameSaveData data = GetWritableSaveData();
        isSubscriptionActive = data != null && IapEntitlementState.IsActive(data, productId);
        adBoostExpiresAtUtcTicks = data != null ? Math.Max(0L, data.goldBoostAdExpiresAtUtcTicks) : 0L;
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

        // If the ad boost is already active, extend it from the current expiry time.
        DateTime startTime = currentExpiry > now ? currentExpiry : now;
        adBoostExpiresAtUtcTicks = startTime.Add(duration).Ticks;
        wasTimedBoostActive = true;

        SaveAdBoostExpiry();

        if (logState)
            Debug.Log($"[GoldMultiplierProvider] Ad boost granted until {AdBoostExpiresAtUtc:O}", this);

        NotifyMultiplierChanged();
    }

    private void SetSubscriptionActive(bool active, bool save)
    {
        bool changed = isSubscriptionActive != active;
        isSubscriptionActive = active;

        if (save)
            SaveSubscriptionState();

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
        SaveAdBoostExpiry();
    }

    private void SaveSubscriptionState()
    {
        GameSaveData data = GetWritableSaveData();
        if (data == null)
            return;

        // Subscription ownership is stored as an entitlement so it can be restored on app restart.
        IapEntitlementState.SetActive(data, productId, isSubscriptionActive);
        SaveManager.Instance.MarkDirtyAndSave();
    }

    private void SaveAdBoostExpiry()
    {
        GameSaveData data = GetWritableSaveData();
        if (data == null)
            return;

        // Timed ad boosts persist by expiry ticks instead of remaining seconds.
        data.goldBoostAdExpiresAtUtcTicks = adBoostExpiresAtUtcTicks;
        SaveManager.Instance.MarkDirtyAndSave();
    }

    private static GameSaveData GetWritableSaveData()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.IsResetting)
            return null;

        return SaveManager.Instance.CurrentData;
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
