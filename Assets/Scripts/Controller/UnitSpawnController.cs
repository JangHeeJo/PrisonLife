using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛/NPC 생성 공통 컨트롤러입니다.
///
/// 역할:
/// - UnitId로 프리팹을 찾습니다.
/// - 지정된 SpawnPoint에 유닛을 생성합니다.
/// - 생성 직후 필요한 Scene 참조를 유닛에게 주입합니다.
/// - 생성된 자동화 유닛 수량을 저장하고, 앱 재실행 시 복원합니다.
/// </summary>
public sealed class UnitSpawnController : MonoBehaviour
{
    [Serializable]
    private sealed class UnitSpawnEntry
    {
        [Tooltip("데이터의 ResultTargetId와 매칭됩니다. 예: AutoMinerWorker, HandcuffDeliveryWorker")]
        public string unitId = string.Empty;

        [Tooltip("생성할 유닛 프리팹입니다.")]
        public GameObject prefab = null;

        [Tooltip("생성된 유닛들을 정리할 부모 Transform입니다. 비워두면 이 컨트롤러 하위에 생성됩니다.")]
        public Transform parent = null;

        [Tooltip("유닛이 생성될 위치들입니다. 여러 개면 순서대로 사용합니다.")]
        public List<Transform> spawnPoints = new();

        [Tooltip("true면 SpawnPoint를 순환 사용합니다.")]
        public bool useRoundRobin = true;

        [Header("Auto Miner Context")]
        [Tooltip("AutoMinerWorker가 채굴할 실제 MineableOre 목록입니다.")]
        public MineableOre[] oreTargets = Array.Empty<MineableOre>();

        [Tooltip("AutoMinerWorker가 캐낸 광석을 넣을 OreDepositPoint의 ResourcePoint입니다.")]
        public ResourcePoint oreDepositPoint = null;

        [Header("Handcuff Delivery Context")]
        [Tooltip("HandcuffDeliveryWorker가 수갑을 가져갈 PickupPoint입니다.")]
        public ResourcePoint handcuffPickupPoint = null;

        [Tooltip("HandcuffDeliveryWorker가 수갑을 내려놓을 DepositPoint입니다.")]
        public ResourcePoint handcuffDepositPoint = null;

        [HideInInspector] public int nextSpawnIndex;
    }

    [Header("Unit Registry")]
    [SerializeField] private List<UnitSpawnEntry> unitEntries = new();

    [Header("Carry Limit")]
    [Tooltip("현재 플레이어 CarryLimit을 제공하는 컨트롤러입니다.")]
    [SerializeField] private PlayerWeaponUpgradeController weaponUpgradeController;

    [Tooltip("WeaponUpgradeController를 찾지 못했을 때 사용할 기본 자동화 유닛 한도입니다.")]
    [SerializeField] private int fallbackAutomationCarryLimit = 10;

    [Header("Save")]
    [Tooltip("true면 SaveManager에서 자동화 유닛 생성 수량을 로드/저장합니다.")]
    [SerializeField] private bool useSaveData = true;

    [Tooltip("true면 앱 시작 시 저장된 자동화 유닛을 다시 생성합니다.")]
    [SerializeField] private bool restoreSavedUnitsOnStart = true;

    [Tooltip("true면 유닛 생성 시 즉시 저장합니다.")]
    [SerializeField] private bool saveImmediatelyOnSpawn = true;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private readonly Dictionary<string, UnitSpawnEntry> entryMap = new();

    private bool restoredFromSave;

    private void Awake()
    {
        if (weaponUpgradeController == null)
            weaponUpgradeController = FindFirstObjectByType<PlayerWeaponUpgradeController>();

        BuildEntryMap();
    }

    private void Start()
    {
        RestoreSpawnedUnitsFromSave();
    }

    /// <summary>
    /// 특정 UnitId의 유닛을 count만큼 생성합니다.
    /// UnlockResultExecutor에서 호출하는 공개 API입니다.
    /// </summary>
    public List<GameObject> SpawnUnits(string unitId, int count)
    {
        return SpawnUnitsInternal(unitId, count, saveAfterSpawn: true);
    }

