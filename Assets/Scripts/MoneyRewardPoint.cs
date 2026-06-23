using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// NPC 처리 보상으로 생성된 돈을 보관하고,
/// 플레이어에게 제공하는 보상 포인트입니다.
///
/// 저장:
/// - 아직 플레이어가 먹지 않은 MoneyRewardPoint 내부 moneyAmount를 저장합니다.
/// - 앱 재실행 시 moneyStackView에 저장된 수량만큼 다시 표시합니다.
/// </summary>
public class MoneyRewardPoint : MonoBehaviour, IResourceProvider
{
    [Header("Storage")]
    [SerializeField] private int capacity = 20;

    [Header("Save")]
    [Tooltip("저장 데이터에서 이 MoneyRewardPoint를 구분하는 ID입니다.")]
    [SerializeField] private string saveId = "MoneyRewardPoint_01";

    [SerializeField] private bool useSaveData = true;
    [SerializeField] private bool saveImmediatelyOnChanged = true;

    [Header("Stack View")]
    [SerializeField] private ResourceStackView moneyStackView;

    [Header("Transfer Effect")]
    [SerializeField] private float jumpPower = 1.2f;
    [SerializeField] private float transferDuration = 0.35f;

    [Header("Batch Spawn")]
    [SerializeField] private float spawnInterval = 0.04f;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private int moneyAmount;
    private int pendingMoneyAmount;
    private bool restoredFromSave;

    private void Start()
    {
        RestoreFromSave();
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

    public bool CanCreateMoney(int amount = 1)
    {
        if (amount <= 0)
            return false;

        if (moneyStackView == null)
            return false;

        if (moneyAmount + pendingMoneyAmount + amount > capacity)
            return false;

        return moneyStackView.CanReserve(amount);
    }

    public bool CanProvide(ResourceType type, int amount)
    {
        if (type != ResourceType.Money)
            return false;

        return moneyAmount >= amount;
    }

    public bool TryProvide(ResourceType type, int amount)
    {
        if (!CanProvide(type, amount))
            return false;

        moneyAmount -= amount;

        SaveCurrentStateIfNeeded();

        return true;
    }

    /// <summary>
    /// NPC 위치에서 돈 여러 개를 생성합니다.
    /// 예: amount = 6이면 StackView 규칙에 따라 순차적으로 쌓입니다.
    /// </summary>
    public bool CreateMoneyBatchFrom(Vector3 startPosition, int amount, Action onComplete = null)
    {
        if (!CanCreateMoney(amount))
            return false;

        CreateMoneyBatchAsync(
            startPosition,
            amount,
            onComplete,
            this.GetCancellationTokenOnDestroy()
        ).Forget();

        return true;
    }

    private async UniTaskVoid CreateMoneyBatchAsync(
        Vector3 startPosition,
        int amount,
        Action onComplete,
        CancellationToken token)
    {
        int startedCount = 0;
        int completedCount = 0;

        bool spawnLoopFinished = false;
        bool completeInvoked = false;

        void TryInvokeComplete()
        {
            if (completeInvoked)
                return;

            if (!spawnLoopFinished)
                return;

            if (completedCount < startedCount)
                return;

            completeInvoked = true;
            onComplete?.Invoke();

            SaveCurrentStateIfNeeded();
        }

        try
        {
            for (int i = 0; i < amount; i++)
            {
                token.ThrowIfCancellationRequested();

                if (moneyStackView == null)
                    break;

                if (!moneyStackView.TryReserveNextWorldPosition(out Vector3 endPosition))
                    break;

                pendingMoneyAmount++;
                startedCount++;

                GameObject visualPrefab = moneyStackView.VisualPrefab;

                if (DoTweenManager.Instance == null || visualPrefab == null)
                {
                    CompleteCreateMoney();
                    completedCount++;
                    TryInvokeComplete();
                }
                else
                {
                    CreateSingleMoneyAsync(
                        visualPrefab,
                        startPosition,
                        endPosition,
                        () =>
                        {
                            CompleteCreateMoney();
                            completedCount++;
                            TryInvokeComplete();
                        },
                        token
                    ).Forget();
                }

                if (spawnInterval > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(spawnInterval),
                        cancellationToken: token
                    );
                }
                else
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            spawnLoopFinished = true;
            TryInvokeComplete();
        }
    }

    private async UniTaskVoid CreateSingleMoneyAsync(
        GameObject visualPrefab,
        Vector3 startPosition,
        Vector3 endPosition,
        Action onComplete,
        CancellationToken token)
    {
        try
        {
            await DoTweenManager.Instance.PlayJumpAsync(
                visualPrefab,
                startPosition,
                endPosition,
                jumpPower,
                transferDuration,
                token
            );

            onComplete?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CompleteCreateMoney()
    {
        pendingMoneyAmount = Mathf.Max(0, pendingMoneyAmount - 1);
        moneyAmount++;

        if (moneyStackView != null)
            moneyStackView.ShowReserved();

        SaveCurrentStateIfNeeded();
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

        if (data.moneyRewardPoints == null)
            return;

        int savedAmount = 0;

        for (int i = 0; i < data.moneyRewardPoints.Count; i++)
        {
            MoneyRewardPointSaveData saved = data.moneyRewardPoints[i];

            if (saved == null)
                continue;

            if (saved.saveId == saveId)
            {
                savedAmount = Mathf.Max(0, saved.amount);
                break;
            }
        }

        if (savedAmount <= 0)
            return;

        RestoreMoneyVisuals(savedAmount);

        if (logState)
        {
            Debug.Log(
                $"[MoneyRewardPoint] Restored. SaveId: {saveId}, Amount: {moneyAmount}",
                this
            );
        }
    }

    private void RestoreMoneyVisuals(int amount)
    {
        if (moneyStackView == null)
            return;

        int safeAmount = Mathf.Min(amount, capacity);
        int restoredCount = 0;

        for (int i = 0; i < safeAmount; i++)
        {
            if (!moneyStackView.TryReserveNextWorldPosition(out _))
                break;

            moneyStackView.ShowReserved();
            restoredCount++;
        }

        moneyAmount = restoredCount;
        pendingMoneyAmount = 0;
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

        GameSaveData data = SaveManager.Instance.CurrentData;

        if (data.moneyRewardPoints == null)
            data.moneyRewardPoints = new System.Collections.Generic.List<MoneyRewardPointSaveData>();

        MoneyRewardPointSaveData target = null;

        for (int i = 0; i < data.moneyRewardPoints.Count; i++)
        {
            if (data.moneyRewardPoints[i] == null)
                continue;

            if (data.moneyRewardPoints[i].saveId == saveId)
            {
                target = data.moneyRewardPoints[i];
                break;
            }
        }

        if (target == null)
        {
            target = new MoneyRewardPointSaveData
            {
                saveId = saveId,
                amount = 0
            };

            data.moneyRewardPoints.Add(target);
        }

        target.amount = Mathf.Max(0, moneyAmount);

        if (logState)
        {
            Debug.Log(
                $"[MoneyRewardPoint] Saved. SaveId: {saveId}, Amount: {target.amount}",
                this
            );
        }

        SaveManager.Instance.MarkDirtyAndSave();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        capacity = Mathf.Max(1, capacity);
        spawnInterval = Mathf.Max(0f, spawnInterval);

        if (string.IsNullOrEmpty(saveId))
            saveId = gameObject.name;
    }
#endif
}
