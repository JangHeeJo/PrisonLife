using System.Collections.Generic;
using R3;
using UnityEngine;

/// <summary>
/// 데이터 테이블을 기반으로 월드 해금 포인트의 공개, 완료, 저장 복원을 관리하는 진행 매니저입니다.
///
/// 역할:
/// - UnlockPointProgressionData.txt를 로드해 데이터 기반 언락 모델을 구성합니다.
/// - 씬에 배치된 UnlockPointSlot들을 SlotId 기준으로 찾습니다.
/// - GameStateSignals를 구독해 자원 획득, 감옥 포화, 해금 완료 같은 게임 이벤트를 언락 조건으로 변환합니다.
/// - RevealGroupId 기준으로 여러 UnlockPoint를 동시에 공개합니다.
/// - 공개된 UnlockPoint마다 Presenter를 생성해 UI/월드 오브젝트 표시 책임을 분리합니다.
/// - 해금 완료 시 다음 RevealGroupId가 있으면 다음 그룹을 공개합니다.
/// - 저장된 RevealGroup / Completed UnlockPoint 상태를 복원해 재접속 후에도 진행도를 유지합니다.
/// </summary>
public sealed class UnlockProgressionManager : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("UnlockPointProgressionData.txt TextAsset을 연결합니다.")]
    [SerializeField] private TextAsset progressionTable;

    [Tooltip("IconId를 실제 Sprite로 변환하는 데이터베이스입니다.")]
    [SerializeField] private GameIconDatabase iconDatabase;

    [Header("Result")]
    [SerializeField] private UnlockResultExecutor resultExecutor;

    [Header("Signals")]
    [Tooltip("씬의 GameStateSignals입니다. 비워두면 Instance를 자동 참조합니다.")]
    [SerializeField] private GameStateSignals gameStateSignals;

    [Header("Slots")]
    [Tooltip("씬에 미리 배치된 UnlockPointSlot 목록입니다.")]
    [SerializeField] private List<UnlockPointSlot> slots = new List<UnlockPointSlot>();

    [Header("Save")]
    [Tooltip("true면 SaveManager에서 UnlockPoint 진행 상태를 로드/저장합니다.")]
    [SerializeField] private bool useSaveData = true;

    [Tooltip("RevealGroup 공개 / Unlock 완료 시 즉시 저장합니다.")]
    [SerializeField] private bool saveImmediately = true;

    [Header("Test")]
    [Tooltip("Play 시작 시 테스트 그룹을 바로 공개할지 여부입니다. 출시용은 false 권장입니다.")]
    [SerializeField] private bool revealTestGroupOnStart;

    [Tooltip("테스트로 공개할 RevealGroupId입니다. 예: Reveal_FirstMoney")]
    [SerializeField] private string testRevealGroupId = "Reveal_FirstMoney";

    private UnlockProgressionModel model;

    private readonly Dictionary<string, UnlockPointSlot> slotMap = new();
    private readonly Dictionary<string, UnlockPointPresenter> activePresentersByUnlockId = new();
    private readonly Dictionary<string, UnlockPointPresenter> activePresentersBySlotId = new();

    private readonly HashSet<string> revealedGroupIds = new();
    private readonly HashSet<string> completedUnlockIds = new();

    private readonly TutorialDisposableGroup disposables = new();

    private bool restoredFromSave;

    private void Awake()
    {
        if (gameStateSignals == null)
            gameStateSignals = GameStateSignals.Instance;

        LoadData();
        BuildSlotMap();
        DisableAllSlots();
    }

    private void OnEnable()
    {
        if (gameStateSignals == null)
            gameStateSignals = GameStateSignals.Instance;

        SubscribeSignals();
    }

    private void Start()
    {
        RestoreUnlockProgressFromSave();

        if (revealTestGroupOnStart && CanRunTestFeatures())
            RevealGroup(testRevealGroupId);
    }

    private void OnDisable()
    {
        disposables.Clear();
        SaveUnlockProgress();
    }

    private void OnDestroy()
    {
        disposables.Dispose();
        DisposeAllPresenters();
    }

    [ContextMenu("Reveal Test Group")]
    public void RevealTestGroup()
    {
        if (!CanRunTestFeatures())
            return;

        RevealGroup(testRevealGroupId);
    }

    private static bool CanRunTestFeatures()
    {
        return Debug.isDebugBuild || Application.isEditor;
    }

    private void LoadData()
    {
        List<UnlockPointData> loadedData = UnlockPointProgressionTableLoader.Load(progressionTable);
        model = new UnlockProgressionModel(loadedData);

        Debug.Log($"[UnlockProgressionManager] Loaded unlock data count: {loadedData.Count}", this);
    }

    private void BuildSlotMap()
    {
        slotMap.Clear();

        for (int i = 0; i < slots.Count; i++)
        {
            UnlockPointSlot slot = slots[i];

            if (slot == null)
                continue;

            if (string.IsNullOrEmpty(slot.SlotId))
            {
                Debug.LogWarning("[UnlockProgressionManager] SlotId is empty.", slot);
                continue;
            }

            if (slotMap.ContainsKey(slot.SlotId))
            {
                Debug.LogWarning($"[UnlockProgressionManager] Duplicate SlotId: {slot.SlotId}", slot);
                continue;
            }

            slotMap.Add(slot.SlotId, slot);
        }
    }

    /// <summary>
    /// OnEnable마다 이벤트 구독을 재구성합니다.
    /// 오브젝트가 비활성화됐다가 다시 켜져도 진행 이벤트를 놓치지 않도록 이전 구독을 먼저 정리합니다.
    /// </summary>
    private void SubscribeSignals()
    {
        if (gameStateSignals == null)
        {
            Debug.LogWarning("[UnlockProgressionManager] GameStateSignals is null.", this);
            return;
        }

        disposables.Clear();

        disposables.Add(
            gameStateSignals.ResourceAmountChanged
                .Where(signal => signal.OwnerType == ResourceOwnerType.Player)
                .Subscribe(OnPlayerResourceAmountChanged)
        );

        disposables.Add(
            gameStateSignals.PrisonFull
                .Subscribe(OnPrisonFull)
        );

        disposables.Add(
            gameStateSignals.UnlockPointStateChanged
                .Where(signal => signal.State == UnlockPointState.Unlocked)
                .Subscribe(OnUnlockPointStateChanged)
        );
    }

    private void DisableAllSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            UnlockPointSlot slot = slots[i];

            if (slot == null)
                continue;

            if (slot.UnlockPoint != null)
                slot.UnlockPoint.HidePoint();

            if (slot.UnlockPointView != null)
                slot.UnlockPointView.Hide();
        }
    }

    /// <summary>
    /// 저장된 Unlock 상태를 복원합니다.
    ///
    /// completedUnlockIds:
    /// - 이미 완료한 UnlockPoint는 다시 표시하지 않습니다.
    ///
    /// revealedUnlockGroupIds:
    /// - 이전에 공개된 그룹은 다시 공개합니다.
    /// - 단, completedUnlockIds에 포함된 UnlockPoint는 표시하지 않습니다.
    /// </summary>
    private void RestoreUnlockProgressFromSave()
    {
        if (restoredFromSave)
            return;

        restoredFromSave = true;

        if (!useSaveData)
            return;

        if (SaveManager.Instance == null)
            return;

        if (SaveManager.Instance.IsResetting)
            return;

        GameSaveData data = SaveManager.Instance.CurrentData;

        if (data == null)
            return;

        completedUnlockIds.Clear();

        if (data.completedUnlockIds != null)
        {
            for (int i = 0; i < data.completedUnlockIds.Count; i++)
            {
                string unlockId = data.completedUnlockIds[i];

                if (!string.IsNullOrEmpty(unlockId))
                    completedUnlockIds.Add(unlockId);
            }
        }

        revealedGroupIds.Clear();

        if (data.revealedUnlockGroupIds != null)
        {
            for (int i = 0; i < data.revealedUnlockGroupIds.Count; i++)
            {
                string revealGroupId = data.revealedUnlockGroupIds[i];

                if (string.IsNullOrEmpty(revealGroupId))
                    continue;

                RevealGroupInternal(revealGroupId, saveAfterReveal: false);
            }
        }

        Debug.Log(
            $"[UnlockProgressionManager] RestoreUnlockProgressFromSave. " +
            $"Completed: {completedUnlockIds.Count}, RevealedGroups: {revealedGroupIds.Count}",
            this
        );
    }

    private void OnPlayerResourceAmountChanged(ResourceAmountSignal signal)
    {
        if (model == null)
            return;

        List<UnlockPointData> matchedData = model.FindByTrigger(
            UnlockTriggerType.FirstResourceChanged,
            signal.TargetId,
            signal.ResourceType
        );

        for (int i = 0; i < matchedData.Count; i++)
        {
            UnlockPointData data = matchedData[i];

            if (data == null)
                continue;

            if (signal.CurrentAmount < data.triggerValue)
                continue;

            RevealGroup(data.revealGroupId);
        }
    }

    private void OnPrisonFull(PrisonStateSignal signal)
    {
        if (model == null)
            return;

        List<UnlockPointData> matchedData = model.FindByTrigger(
            UnlockTriggerType.PrisonFull,
            signal.PrisonId,
            null
        );

        for (int i = 0; i < matchedData.Count; i++)
        {
            UnlockPointData data = matchedData[i];

            if (data == null)
                continue;

            if (data.triggerValue > 0 && signal.CurrentCount < data.triggerValue)
                continue;

            RevealGroup(data.revealGroupId);
        }
    }

    private void OnUnlockPointStateChanged(UnlockPointStateSignal signal)
    {
        if (string.IsNullOrEmpty(signal.UnlockPointId))
            return;

        if (completedUnlockIds.Contains(signal.UnlockPointId))
            return;

        if (!model.TryGetByUnlockId(signal.UnlockPointId, out UnlockPointData completedData))
            return;

        HandleUnlockCompleted(completedData);
    }

    /// <summary>
    /// 외부 시스템이나 디버그 메뉴에서 특정 RevealGroup을 공개할 때 사용하는 공개 API입니다.
    /// </summary>
    public void RevealGroup(string revealGroupId)
    {
        RevealGroupInternal(revealGroupId, saveAfterReveal: true);
    }

    private void RevealGroupInternal(string revealGroupId, bool saveAfterReveal)
    {
        if (string.IsNullOrEmpty(revealGroupId))
            return;

        if (model == null)
        {
            Debug.LogError("[UnlockProgressionManager] Model is null. Data load failed.", this);
            return;
        }

        if (revealedGroupIds.Contains(revealGroupId))
            return;

        if (!model.TryGetRevealGroup(revealGroupId, out IReadOnlyList<UnlockPointData> groupData))
        {
            Debug.LogWarning($"[UnlockProgressionManager] RevealGroup not found: {revealGroupId}", this);
            return;
        }

        revealedGroupIds.Add(revealGroupId);

        Debug.Log($"[UnlockProgressionManager] RevealGroup: {revealGroupId}, Count: {groupData.Count}", this);

        for (int i = 0; i < groupData.Count; i++)
        {
            RevealUnlockPoint(groupData[i]);
        }

        if (saveAfterReveal)
            SaveUnlockProgressIfNeeded();
    }

    private void RevealUnlockPoint(UnlockPointData data)
    {
        if (data == null)
            return;

        // 이미 완료된 UnlockPoint는 다시 표시하지 않습니다.
        if (completedUnlockIds.Contains(data.unlockId))
            return;

        string slotId = data.EffectiveSlotId;

        if (!slotMap.TryGetValue(slotId, out UnlockPointSlot slot))
        {
            Debug.LogError(
                $"[UnlockProgressionManager] Slot not found. UnlockId: {data.unlockId}, SlotId: {slotId}",
                this
            );
            return;
        }

        if (slot.UnlockPoint == null)
        {
            Debug.LogError($"[UnlockProgressionManager] Slot has no UnlockPoint. SlotId: {slotId}", slot);
            return;
        }

        if (slot.UnlockPointView == null)
        {
            Debug.LogError($"[UnlockProgressionManager] Slot has no UnlockPointView. SlotId: {slotId}", slot);
            return;
        }

        DisposePresenterBySlot(slotId);

        UnlockPointPresenter presenter = new UnlockPointPresenter(
            data,
            slot,
            slot.UnlockPointView,
            iconDatabase,
            OnPresenterUnlockCompleted
        );

        activePresentersByUnlockId[data.unlockId] = presenter;
        activePresentersBySlotId[slotId] = presenter;

        presenter.Reveal();
    }

    private void OnPresenterUnlockCompleted(UnlockPointData data)
    {
        HandleUnlockCompleted(data);
    }

    private void HandleUnlockCompleted(UnlockPointData data)
    {
        if (data == null)
            return;

        if (completedUnlockIds.Contains(data.unlockId))
            return;

        completedUnlockIds.Add(data.unlockId);

        Debug.Log($"[UnlockProgressionManager] Unlock completed: {data.unlockId}", this);

        if (resultExecutor != null)
            resultExecutor.Execute(data);
        else
            Debug.LogWarning("[UnlockProgressionManager] ResultExecutor is null.", this);

        DisposePresenterByUnlockId(data.unlockId);

        SaveUnlockProgressIfNeeded();

        if (!string.IsNullOrEmpty(data.nextRevealGroupId))
            RevealGroup(data.nextRevealGroupId);

        RevealGroupsTriggeredByUnlockCompleted(data.unlockId);
    }

    private void RevealGroupsTriggeredByUnlockCompleted(string completedUnlockId)
    {
        if (model == null || string.IsNullOrEmpty(completedUnlockId))
            return;

        List<UnlockPointData> matchedData = model.FindByTrigger(
            UnlockTriggerType.UnlockCompleted,
            completedUnlockId,
            null
        );

        for (int i = 0; i < matchedData.Count; i++)
        {
            UnlockPointData data = matchedData[i];

            if (data == null)
                continue;

            RevealGroup(data.revealGroupId);
        }
    }

    private void SaveUnlockProgressIfNeeded()
    {
        if (!saveImmediately)
            return;

        SaveUnlockProgress();
    }

    /// <summary>
    /// 현재까지 공개된 그룹과 완료된 해금 포인트를 저장 데이터에 동기화합니다.
    /// HashSet으로 중복을 막고 저장 직전에 List로 변환합니다.
    /// </summary>
    private void SaveUnlockProgress()
    {
        if (!useSaveData)
            return;

        if (SaveManager.Instance == null)
            return;

        GameSaveData data = SaveManager.Instance.CurrentData;

        if (data == null)
            return;

        if (data.completedUnlockIds == null)
            data.completedUnlockIds = new List<string>();

        if (data.revealedUnlockGroupIds == null)
            data.revealedUnlockGroupIds = new List<string>();

        data.completedUnlockIds.Clear();
        data.revealedUnlockGroupIds.Clear();

        foreach (string unlockId in completedUnlockIds)
        {
            if (!string.IsNullOrEmpty(unlockId))
                data.completedUnlockIds.Add(unlockId);
        }

        foreach (string revealGroupId in revealedGroupIds)
        {
            if (!string.IsNullOrEmpty(revealGroupId))
                data.revealedUnlockGroupIds.Add(revealGroupId);
        }

        SaveManager.Instance.MarkDirtyAndSave();
    }

    private void DisposePresenterByUnlockId(string unlockId)
    {
        if (string.IsNullOrEmpty(unlockId))
            return;

        if (!activePresentersByUnlockId.TryGetValue(unlockId, out UnlockPointPresenter presenter))
            return;

        string slotId = presenter.Slot != null ? presenter.Slot.SlotId : string.Empty;

        presenter.Dispose();

        activePresentersByUnlockId.Remove(unlockId);

        if (!string.IsNullOrEmpty(slotId))
            activePresentersBySlotId.Remove(slotId);
    }

    private void DisposePresenterBySlot(string slotId)
    {
        if (string.IsNullOrEmpty(slotId))
            return;

        if (!activePresentersBySlotId.TryGetValue(slotId, out UnlockPointPresenter presenter))
            return;

        string unlockId = presenter.Data != null ? presenter.Data.unlockId : string.Empty;

        presenter.Dispose();

        activePresentersBySlotId.Remove(slotId);

        if (!string.IsNullOrEmpty(unlockId))
            activePresentersByUnlockId.Remove(unlockId);
    }

    private void DisposeAllPresenters()
    {
        foreach (KeyValuePair<string, UnlockPointPresenter> pair in activePresentersByUnlockId)
        {
            pair.Value?.Dispose();
        }

        activePresentersByUnlockId.Clear();
        activePresentersBySlotId.Clear();
    }
}
