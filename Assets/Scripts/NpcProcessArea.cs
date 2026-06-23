using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 수갑을 받아 NPC를 처리하고, 처리 완료된 NPC를 감옥으로 이동시키는 영역입니다.
/// 감옥 수감 카운트는 Collider/Trigger가 아니라 NPC 이동 완료 콜백에서만 증가합니다.
/// </summary>
public class NpcProcessArea : MonoBehaviour, IResourceReceiver
{
    [Header("NPC Spawn")]
    [SerializeField] private ProcessNpcUnit npcPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform npcContainer;
    [SerializeField] private bool autoSpawnNpc = true;

    [Header("Initial NPC")]
    [SerializeField] private List<ProcessNpcUnit> initialNpcList = new List<ProcessNpcUnit>();

    [Header("Queue")]
    [Tooltip("0번은 처리 위치, 1번부터 대기 위치입니다.")]
    [SerializeField] private Transform[] queuePoints;

    [Header("Prison Path")]
    [SerializeField] private Transform[] prisonPathPoints;
    [SerializeField] private Transform prisonTarget;

    [Header("Prison Gate")]
    [SerializeField] private PrisonGateController prisonGateController;

    [Header("Handcuff")]
    [SerializeField] private int handcuffCapacity = 20;
    [SerializeField] private int requiredHandcuffPerNpc = 4;
    [SerializeField] private ResourceStackView handcuffBufferStackView;

    [Header("Transfer")]
    [SerializeField] private float giveInterval = 0.35f;
    [SerializeField] private float jumpPower = 1.2f;
    [SerializeField] private float transferDuration = 0.35f;

    [Header("Money Reward")]
    [SerializeField] private MoneyRewardPoint moneyRewardPoint;
    [SerializeField] private int rewardMoneyPerNpc = 6;

    [Header("Prison")]
    [SerializeField] private bool deactivateNpcAtPrison = true;

    [Header("Prison State")]
    [SerializeField] private PrisonAreaState prisonAreaState;

    private NpcProcessProgressUI ProgressUI =>
        UIManager.Instance != null ? UIManager.Instance.NpcProcessProgressUI : null;

    private readonly List<ProcessNpcUnit> waitingNpcs = new List<ProcessNpcUnit>();

    private ProcessNpcUnit currentNpc;

    private int currentNpcHandcuffCount;
    private int handcuffAmount;
    private int spawnedNpcCount;

    private bool isCurrentNpcReady;
    private bool isTransferring;
    private bool hasSpawnedReplacementForCurrent;
    private bool isInitialized;

    private CancellationTokenSource processLoopCts;

    private int LineSlotCount => queuePoints != null ? queuePoints.Length : 0;
    private int WaitingSlotCount => Mathf.Max(0, LineSlotCount - 1);

    private Transform ProcessPoint =>
        queuePoints != null && queuePoints.Length > 0 ? queuePoints[0] : null;

    private void OnEnable()
    {
        if (isInitialized)
            StartProcessLoop();
    }

    private void Start()
    {
        InitializeNpcs();
        MoveNextNpcToProcessPoint();

        isInitialized = true;
        StartProcessLoop();
    }

    private void OnDisable()
    {
        StopProcessLoop();
        HideNpcProgressUI();
    }

    private void OnDestroy()
    {
        StopProcessLoop();
    }

    public bool CanReceive(ResourceType type, int amount)
    {
        if (type != ResourceType.Handcuff)
            return false;

        return handcuffAmount + amount <= handcuffCapacity;
    }

    public bool TryReceive(ResourceType type, int amount)
    {
        if (!CanReceive(type, amount))
            return false;

        handcuffAmount += amount;
        return true;
    }

    public void RegisterNpc(ProcessNpcUnit npc)
    {
        if (npc == null)
            return;

        if (npc == currentNpc || waitingNpcs.Contains(npc))
            return;

        npc.ResetUnit();
        waitingNpcs.Add(npc);

        RefreshQueuePositions();

        if (currentNpc == null)
            MoveNextNpcToProcessPoint();
    }

