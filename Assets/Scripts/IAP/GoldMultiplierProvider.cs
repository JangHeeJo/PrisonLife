using System;
using UnityEngine;

/// <summary>
/// 골드 획득 배율을 제공하는 컨트롤러입니다.
///
/// 현재는 IAP 구독 활성화 여부를 SaveData에서 읽어
/// 골드 획득량 1.5배 효과를 적용합니다.
/// </summary>
public sealed class GoldMultiplierProvider : MonoBehaviour
{
    public static GoldMultiplierProvider Instance { get; private set; }

    [Header("Product")]
    [SerializeField] private string productId = IapProductIds.GoldBoostSubscription;

    [Header("Multiplier")]
    [SerializeField] private float defaultMultiplier = 1f;
    [SerializeField] private float subscriptionMultiplier = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private bool isSubscriptionActive;

    public bool IsSubscriptionActive => isSubscriptionActive;

    public float CurrentMultiplier =>
        isSubscriptionActive ? subscriptionMultiplier : defaultMultiplier;

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

    public void RefreshFromSave()
    {
        bool active = false;

        if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
            active = IapEntitlementState.IsActive(SaveManager.Instance.CurrentData, productId);

        SetSubscriptionActive(active, save: false);
    }

    public void ActivateSubscription()
    {
        SetSubscriptionActive(true, save: true);
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

    private void SetSubscriptionActive(bool active, bool save)
    {
        if (isSubscriptionActive == active && !save)
            return;

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
                $"[GoldMultiplierProvider] Active: {isSubscriptionActive}, Multiplier: {CurrentMultiplier}",
                this
            );
        }

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
