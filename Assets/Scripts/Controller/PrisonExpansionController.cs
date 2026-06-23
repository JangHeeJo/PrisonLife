using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 감옥 확장 실행 컨트롤러입니다.
///
/// 저장:
/// - 감옥 확장 완료 여부를 저장합니다.
/// - 앱 재실행 시 확장 완료 상태라면 막힌 오브젝트를 비활성화하고 섹터를 즉시 표시합니다.
/// </summary>
public sealed class PrisonExpansionController : MonoBehaviour
{
    [Header("Id")]
    [SerializeField] private string expansionId = "PrisonExpand_01";

    [Header("Prison")]
    [SerializeField] private PrisonAreaState prisonAreaState;

    [Tooltip("확장 시 늘어날 수용량입니다. 예: +20")]
    [SerializeField] private int addCapacity = 20;

    [Header("Expansion View")]
    [SerializeField] private PrisonSectorExpansionView expansionView;

    [Header("Disable On Expand")]
    [Tooltip("확장 시 비활성화할 오브젝트입니다. 예: 막고 있던 벽, 공사 표지, 폐쇄 구역 오브젝트")]
    [SerializeField] private GameObject objectToDisableOnExpand;

    [Tooltip("true면 섹터 등장 전에 비활성화합니다. false면 등장 완료 후 비활성화합니다.")]
    [SerializeField] private bool disableBeforeReveal = true;

    [Header("Game Clear")]
    [Tooltip("감옥 확장 완료 후 게임 클리어 처리를 담당하는 컨트롤러입니다.")]
    [SerializeField] private GameClearController gameClearController;

    [Tooltip("true면 감옥 확장 완료 후 게임 클리어 UI를 띄우고 현재 씬을 다시 로드합니다.")]
    [SerializeField] private bool clearGameAfterExpand = true;

    [Tooltip("확장 연출 완료 후 클리어 처리까지 추가 대기 시간입니다.")]
    [SerializeField] private float clearDelaySeconds = 0.5f;

    [Header("Save")]
    [SerializeField] private bool useSaveData = true;

    [Header("Option")]
    [SerializeField] private bool expandOnlyOnce = true;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private bool expanded;
    private bool isExpanding;
    private bool restoredFromSave;

    private CancellationTokenSource expandCts;

    public bool IsExpanded => expanded;
    public bool IsExpanding => isExpanding;

    private void Start()
    {
        RestoreExpansionState();
    }

    private void OnDisable()
    {
        CancelExpand();
        SaveExpansionState();
    }

    private void OnDestroy()
    {
        CancelExpand();
    }

    /// <summary>
    /// 기존 UnlockResultExecutor 등에서 호출하던 함수명은 그대로 유지합니다.
    /// </summary>
    public void ExpandPrison()
    {
        if (isExpanding)
            return;

        if (expandOnlyOnce && expanded)
            return;

        ExpandPrisonAsync().Forget();
    }

    private async UniTaskVoid ExpandPrisonAsync()
    {
        CancelExpand();

        expandCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy()
        );

        CancellationToken token = expandCts.Token;

        isExpanding = true;
        expanded = true;

        try
        {
            if (logState)
            {
                Debug.Log(
                    $"[PrisonExpansionController] ExpandPrison started. AddCapacity: {addCapacity}",
                    this
                );
            }

            if (disableBeforeReveal)
                DisableBlockObject();

            await RevealExpansionViewAsync(token);

            if (!disableBeforeReveal)
                DisableBlockObject();

            AddPrisonCapacity();

            SaveExpansionState();

            if (logState)
            {
                Debug.Log(
                    $"[PrisonExpansionController] ExpandPrison completed. AddCapacity: {addCapacity}",
                    this
                );
            }

            if (clearGameAfterExpand)
            {
                if (clearDelaySeconds > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(clearDelaySeconds),
                        cancellationToken: token
                    );
                }

                PlayGameClear();
            }
        }
        catch (OperationCanceledException)
        {
            if (logState)
                Debug.Log("[PrisonExpansionController] ExpandPrison canceled.", this);
        }
        finally
        {
            isExpanding = false;

            expandCts?.Dispose();
            expandCts = null;
        }
    }

    private async UniTask RevealExpansionViewAsync(CancellationToken token)
    {
        if (expansionView == null)
            return;

        await expansionView.RevealAsync(token);
    }

    private void AddPrisonCapacity()
    {
        if (prisonAreaState == null)
            return;

        prisonAreaState.AddCapacity(addCapacity);
    }

    private void DisableBlockObject()
    {
        if (objectToDisableOnExpand == null)
            return;

        objectToDisableOnExpand.SetActive(false);
    }

    private void PlayGameClear()
    {
        if (gameClearController == null)
        {
            if (logState)
                Debug.LogWarning("[PrisonExpansionController] GameClearController is null.", this);

            return;
        }

        gameClearController.PlayClearAndRestart();
    }

    private void RestoreExpansionState()
    {
        if (restoredFromSave)
            return;

        restoredFromSave = true;

        if (!useSaveData)
            return;

        if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
            return;

        GameSaveData data = SaveManager.Instance.CurrentData;

        if (data.prisonExpansions == null)
            return;

        for (int i = 0; i < data.prisonExpansions.Count; i++)
        {
            PrisonExpansionSaveData saved = data.prisonExpansions[i];

            if (saved == null)
                continue;

            if (saved.expansionId != expansionId)
                continue;

            if (!saved.expanded)
                return;

            expanded = true;
            isExpanding = false;

            DisableBlockObject();

            if (expansionView != null)
                expansionView.ShowImmediate();

            if (logState)
                Debug.Log($"[PrisonExpansionController] Restored expansion: {expansionId}", this);

            return;
        }
    }

    private void SaveExpansionState()
    {
        if (!useSaveData)
            return;

        if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
            return;

        if (SaveManager.Instance.IsResetting)
            return;

        GameSaveData data = SaveManager.Instance.CurrentData;

        if (data.prisonExpansions == null)
            data.prisonExpansions = new List<PrisonExpansionSaveData>();

        PrisonExpansionSaveData target = null;

        for (int i = 0; i < data.prisonExpansions.Count; i++)
        {
            if (data.prisonExpansions[i] == null)
                continue;

            if (data.prisonExpansions[i].expansionId == expansionId)
            {
                target = data.prisonExpansions[i];
                break;
            }
        }

        if (target == null)
        {
            target = new PrisonExpansionSaveData
            {
                expansionId = expansionId
            };

            data.prisonExpansions.Add(target);
        }

        target.expanded = expanded;

        SaveManager.Instance.MarkDirtyAndSave();
    }

    private void CancelExpand()
    {
        if (expandCts == null)
            return;

        expandCts.Cancel();
        expandCts.Dispose();
        expandCts = null;

        isExpanding = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        addCapacity = Mathf.Max(0, addCapacity);
        clearDelaySeconds = Mathf.Max(0f, clearDelaySeconds);

        if (string.IsNullOrEmpty(expansionId))
            expansionId = gameObject.name;
    }
#endif
}