    /// <summary>
    /// 저장 복원용입니다.
    /// 저장된 수량을 다시 생성하되, 복원 중에는 저장 수량을 다시 증가시키지 않습니다.
    /// </summary>
    private List<GameObject> SpawnUnitsInternal(string unitId, int count, bool saveAfterSpawn)
    {
        List<GameObject> spawnedUnits = new();

        if (string.IsNullOrEmpty(unitId))
        {
            Debug.LogWarning("[UnitSpawnController] UnitId is empty.", this);
            return spawnedUnits;
        }

        int safeCount = Mathf.Max(0, count);

        if (safeCount <= 0)
        {
            Debug.LogWarning($"[UnitSpawnController] Spawn count is zero. UnitId: {unitId}", this);
            return spawnedUnits;
        }

        if (!entryMap.TryGetValue(unitId, out UnitSpawnEntry entry))
        {
            Debug.LogWarning($"[UnitSpawnController] Unit entry not found. UnitId: {unitId}", this);
            return spawnedUnits;
        }

        if (entry.prefab == null)
        {
            Debug.LogWarning($"[UnitSpawnController] Prefab is null. UnitId: {unitId}", this);
            return spawnedUnits;
        }

        for (int i = 0; i < safeCount; i++)
        {
            GameObject spawned = SpawnOne(entry);

            if (spawned != null)
                spawnedUnits.Add(spawned);
        }

        if (saveAfterSpawn && spawnedUnits.Count > 0)
            AddSpawnedUnitToSave(unitId, spawnedUnits.Count);

        if (logState)
            Debug.Log($"[UnitSpawnController] SpawnUnits: {unitId}, Count: {spawnedUnits.Count}, Save: {saveAfterSpawn}", this);

        return spawnedUnits;
    }

    /// <summary>
    /// 유닛 1개를 생성하고, 필요한 런타임 컨텍스트를 주입합니다.
    /// </summary>
    private GameObject SpawnOne(UnitSpawnEntry entry)
    {
        Transform spawnPoint = GetSpawnPoint(entry);

        Vector3 spawnPosition = spawnPoint != null
            ? spawnPoint.position
            : transform.position;

        Quaternion spawnRotation = spawnPoint != null
            ? spawnPoint.rotation
            : Quaternion.identity;

        Transform spawnParent = entry.parent != null
            ? entry.parent
            : transform;

        GameObject unit = Instantiate(
            entry.prefab,
            spawnPosition,
            spawnRotation,
            spawnParent
        );

        unit.SetActive(true);

        InjectSpawnContext(unit, entry);

        return unit;
    }

