using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 무기/채굴 도구 업그레이드를 관리합니다.
/// 
/// 역할:
/// - PlayerWeaponUpgradeData.txt를 읽습니다.
/// - WeaponLevelId에 맞는 MiningStats와 WeaponVisualId를 찾습니다.
/// - PlayerMiningController에 채굴 스탯을 적용합니다.
/// - PlayerWeaponView에 무기 모델 교체를 요청합니다.
/// - 저장된 무기 레벨을 불러오고, 업그레이드 시 저장합니다.
/// 
/// 배치:
/// - Player 오브젝트가 아니라 Managers 오브젝트에 붙이는 것을 권장합니다.
/// </summary>
public sealed class PlayerWeaponUpgradeController : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("PlayerWeaponUpgradeData.txt")]
    [SerializeField] private TextAsset weaponUpgradeTable;

    [SerializeField] private int currentCarryLimit;
    public int CurrentCarryLimit => currentCarryLimit;

    [Header("Default")]
    [Tooltip("게임 시작 시 기본으로 적용할 무기 레벨입니다.")]
    [SerializeField] private string defaultWeaponLevelId = "WeaponLevel_00";

    [Tooltip("Start 시 저장 데이터가 없을 때 기본 무기를 자동 적용할지 여부입니다.")]
    [SerializeField] private bool applyDefaultOnStart = true;

    [Header("Save")]
    [Tooltip("true면 SaveManager에서 무기 업그레이드 상태를 로드/저장합니다.")]
    [SerializeField] private bool useSaveData = true;

    [Tooltip("업그레이드 적용 시 즉시 저장합니다.")]
    [SerializeField] private bool saveImmediatelyOnUpgrade = true;

    [Header("Targets")]
    [SerializeField] private PlayerMiningController playerMiningController;
    [SerializeField] private PlayerWeaponView playerWeaponView;

    [Header("Debug State")]
    [SerializeField] private string currentWeaponLevelId;
    [SerializeField] private string currentWeaponVisualId;
    [SerializeField] private int currentWeaponLevelValue;

    private PlayerWeaponUpgradeModel model;

    public event Action<string, int> WeaponUpgraded;

    public string CurrentWeaponLevelId => currentWeaponLevelId;
    public string CurrentWeaponVisualId => currentWeaponVisualId;
    public int CurrentWeaponLevelValue => currentWeaponLevelValue;

    private void Awake()
    {
        LoadData();
        AutoFindTargetsIfNeeded();
    }

    private void Start()
    {
        if (TryApplySavedWeapon())
            return;

        if (applyDefaultOnStart)
            ApplyDefaultWeapon();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
            SaveCurrentWeapon();
    }

    private void OnApplicationQuit()
    {
        SaveCurrentWeapon();
    }

    private void LoadData()
    {
        List<PlayerWeaponUpgradeData> loadedData =
            PlayerWeaponUpgradeTableLoader.Load(weaponUpgradeTable);

        model = new PlayerWeaponUpgradeModel(loadedData);

        Debug.Log(
            $"[PlayerWeaponUpgradeController] Loaded weapon upgrade data count: {loadedData.Count}",
            this
        );
    }

    private void AutoFindTargetsIfNeeded()
    {
        if (playerMiningController == null)
            playerMiningController = FindFirstObjectByType<PlayerMiningController>();

        if (playerWeaponView == null)
            playerWeaponView = FindFirstObjectByType<PlayerWeaponView>();
    }

    /// <summary>
    /// 저장된 무기가 있으면 저장된 무기를 우선 적용합니다.
    /// 저장 데이터가 없으면 false를 반환해서 기본 무기를 적용하게 합니다.
    /// </summary>
    private bool TryApplySavedWeapon()
    {
        if (!useSaveData)
            return false;

        if (SaveManager.Instance == null)
            return false;

        GameSaveData data = SaveManager.Instance.CurrentData;

        if (data == null)
            return false;

        if (string.IsNullOrEmpty(data.weaponLevelId))
            return false;

        int savedLevelValue = Mathf.Max(0, data.weaponLevel);

        ApplyUpgradeInternal(
            data.weaponLevelId,
            savedLevelValue,
            saveAfterApply: false
        );

        Debug.Log(
            $"[PlayerWeaponUpgradeController] Saved weapon loaded. " +
            $"LevelId: {data.weaponLevelId}, LevelValue: {savedLevelValue}",
            this
        );

        return true;
    }

    /// <summary>
    /// 게임 시작 시 기본 무기를 적용합니다.
    /// 예: WeaponLevel_00 = 기본 곡괭이
    /// </summary>
    public void ApplyDefaultWeapon()
    {
        ApplyUpgradeInternal(
            defaultWeaponLevelId,
            0,
            saveAfterApply: true
        );
    }

    /// <summary>
    /// 해금 결과로 들어온 무기 업그레이드를 적용합니다.
    /// 
    /// 예:
    /// UnlockPoint_Weapon_01 완료
    /// → ResultTargetId = WeaponLevel_01
    /// → 드릴 장착
    /// </summary>
    public void ApplyUpgrade(string weaponLevelId, int levelValue)
    {
        ApplyUpgradeInternal(
            weaponLevelId,
            levelValue,
            saveAfterApply: true
        );
    }

    private void ApplyUpgradeInternal(
        string weaponLevelId,
        int levelValue,
        bool saveAfterApply)
    {
        if (string.IsNullOrEmpty(weaponLevelId))
        {
            Debug.LogWarning("[PlayerWeaponUpgradeController] WeaponLevelId is empty.", this);
            return;
        }

        if (model == null)
        {
            Debug.LogWarning("[PlayerWeaponUpgradeController] Model is null.", this);
            return;
        }

        if (!model.TryGet(weaponLevelId, out PlayerWeaponUpgradeData data))
        {
            Debug.LogWarning(
                $"[PlayerWeaponUpgradeController] Weapon upgrade data not found: {weaponLevelId}",
                this
            );
            return;
        }

        currentWeaponLevelId = data.weaponLevelId;
        currentWeaponVisualId = data.weaponVisualId;
        currentWeaponLevelValue = Mathf.Max(0, levelValue);
        currentCarryLimit = data.miningStats.carryLimit;

        // CarryLimit 변경 신호를 발행하면 PlayerCarryStack, ResourcePoint 등이 기존 구독 구조로 자동 갱신됩니다.
        GameStateSignals.RaisePlayerCarryLimitChanged(currentCarryLimit);

        if (playerMiningController != null)
        {
            playerMiningController.ApplyMiningStats(data.miningStats);
        }
        else
        {
            Debug.LogWarning("[PlayerWeaponUpgradeController] PlayerMiningController is null.", this);
        }

        if (playerWeaponView != null)
        {
            PlayerMiningDetector detector = playerWeaponView.ApplyVisual(data.weaponVisualId);

            if (playerMiningController != null)
                playerMiningController.SetDetector(detector);
        }
        else
        {
            Debug.LogWarning("[PlayerWeaponUpgradeController] PlayerWeaponView is null.", this);
        }

        if (saveAfterApply)
            SaveCurrentWeaponIfNeeded();

        Debug.Log(
            $"[PlayerWeaponUpgradeController] ApplyUpgrade. " +
            $"LevelId: {data.weaponLevelId}, " +
            $"VisualId: {data.weaponVisualId}, " +
            $"CarryLimit: {currentCarryLimit}",
            this
        );

        WeaponUpgraded?.Invoke(data.weaponLevelId, currentWeaponLevelValue);
    }

    private void SaveCurrentWeaponIfNeeded()
    {
        if (!saveImmediatelyOnUpgrade)
            return;

        SaveCurrentWeapon();
    }

    private void SaveCurrentWeapon()
    {
        if (!useSaveData)
            return;

        if (SaveManager.Instance == null)
            return;

        GameSaveData data = SaveManager.Instance.CurrentData;

        if (data == null)
            return;

        if (string.IsNullOrEmpty(currentWeaponLevelId))
            return;

        data.weaponLevelId = currentWeaponLevelId;
        data.weaponLevel = currentWeaponLevelValue;
        data.carryLimit = currentCarryLimit;
        data.pointCapacity = currentCarryLimit * 2;

        SaveManager.Instance.MarkDirtyAndSave();
    }
}