    private void InitializeNpcs()
    {
        waitingNpcs.Clear();

        if (npcContainer == null)
            npcContainer = transform;

        for (int i = 0; i < initialNpcList.Count; i++)
        {
            ProcessNpcUnit npc = initialNpcList[i];

            if (npc == null)
                continue;

            npc.ResetUnit();
            waitingNpcs.Add(npc);
        }

        FillWaitingNpcs(LineSlotCount);
        RefreshQueuePositions();
    }

    private void StartProcessLoop()
    {
        if (processLoopCts != null)
            return;

        processLoopCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy()
        );

        ProcessLoopAsync(processLoopCts.Token).Forget();
    }

    private void StopProcessLoop()
    {
        if (processLoopCts == null)
            return;

        processLoopCts.Cancel();
        processLoopCts.Dispose();
        processLoopCts = null;
    }

    private async UniTaskVoid ProcessLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (currentNpc == null)
                {
                    MoveNextNpcToProcessPoint();
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    continue;
                }

                if (!CanProcessCurrentNpc())
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    continue;
                }

                if (giveInterval > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(giveInterval), cancellationToken: token);
                else
                    await UniTask.Yield(PlayerLoopTiming.Update, token);

                if (CanProcessCurrentNpc())
                    GiveHandcuffToCurrentNpc();

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private bool CanProcessCurrentNpc()
    {
        if (currentNpc == null)
            return false;

        if (!isCurrentNpcReady || isTransferring)
            return false;

        if (currentNpcHandcuffCount >= requiredHandcuffPerNpc)
            return false;

        if (handcuffAmount <= 0)
            return false;

        if (handcuffBufferStackView == null || handcuffBufferStackView.IsEmpty)
            return false;

        bool willCompleteNpc = currentNpcHandcuffCount + 1 >= requiredHandcuffPerNpc;

        if (willCompleteNpc && moneyRewardPoint != null)
        {
            if (!moneyRewardPoint.CanCreateMoney(rewardMoneyPerNpc))
                return false;
        }

        return true;
    }

    private void FillWaitingNpcs(int targetCount)
    {
        if (!autoSpawnNpc || npcPrefab == null)
            return;

        while (waitingNpcs.Count < targetCount)
        {
            ProcessNpcUnit npc = SpawnNpc();
            waitingNpcs.Add(npc);
        }
    }

    private ProcessNpcUnit SpawnNpc()
    {
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        ProcessNpcUnit npc = Instantiate(npcPrefab, spawnPosition, spawnRotation, npcContainer);

        npc.name = $"{npcPrefab.name}_{spawnedNpcCount++}";
        npc.ResetUnit();

        return npc;
    }

    private void RefreshQueuePositions()
    {
        if (queuePoints == null || queuePoints.Length <= 1)
            return;

        int visibleWaitingCount = Mathf.Min(waitingNpcs.Count, WaitingSlotCount);

        for (int i = 0; i < visibleWaitingCount; i++)
        {
            ProcessNpcUnit npc = waitingNpcs[i];

            if (npc == null)
                continue;

            Transform point = queuePoints[i + 1];

            if (point == null)
                continue;

            npc.MoveTo(point);
        }

        for (int i = visibleWaitingCount; i < waitingNpcs.Count; i++)
        {
            ProcessNpcUnit npc = waitingNpcs[i];

            if (npc == null || spawnPoint == null)
                continue;

            npc.MoveTo(spawnPoint);
        }
    }

    private void MoveNextNpcToProcessPoint()
    {
        if (currentNpc != null)
            return;

        if (ProcessPoint == null)
            return;

        if (waitingNpcs.Count <= 0)
            FillWaitingNpcs(LineSlotCount);

        if (waitingNpcs.Count <= 0)
            return;

        currentNpc = waitingNpcs[0];
        waitingNpcs.RemoveAt(0);

        currentNpcHandcuffCount = 0;
        isCurrentNpcReady = false;
        isTransferring = false;
        hasSpawnedReplacementForCurrent = false;

        RefreshQueuePositions();

        ProcessNpcUnit movingNpc = currentNpc;

        movingNpc.MoveTo(ProcessPoint, () =>
        {
            if (currentNpc != movingNpc)
                return;

            ShowNpcProgressUI();
            isCurrentNpcReady = true;
        });
    }

    private void GiveHandcuffToCurrentNpc()
    {
        if (currentNpc == null || handcuffBufferStackView == null)
            return;

        if (!handcuffBufferStackView.TryGetTopWorldPosition(out Vector3 startPosition))
            return;

        SpawnReplacementForCurrentNpc();

        handcuffAmount--;
        handcuffBufferStackView.HideLast();

        Vector3 endPosition = currentNpc.ReceivePosition;
        GameObject visualPrefab = handcuffBufferStackView.VisualPrefab;

        isTransferring = true;

        if (DoTweenManager.Instance == null || visualPrefab == null)
        {
            CompleteHandcuffTransfer();
            return;
        }

        DoTweenManager.Instance.PlayJump(
            visualPrefab,
            startPosition,
            endPosition,
            jumpPower,
            transferDuration,
            CompleteHandcuffTransfer
        );
    }

    private void SpawnReplacementForCurrentNpc()
    {
        if (hasSpawnedReplacementForCurrent)
            return;

        if (!autoSpawnNpc || npcPrefab == null)
            return;

        if (waitingNpcs.Count >= LineSlotCount)
        {
            hasSpawnedReplacementForCurrent = true;
            return;
        }

        ProcessNpcUnit npc = SpawnNpc();
        waitingNpcs.Add(npc);

        hasSpawnedReplacementForCurrent = true;
        RefreshQueuePositions();
    }

    private void CompleteHandcuffTransfer()
    {
        if (currentNpc == null)
        {
            isTransferring = false;
            return;
        }

        currentNpcHandcuffCount++;
        UpdateNpcProgressUI();

        if (currentNpcHandcuffCount >= requiredHandcuffPerNpc)
        {
            CompleteCurrentNpcProcess();
            return;
        }

        isTransferring = false;
    }

    private void CompleteCurrentNpcProcess()
    {
        ProcessNpcUnit completedNpc = currentNpc;

        currentNpc = null;
        currentNpcHandcuffCount = 0;

        isCurrentNpcReady = false;
        isTransferring = false;
        hasSpawnedReplacementForCurrent = false;

        HideNpcProgressUI();

        if (completedNpc == null)
        {
            MoveNextNpcToProcessPoint();
            return;
        }

        if (moneyRewardPoint != null && rewardMoneyPerNpc > 0)
        {
            moneyRewardPoint.CreateMoneyBatchFrom(
                completedNpc.ReceivePosition,
                rewardMoneyPerNpc
            );
        }

        completedNpc.SetPrisonerVisual();

        MoveNpcToPrison(completedNpc);
        MoveNextNpcToProcessPoint();
    }

    private void MoveNpcToPrison(ProcessNpcUnit npc)
    {
        if (npc == null)
            return;

        prisonGateController?.OpenForNpc(npc);

        if (prisonPathPoints != null && prisonPathPoints.Length > 0)
        {
            npc.MoveAlongPath(prisonPathPoints, () =>
            {
                CompleteNpcArrivalAtPrison(npc);
            });

            return;
        }

        if (prisonTarget != null)
        {
            npc.MoveTo(prisonTarget, () =>
            {
                CompleteNpcArrivalAtPrison(npc);
            });

            return;
        }

        CompleteNpcArrivalAtPrison(npc);
    }

    /// <summary>
    /// 수감 카운트는 오직 이 함수에서만 증가합니다.
    /// Collider/Trigger에서는 절대 AddPrisoner를 호출하지 마세요.
    /// </summary>
    private void CompleteNpcArrivalAtPrison(ProcessNpcUnit npc)
    {
        if (npc == null)
            return;

        prisonAreaState?.AddPrisoner(1);
        prisonGateController?.CloseForNpc(npc);

        if (deactivateNpcAtPrison)
            npc.gameObject.SetActive(false);
    }

    private void ShowNpcProgressUI()
    {
        if (currentNpc == null || ProgressUI == null)
            return;

        ProgressUI.SetFollowTarget(currentNpc.ProgressAnchor);
        ProgressUI.Show(currentNpcHandcuffCount, requiredHandcuffPerNpc);
    }

    private void UpdateNpcProgressUI()
    {
        if (ProgressUI == null)
            return;

        ProgressUI.UpdateProgress(currentNpcHandcuffCount, requiredHandcuffPerNpc);
    }

    private void HideNpcProgressUI()
    {
        if (ProgressUI == null)
            return;

        ProgressUI.Hide();
    }
}
