using System;
using R3;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays and persists the player's gold.
/// Money pickups are converted into gold, then multiplied by active boosts.
/// The latest earned amount is kept so rewarded ads can grant one extra copy.
/// </summary>
public sealed class GoldHudView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text goldText;

    [Header("Gold Rule")]
    [Tooltip("Gold awarded per 1 Money resource before multipliers.")]
    [SerializeField] private int goldPerMoney = 1;

    [Header("Multiplier")]
    [Tooltip("Leave empty to use GoldMultiplierProvider.Instance.")]
    [SerializeField] private GoldMultiplierProvider goldMultiplierProvider;

    [Header("Save")]
    [Tooltip("Load and save gold through SaveManager.")]
    [SerializeField] private bool useSaveData = true;

    [Tooltip("Save immediately whenever gold changes. Recommended for mobile builds.")]
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
        ResolveMultiplierProvider();
        TryLoadGoldFromSave();
        RefreshText();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        ResolveMultiplierProvider();
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
        if (loadedFromSave || !useSaveData)
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

        int earnedGold = CalculateGoldReward(signal.Amount);
        AddGold(earnedGold);

        lastEarnedGold = earnedGold;
        RewardableGoldChanged?.Invoke(lastEarnedGold);
    }

    private int CalculateGoldReward(int moneyAmount)
    {
        float multiplier = GetCurrentGoldMultiplier();
        int earnedGold = Mathf.RoundToInt(moneyAmount * goldPerMoney * multiplier);
        return Mathf.Max(0, earnedGold);
    }

    private float GetCurrentGoldMultiplier()
    {
        ResolveMultiplierProvider();

        if (goldMultiplierProvider == null)
            return 1f;

        return Mathf.Max(0f, goldMultiplierProvider.CurrentMultiplier);
    }

    private void ResolveMultiplierProvider()
    {
        if (goldMultiplierProvider == null)
            goldMultiplierProvider = GoldMultiplierProvider.Instance;
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

    public bool TryClaimDoubleGoldReward()
    {
        if (lastEarnedGold <= 0)
            return false;

        AddGold(lastEarnedGold);
        ClearRewardableGold();
        return true;
    }

    public void ClearRewardableGold()
    {
        if (lastEarnedGold <= 0)
            return;

        lastEarnedGold = 0;
        RewardableGoldChanged?.Invoke(lastEarnedGold);
    }

    private void SaveGoldIfNeeded()
    {
        if (saveImmediatelyOnChange)
            SaveGold();
    }

    private void SaveGold()
    {
        if (!useSaveData)
            return;

        if (SaveManager.Instance == null || SaveManager.Instance.IsResetting)
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