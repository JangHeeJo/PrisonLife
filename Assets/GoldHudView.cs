using System;
using R3;
using TMPro;
using UnityEngine;

/// <summary>
/// 골드 UI를 관리합니다.
///
/// 역할:
/// - 저장된 골드를 불러와 UI에 표시합니다.
/// - Money 획득 시 골드를 증가시킵니다.
/// - IAP 골드 배율이 활성화되어 있으면 획득 골드에 배율을 적용합니다.
/// - 최근 획득 골드를 저장해서 광고 2배 보상에 사용합니다.
/// - 골드가 변경될 때 SaveManager에 저장합니다.
/// </summary>
public sealed class GoldHudView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text goldText;

    [Header("Gold Rule")]
    [Tooltip("Money 1개당 증가할 골드 수량입니다.")]
    [SerializeField] private int goldPerMoney = 1;

    [Header("IAP Multiplier")]
    [Tooltip("비워두면 GoldMultiplierProvider.Instance를 사용합니다.")]
    [SerializeField] private GoldMultiplierProvider goldMultiplierProvider;

    [Header("Save")]
    [Tooltip("true면 SaveManager에서 골드를 로드/저장합니다.")]
    [SerializeField] private bool useSaveData = true;

    [Tooltip("골드가 바뀔 때마다 즉시 저장합니다. 모바일 출시용으로는 true 추천.")]
    [SerializeField] private bool saveImmediatelyOnChange = true;

    [Header("Debug")]
    [SerializeField] private int currentGold;
    [SerializeField] private int lastEarnedGold;

    private IDisposable subscription;
    private bool loadedFromSave;

    public int CurrentGold => currentGold;
    public int LastEarnedGold => lastEarnedGold;
    public bool HasRewardableGold => lastEarnedGold > 0;

    public event Action<int> GoldChanged;
    public event Action<int> RewardableGoldChanged;

    private void Awake()
    {
        if (goldMultiplierProvider == null)
            goldMultiplierProvider = GoldMultiplierProvider.Instance;

        TryLoadGoldFromSave();
        RefreshText();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        if (goldMultiplierProvider == null)
            goldMultiplierProvider = GoldMultiplierProvider.Instance;

        // SaveManager / GameStateSignals 생성 순서 대비
        TryLoadGoldFromSave();
        TrySubscribe();
        RefreshText();
    }

    private void OnDisable()
    {
        subscription?.Dispose();
        subscription = null;

        SaveGold();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
            SaveGold();
    }

    private void OnApplicationQuit()
    {
        SaveGold();
    }

    private void TrySubscribe()
    {
        if (subscription != null)
            return;

        if (GameStateSignals.Instance == null)
            return;

        subscription = GameStateSignals.Instance.ResourcePickedUp
            .Subscribe(OnResourcePickedUp);
    }

    private void TryLoadGoldFromSave()
    {
        if (loadedFromSave)
            return;

        if (!useSaveData)
            return;

        if (SaveManager.Instance == null)
            return;

        GameSaveData data = SaveManager.Instance.CurrentData;

        if (data == null)
            return;

        currentGold = Mathf.Max(0, data.gold);
        loadedFromSave = true;
    }

    private void OnResourcePickedUp(ResourceTransactionSignal signal)
    {
        if (signal.ResourceType != ResourceType.Money)
            return;

        float multiplier = GetCurrentGoldMultiplier();

        int earnedGold = Mathf.RoundToInt(signal.Amount * goldPerMoney * multiplier);
        earnedGold = Mathf.Max(0, earnedGold);

        // 기본 골드는 즉시 지급합니다.
        AddGold(earnedGold);

        // 광고 2배 보상용으로 최근 획득 골드량을 저장합니다.
        // 배율이 적용된 최종 획득량 기준으로 2배 보상을 줍니다.
        lastEarnedGold = earnedGold;
        RewardableGoldChanged?.Invoke(lastEarnedGold);
    }

    private float GetCurrentGoldMultiplier()
    {
        if (goldMultiplierProvider == null)
            goldMultiplierProvider = GoldMultiplierProvider.Instance;

        if (goldMultiplierProvider == null)
            return 1f;

        return Mathf.Max(0f, goldMultiplierProvider.CurrentMultiplier);
    }

    public void AddGold(int amount)
    {
        if (amount <= 0)
            return;

        currentGold += amount;

        RefreshText();
        SaveGoldIfNeeded();

        GoldChanged?.Invoke(currentGold);
    }

    public bool TrySpendGold(int amount)
    {
        if (amount <= 0)
            return false;

        if (currentGold < amount)
            return false;

        currentGold -= amount;

        RefreshText();
        SaveGoldIfNeeded();

        GoldChanged?.Invoke(currentGold);

        return true;
    }

    public void SetGold(int amount)
    {
        currentGold = Mathf.Max(0, amount);

        RefreshText();
        SaveGoldIfNeeded();

        GoldChanged?.Invoke(currentGold);
    }

    /// <summary>
    /// 광고 성공 시 최근 획득 골드만큼 추가 지급합니다.
    /// 예: 기본 +100 지급 후 광고 성공 시 추가 +100 지급.
    /// </summary>
    public bool TryClaimDoubleGoldReward()
    {
        if (lastEarnedGold <= 0)
            return false;

        AddGold(lastEarnedGold);
        ClearRewardableGold();

        return true;
    }

    /// <summary>
    /// 광고를 보지 않거나, 보상 선택 UI를 닫았을 때 호출합니다.
    /// 기본 골드는 이미 지급된 상태이므로 lastEarnedGold만 초기화합니다.
    /// </summary>
    public void ClearRewardableGold()
    {
        if (lastEarnedGold <= 0)
            return;

        lastEarnedGold = 0;
        RewardableGoldChanged?.Invoke(lastEarnedGold);
    }

    private void SaveGoldIfNeeded()
    {
        if (!saveImmediatelyOnChange)
            return;

        SaveGold();
    }

    private void SaveGold()
    {
        if (!useSaveData)
            return;

        if (SaveManager.Instance == null)
            return;

        if (SaveManager.Instance.IsResetting)
            return;

        GameSaveData data = SaveManager.Instance.CurrentData;

        if (data == null)
            return;

        data.gold = currentGold;
        SaveManager.Instance.MarkDirtyAndSave();
    }

    private void RefreshText()
    {
        if (goldText == null)
            return;

        goldText.text = currentGold.ToString();
    }
}
