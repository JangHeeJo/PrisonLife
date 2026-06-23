using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

/// <summary>
/// 씬에 배치된 ResourcePoint의 보유 수량을 저장하고 복원하는 컨트롤러입니다.
///
/// 역할:
/// - GameStateSignals의 ResourceDeposited / ResourcePickedUp 이벤트를 구독합니다.
/// - pointId + resourceType 조합을 키로 사용해 각 ResourcePoint의 수량을 저장합니다.
/// - 앱 재시작 시 저장된 수량을 ResourcePoint.TryAddFromAutomation()으로 복원합니다.
///
/// 중요:
/// - Entry의 pointId는 ResourcePoint의 pointTargetId와 일치해야 합니다.
/// - ResourcePoint의 pointTargetId가 비어 있으면 gameObject.name을 대체 ID로 사용합니다.
/// </summary>
public sealed class ResourcePointSaveController : MonoBehaviour
{
    [Serializable]
    private sealed class ResourcePointSaveEntry
    {
        [Tooltip("ResourcePoint의 pointTargetId와 동일하게 입력합니다. 비어 있으면 ResourcePoint의 gameObject.name과 비교합니다.")]
        public string pointId = string.Empty;

        public ResourceType resourceType = default;

        [Tooltip("저장/복원 대상 ResourcePoint입니다.")]
        public ResourcePoint point = null;

        [Tooltip("앱 시작 시 저장된 수량을 이 포인트에 복원할지 여부입니다.")]
        public bool restoreOnStart = true;
    }

    [Header("Entries")]
    [SerializeField] private List<ResourcePointSaveEntry> entries = new();

    [Header("Save")]
    [SerializeField] private bool useSaveData = true;
    [SerializeField] private bool saveImmediatelyOnChanged = true;

    [Header("Debug")]
    [SerializeField] private bool logState = true;

    [Tooltip("등록되지 않은 TargetId 신호가 들어왔을 때 로그를 출력합니다. pointId 매칭 문제를 확인할 때 사용합니다.")]
    [SerializeField] private bool logUnmatchedSignals = true;

    private readonly Dictionary<string, ResourcePointSaveEntry> entryMap = new();
    private readonly Dictionary<string, int> amountCache = new();

    private IDisposable depositedSubscription;
    private IDisposable pickedUpSubscription;

    private bool isRestoring;
    private bool restored;

    private void Awake()
    {
        BuildEntryMap();
    }

    private void OnEnable()
    {
        TrySubscribeSignals();
    }

    private void Start()
    {
        // GameStateSignals가 늦게 생성되는 씬 순서를 대비해 Start에서 한 번 더 구독을 시도합니다.
        TrySubscribeSignals();

        RestoreFromSave();
    }

    private void OnDisable()
    {
        depositedSubscription?.Dispose();
        depositedSubscription = null;

        pickedUpSubscription?.Dispose();
        pickedUpSubscription = null;

        if (SaveManager.Instance != null && SaveManager.Instance.IsResetting)
            return;

        SaveToSaveManager();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
            SaveToSaveManager();
    }

    private void OnApplicationQuit()
    {
        SaveToSaveManager();
    }

    private void BuildEntryMap()
    {
        entryMap.Clear();
        amountCache.Clear();

        for (int i = 0; i < entries.Count; i++)
        {
            ResourcePointSaveEntry entry = entries[i];

            if (entry == null)
                continue;

            if (string.IsNullOrEmpty(entry.pointId))
            {
                Debug.LogWarning("[ResourcePointSaveController] Entry pointId is empty.", this);
                continue;
            }

            string key = BuildKey(entry.pointId, entry.resourceType);

            if (entryMap.ContainsKey(key))
            {
                Debug.LogWarning($"[ResourcePointSaveController] Duplicate entry: {key}", this);
                continue;
            }

            entryMap.Add(key, entry);
            amountCache[key] = 0;

            if (logState)
            {
                Debug.Log(
                    $"[ResourcePointSaveController] Entry Registered. " +
                    $"PointId: {entry.pointId}, Type: {entry.resourceType}, Key: {key}",
                    this
                );
            }
        }
    }

    private void TrySubscribeSignals()
    {
        if (GameStateSignals.Instance == null)
        {
            if (logState)
        // GameStateSignals가 늦게 생성되는 씬 순서를 대비해 Start에서 한 번 더 구독을 시도합니다.

            return;
        }

        if (depositedSubscription == null)
        {
            depositedSubscription = GameStateSignals.Instance.ResourceDeposited
                .Subscribe(OnResourceDeposited);
        }

        if (pickedUpSubscription == null)
        {
            pickedUpSubscription = GameStateSignals.Instance.ResourcePickedUp
                .Subscribe(OnResourcePickedUp);
        }

        if (logState)
            Debug.Log("[ResourcePointSaveController] Subscribed GameStateSignals.", this);
    }

