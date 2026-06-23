using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 감옥 수용 인원 상태를 관리합니다.
///
/// 저장:
/// - 현재 수감 인원
/// - 최대 수용량
/// - PrisonFull 신호 발행 여부
/// </summary>
public sealed class PrisonAreaState : MonoBehaviour
{
    [Header("Id")]
    [SerializeField] private string prisonId = "Prison_01";

    [Header("Capacity")]
    [SerializeField] private int currentCount;
    [SerializeField] private int maxCount = 20;

    [Header("UI")]
    [SerializeField] private TMP_Text countText;

    [Header("Option")]
    [Tooltip("true면 maxCount 도달 시 PrisonFull 신호를 한 번만 발행합니다.")]
    [SerializeField] private bool raiseFullOnlyOnce = true;

    [Header("Save")]
    [SerializeField] private bool useSaveData = true;
    [SerializeField] private bool saveImmediatelyOnChanged = true;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private bool hasRaisedFull;
    private bool restoredFromSave;

    public string PrisonId => prisonId;
    public int CurrentCount => currentCount;
    public int MaxCount => maxCount;

    public bool IsFull => maxCount > 0 && currentCount >= maxCount;
    public bool HasFreeSpace => maxCount > 0 && currentCount < maxCount;

    private void Awake()
    {
        maxCount = Mathf.Max(1, maxCount);
        currentCount = Mathf.Clamp(currentCount, 0, maxCount);

        RefreshUI();
    }

    private void Start()
    {
        RestoreFromSave();

        RefreshUI();
        RaiseStateChanged();
    }

    private void OnDisable()
    {
        SaveCurrentState();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
            SaveCurrentState();
    }

    private void OnApplicationQuit()
    {
        SaveCurrentState();
    }

    /// <summary>
    /// NPC가 감옥에 들어왔을 때 호출합니다.
    /// </summary>
    public void AddPrisoner(int amount = 1)
    {
        if (amount <= 0)
            return;

        if (IsFull)
        {
            if (logState)
                Debug.Log($"[PrisonAreaState] Already full. {currentCount}/{maxCount}", this);

            return;
        }

        currentCount = Mathf.Clamp(currentCount + amount, 0, maxCount);

        RefreshUI();
        RaiseStateChanged();
        SaveCurrentStateIfNeeded();

        if (logState)
            Debug.Log($"[PrisonAreaState] AddPrisoner: {currentCount}/{maxCount}", this);
    }

    /// <summary>
    /// 감옥 확장으로 최대 수용량을 증가시킵니다.
    /// 예: 20/20 상태에서 +20 하면 20/40이 됩니다.
    /// </summary>
    public void AddCapacity(int amount)
    {
        if (amount <= 0)
            return;

        maxCount += amount;

        // 확장 후에는 다시 가득 찰 수 있으므로 full 발행 플래그를 초기화합니다.
        hasRaisedFull = false;

        RefreshUI();
        RaiseStateChanged();
        SaveCurrentStateIfNeeded();

        if (logState)
            Debug.Log($"[PrisonAreaState] AddCapacity: {currentCount}/{maxCount}", this);
    }

    public void SetCapacity(int newMaxCount)
    {
        maxCount = Mathf.Max(1, newMaxCount);
        currentCount = Mathf.Clamp(currentCount, 0, maxCount);

        hasRaisedFull = false;

        RefreshUI();
        RaiseStateChanged();
        SaveCurrentStateIfNeeded();
    }

    public void SetState(int newCurrentCount, int newMaxCount, bool newHasRaisedFull)
    {
        maxCount = Mathf.Max(1, newMaxCount);
        currentCount = Mathf.Clamp(newCurrentCount, 0, maxCount);
        hasRaisedFull = newHasRaisedFull;

        RefreshUI();
        RaiseStateChanged();
        SaveCurrentStateIfNeeded();
    }

    private void RefreshUI()
    {
        if (countText == null)
        {
            if (logState)
                Debug.LogWarning("[PrisonAreaState] Count Text is null.", this);

            return;
        }

        countText.text = $"{currentCount}/{maxCount}";
    }

    private void RaiseStateChanged()
    {
        if (IsFull && raiseFullOnlyOnce && hasRaisedFull)
            return;

        GameStateSignals.RaisePrisonStateChanged(
            prisonId,
            currentCount,
            maxCount
        );

        if (IsFull)
        {
            hasRaisedFull = true;

            if (logState)
                Debug.Log($"[PrisonAreaState] Prison Full: {prisonId}", this);

            SaveCurrentStateIfNeeded();
        }
    }

    private void RestoreFromSave()
    {
        if (restoredFromSave)
            return;

        restoredFromSave = true;

        if (!useSaveData)
            return;

        if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
            return;

        GameSaveData data = SaveManager.Instance.CurrentData;

        if (data.prisonAreas == null)
            return;

        for (int i = 0; i < data.prisonAreas.Count; i++)
        {
            PrisonAreaSaveData saved = data.prisonAreas[i];

            if (saved == null)
                continue;

            if (saved.prisonId != prisonId)
                continue;

            maxCount = Mathf.Max(1, saved.maxCount);
            currentCount = Mathf.Clamp(saved.currentCount, 0, maxCount);
            hasRaisedFull = saved.hasRaisedFull;

            if (logState)
            {
                Debug.Log(
                    $"[PrisonAreaState] Restored. " +
                    $"PrisonId: {prisonId}, Count: {currentCount}/{maxCount}, HasRaisedFull: {hasRaisedFull}",
                    this
                );
            }

            break;
        }
    }

    private void SaveCurrentStateIfNeeded()
    {
        if (!saveImmediatelyOnChanged)
            return;

        SaveCurrentState();
    }

    private void SaveCurrentState()
    {
        if (!useSaveData)
            return;

        if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
            return;

        if (SaveManager.Instance.IsResetting)
            return;

        GameSaveData data = SaveManager.Instance.CurrentData;

        if (data.prisonAreas == null)
            data.prisonAreas = new List<PrisonAreaSaveData>();

        PrisonAreaSaveData target = null;

        for (int i = 0; i < data.prisonAreas.Count; i++)
        {
            if (data.prisonAreas[i] == null)
                continue;

            if (data.prisonAreas[i].prisonId == prisonId)
            {
                target = data.prisonAreas[i];
                break;
            }
        }

        if (target == null)
        {
            target = new PrisonAreaSaveData
            {
                prisonId = prisonId
            };

            data.prisonAreas.Add(target);
        }

        target.currentCount = currentCount;
        target.maxCount = maxCount;
        target.hasRaisedFull = hasRaisedFull;

        SaveManager.Instance.MarkDirtyAndSave();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxCount = Mathf.Max(1, maxCount);
        currentCount = Mathf.Clamp(currentCount, 0, maxCount);

        if (countText != null)
            countText.text = $"{currentCount}/{maxCount}";
    }

    [ContextMenu("Test Add Prisoner")]
    private void TestAddPrisoner()
    {
        AddPrisoner(1);
    }

    [ContextMenu("Test Add Capacity")]
    private void TestAddCapacity()
    {
        AddCapacity(20);
    }
#endif
}