    /// <summary>
    /// 생성된 유닛에게 Scene 참조를 전달합니다.
    /// </summary>
    private void InjectSpawnContext(GameObject unit, UnitSpawnEntry entry)
    {
        if (unit == null || entry == null)
            return;

        int carryLimit = GetCurrentAutomationCarryLimit();

        UnitSpawnContext context = new UnitSpawnContext(
            entry.unitId,
            entry.oreTargets,
            entry.oreDepositPoint,
            entry.handcuffPickupPoint,
            entry.handcuffDepositPoint,
            carryLimit
        );

        EnsurePremiumWorkerContextReceiver(unit, entry);

        MonoBehaviour[] behaviours = unit.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IUnitSpawnContextReceiver receiver)
                receiver.Initialize(context);
        }
    }

    private static void EnsurePremiumWorkerContextReceiver(GameObject unit, UnitSpawnEntry entry)
    {
        if (!string.Equals(entry.unitId, "PremiumWorker", StringComparison.OrdinalIgnoreCase))
            return;

        if (unit.GetComponentInChildren<HandcuffDeliveryWorkerUnit>(true) != null)
            return;

        if (unit.GetComponent<UnitCarryStack>() == null)
            unit.AddComponent<UnitCarryStack>();

        unit.AddComponent<HandcuffDeliveryWorkerUnit>();
    }

    private int GetCurrentAutomationCarryLimit()
    {
        // 앱 재실행 복원 타이밍에서는 PlayerWeaponUpgradeController.Start보다
        // UnitSpawnController.Start가 먼저 실행될 수 있습니다.
        // 그래서 저장된 carryLimit이 있으면 그 값을 우선 사용합니다.
        if (SaveManager.Instance != null &&
            SaveManager.Instance.CurrentData != null &&
            SaveManager.Instance.CurrentData.carryLimit > 0)
        {
            return Mathf.Max(1, SaveManager.Instance.CurrentData.carryLimit);
        }

        if (weaponUpgradeController != null)
            return Mathf.Max(1, weaponUpgradeController.CurrentCarryLimit);

        return Mathf.Max(1, fallbackAutomationCarryLimit);
    }

    private Transform GetSpawnPoint(UnitSpawnEntry entry)
    {
        if (entry.spawnPoints == null || entry.spawnPoints.Count <= 0)
            return null;

        int index = Mathf.Clamp(entry.nextSpawnIndex, 0, entry.spawnPoints.Count - 1);
        Transform point = entry.spawnPoints[index];

        if (entry.useRoundRobin)
            entry.nextSpawnIndex = (entry.nextSpawnIndex + 1) % entry.spawnPoints.Count;

        return point;
    }

    private void BuildEntryMap()
    {
        entryMap.Clear();

        for (int i = 0; i < unitEntries.Count; i++)
        {
            UnitSpawnEntry entry = unitEntries[i];

            if (entry == null)
                continue;

            if (string.IsNullOrEmpty(entry.unitId))
                continue;

            if (entryMap.ContainsKey(entry.unitId))
            {
                Debug.LogWarning($"[UnitSpawnController] Duplicate UnitId: {entry.unitId}", this);
                continue;
            }

            entryMap.Add(entry.unitId, entry);
        }
    }

    /// <summary>
    /// 저장된 자동화 유닛 수량을 다시 생성합니다.
    /// completedUnlockIds 복원과는 별개로, 실제 런타임 유닛을 씬에 다시 만들어주는 역할입니다.
    /// </summary>
    private void RestoreSpawnedUnitsFromSave()
    {
        if (restoredFromSave)
            return;

        restoredFromSave = true;

        if (!useSaveData || !restoreSavedUnitsOnStart)
            return;

        if (SaveManager.Instance == null)
            return;

        GameSaveData data = SaveManager.Instance.CurrentData;

        if (data == null || data.spawnedUnits == null)
            return;

        for (int i = 0; i < data.spawnedUnits.Count; i++)
        {
            SpawnedUnitSaveData unitData = data.spawnedUnits[i];

            if (unitData == null)
                continue;

            if (string.IsNullOrEmpty(unitData.unitId) || unitData.count <= 0)
                continue;

            SpawnUnitsInternal(
                unitData.unitId,
                unitData.count,
                saveAfterSpawn: false
            );
        }

        if (logState)
            Debug.Log($"[UnitSpawnController] Restored spawned units. Count: {data.spawnedUnits.Count}", this);
    }

    private void AddSpawnedUnitToSave(string unitId, int count)
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

        if (data.spawnedUnits == null)
            data.spawnedUnits = new List<SpawnedUnitSaveData>();

        SpawnedUnitSaveData target = null;

        for (int i = 0; i < data.spawnedUnits.Count; i++)
        {
            if (data.spawnedUnits[i] == null)
                continue;

            if (data.spawnedUnits[i].unitId == unitId)
            {
                target = data.spawnedUnits[i];
                break;
            }
        }

        if (target == null)
        {
            target = new SpawnedUnitSaveData
            {
                unitId = unitId,
                count = 0
            };

            data.spawnedUnits.Add(target);
        }

        target.count += Mathf.Max(0, count);

        if (saveImmediatelyOnSpawn)
            SaveManager.Instance.MarkDirtyAndSave();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (unitEntries == null)
            unitEntries = new List<UnitSpawnEntry>();

        fallbackAutomationCarryLimit = Mathf.Max(1, fallbackAutomationCarryLimit);
    }
#endif
}