    private void OnResourceDeposited(ResourceTransactionSignal signal)
    {
        if (isRestoring)
            return;

        string key = BuildKey(signal.TargetId, signal.ResourceType);

        if (!amountCache.ContainsKey(key))
        {
            LogUnmatched("Deposited", signal, key);
            return;
        }

        amountCache[key] = Mathf.Max(0, amountCache[key] + signal.Amount);

        if (logState)
        {
            Debug.Log(
                $"[ResourcePointSaveController] Deposited Saved. " +
                $"TargetId: {signal.TargetId}, Type: {signal.ResourceType}, " +
                $"Amount: {signal.Amount}, Cached: {amountCache[key]}",
                this
            );
        }

        SaveIfNeeded();
    }

    private void OnResourcePickedUp(ResourceTransactionSignal signal)
    {
        if (isRestoring)
            return;

        string key = BuildKey(signal.TargetId, signal.ResourceType);

        if (!amountCache.ContainsKey(key))
        {
            LogUnmatched("PickedUp", signal, key);
            return;
        }

        amountCache[key] = Mathf.Max(0, amountCache[key] - signal.Amount);

        if (logState)
        {
            Debug.Log(
                $"[ResourcePointSaveController] PickedUp Saved. " +
                $"TargetId: {signal.TargetId}, Type: {signal.ResourceType}, " +
                $"Amount: {signal.Amount}, Cached: {amountCache[key]}",
                this
            );
        }

        SaveIfNeeded();
    }

    private void RestoreFromSave()
    {
        if (restored)
            return;

        restored = true;

        if (!useSaveData)
            return;

        if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
        {
            if (logState)
                Debug.LogWarning("[ResourcePointSaveController] SaveManager or CurrentData is null. Restore skipped.", this);
            return;
        }

        GameSaveData data = SaveManager.Instance.CurrentData;

        if (data.resourcePoints == null)
            data.resourcePoints = new List<ResourcePointSaveData>();

        for (int i = 0; i < data.resourcePoints.Count; i++)
        {
            ResourcePointSaveData saved = data.resourcePoints[i];

            if (saved == null || string.IsNullOrEmpty(saved.pointId))
                continue;

            string key = BuildKey(saved.pointId, saved.resourceType);

            if (!amountCache.ContainsKey(key))
                continue;

            amountCache[key] = Mathf.Max(0, saved.amount);
        }

        isRestoring = true;

        try
        {
            foreach (KeyValuePair<string, ResourcePointSaveEntry> pair in entryMap)
            {
                ResourcePointSaveEntry entry = pair.Value;

                if (entry == null || !entry.restoreOnStart || entry.point == null)
                    continue;

                int amount = amountCache.TryGetValue(pair.Key, out int savedAmount)
                    ? Mathf.Max(0, savedAmount)
                    : 0;

                if (amount <= 0)
                    continue;

                bool restoredResult = entry.point.TryAddFromAutomation(entry.resourceType, amount);

                if (logState)
                {
                    Debug.Log(
                        $"[ResourcePointSaveController] Restore Point. " +
                        $"PointId: {entry.pointId}, Type: {entry.resourceType}, " +
                        $"Amount: {amount}, Result: {restoredResult}",
                        this
                    );
                }
            }
        }
        finally
        {
            isRestoring = false;
        }
    }

    private void SaveIfNeeded()
    {
        if (!saveImmediatelyOnChanged)
            return;

        SaveToSaveManager();
    }

    private void SaveToSaveManager()
    {
        if (!useSaveData)
            return;

        if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
            return;

        if (SaveManager.Instance.IsResetting)
            return;

        GameSaveData data = SaveManager.Instance.CurrentData;

        if (data.resourcePoints == null)
            data.resourcePoints = new List<ResourcePointSaveData>();

        data.resourcePoints.Clear();

        foreach (KeyValuePair<string, ResourcePointSaveEntry> pair in entryMap)
        {
            ResourcePointSaveEntry entry = pair.Value;

            if (entry == null || string.IsNullOrEmpty(entry.pointId))
                continue;

            int amount = amountCache.TryGetValue(pair.Key, out int cachedAmount)
                ? Mathf.Max(0, cachedAmount)
                : 0;

            data.resourcePoints.Add(new ResourcePointSaveData
            {
                pointId = entry.pointId,
                resourceType = entry.resourceType,
                amount = amount
            });
        }

        if (logState)
            Debug.Log($"[ResourcePointSaveController] Save resource points. Count: {data.resourcePoints.Count}", this);

        SaveManager.Instance.MarkDirtyAndSave();
    }

    private void LogUnmatched(string eventName, ResourceTransactionSignal signal, string key)
    {
        if (!logUnmatchedSignals)
            return;

        Debug.LogWarning(
            $"[ResourcePointSaveController] Unmatched Signal. " +
            $"Event: {eventName}, TargetId: {signal.TargetId}, Type: {signal.ResourceType}, Key: {key}. " +
            $"Check Entry pointId/resourceType.",
            this
        );
    }

    private static string BuildKey(string pointId, ResourceType resourceType)
    {
        return $"{pointId}_{resourceType}";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (entries == null)
            entries = new List<ResourcePointSaveEntry>();
    }
#endif
}
