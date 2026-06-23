using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

/// <summary>
/// 월드 진행 조건이 어떤 신호로 열리는지 정의합니다.
/// 튜토리얼이 아니라 게임 진행 시스템이 해금 포인트 공개를 담당해야 합니다.
/// </summary>
public enum WorldProgressionTriggerType
{
    None,
    PlayerResourceFirstChanged,
    PrisonFull
}

/// <summary>
/// 조건 만족 시 특정 UnlockPoint를 Available 상태로 공개하는 데이터입니다.
/// 추후에는 이 데이터도 Excel/TSV로 분리할 수 있습니다.
/// </summary>
[Serializable]
public sealed class WorldProgressionEntry
{
    public string progressionId;
    public WorldProgressionTriggerType triggerType;

    [Header("Trigger Target")]
    public string triggerTargetId;
    public ResourceType triggerResourceType;

    [Header("Unlock Point")]
    public string unlockPointId;
    public GameObject unlockPointObject;

    [Header("Options")]
    public bool revealOnlyOnce = true;
}

/// <summary>
/// 해금 포인트 공개/활성화/상태 신호를 관리하는 월드 진행 매니저입니다.
/// Tutorial은 이 매니저가 공개한 결과를 관찰해서 카메라/화살표 안내만 담당합니다.
/// </summary>
public sealed class WorldProgressionManager : MonoBehaviour
{
    [SerializeField] private GameStateSignals gameStateSignals;
    [SerializeField] private List<WorldProgressionEntry> entries = new();

    private readonly HashSet<string> revealedUnlockPointIds = new();
    private readonly TutorialDisposableGroup disposables = new();

    private void Awake()
    {
        if (gameStateSignals == null)
            gameStateSignals = GameStateSignals.Instance;
    }

    private void OnEnable()
    {
        if (gameStateSignals == null)
            gameStateSignals = GameStateSignals.Instance;

        SubscribeSignals();
    }

    private void OnDisable()
    {
        disposables.Clear();
    }

    private void OnDestroy()
    {
        disposables.Dispose();
    }

    private void SubscribeSignals()
    {
        if (gameStateSignals == null)
            return;

        
        disposables.Clear();

        disposables.Add(
            gameStateSignals.ResourceAmountChanged
                .Where(signal => signal.OwnerType == ResourceOwnerType.Player)
                .Subscribe(OnPlayerResourceChanged)
        );

        disposables.Add(
            gameStateSignals.PrisonFull
                .Subscribe(OnPrisonFull)
        );
    }

    private void OnPlayerResourceChanged(ResourceAmountSignal signal)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            WorldProgressionEntry entry = entries[i];

            if (entry == null)
                continue;

            if (entry.triggerType != WorldProgressionTriggerType.PlayerResourceFirstChanged)
                continue;

            if (entry.triggerResourceType != signal.ResourceType)
                continue;

            if (!string.IsNullOrEmpty(entry.triggerTargetId) && entry.triggerTargetId != signal.TargetId)
                continue;

            // 현재 수량이 1 이상이 된 최초 순간을 해금 공개 조건으로 사용한다.
            if (signal.CurrentAmount > 0)
                RevealUnlockPoint(entry);
        }
    }

    private void OnPrisonFull(PrisonStateSignal signal)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            WorldProgressionEntry entry = entries[i];

            if (entry == null)
                continue;

            if (entry.triggerType != WorldProgressionTriggerType.PrisonFull)
                continue;

            if (!string.IsNullOrEmpty(entry.triggerTargetId) && entry.triggerTargetId != signal.PrisonId)
                continue;

            RevealUnlockPoint(entry);
        }
    }

    private void RevealUnlockPoint(WorldProgressionEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.unlockPointId))
            return;

        if (entry.revealOnlyOnce && revealedUnlockPointIds.Contains(entry.unlockPointId))
            return;

        revealedUnlockPointIds.Add(entry.unlockPointId);

        if (entry.unlockPointObject != null)
            entry.unlockPointObject.SetActive(true);

        // 해금 포인트가 공개되었음을 전체 게임 상태 신호로 전달한다.
        GameStateSignals.RaiseUnlockPointStateChanged(entry.unlockPointId, UnlockPointState.Available);
    }
}
