using UnityEngine;

/// <summary>
/// 자동 채광 일꾼입니다.
///
/// 핵심 규칙:
/// - 일꾼은 광석 1개를 얻기 위해 requiredHitsPerOre만큼 작업합니다.
/// - requiredHitsPerOre 도달 전에는 MineableOre.TryMine()을 호출하지 않습니다.
/// - 마지막 작업에서만 TryMine()을 호출합니다.
/// - TryMine()이 성공하면 MineableOre 내부의 기존 비활성화/리스폰 로직이 실행됩니다.
/// - 획득한 Ore는 플레이어 CarryStack이 아니라 OreDepositPoint에 즉시 들어갑니다.
/// </summary>
public sealed class AutoMinerWorkerUnit : MonoBehaviour, IUnitSpawnContextReceiver
{
    private enum WorkerState
    {
        Idle,
        MoveToOre,
        Mining,
        Wait
    }

    [Header("Target")]
    [Tooltip("자동 채광 일꾼이 채굴할 실제 MineableOre 목록입니다.")]
    [SerializeField] private MineableOre[] oreTargets;

    [Tooltip("캐낸 광석을 즉시 넣을 OreDepositPoint의 ResourcePoint입니다.")]
    [SerializeField] private ResourcePoint oreDepositPoint;

    [Header("Mining")]
    [SerializeField] private ResourceType miningResourceType = ResourceType.Ore;

    [Tooltip("일꾼이 광석 1개를 캐기 위해 필요한 작업 횟수입니다.")]
    [SerializeField, Min(1)] private int requiredHitsPerOre = 2;

    [Tooltip("한 번 작업하는 데 걸리는 시간입니다.")]
    [SerializeField] private float hitInterval = 0.5f;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 2.5f;

    [Tooltip("광석 위치에 이 거리 이하로 가까워지면 채굴을 시작합니다.")]
    [SerializeField] private float arriveDistance = 0.5f;

    [Tooltip("회전 속도입니다.")]
    [SerializeField] private float rotateSpeed = 12f;

    [Header("Loop")]
    [Tooltip("광석 1개를 DepositPoint에 넣은 뒤 다음 작업까지 기다리는 시간입니다.")]
    [SerializeField] private float waitAfterDeposit = 0.2f;

    [Tooltip("채굴할 광석이 없거나 DepositPoint가 꽉 찼을 때 대기 시간입니다.")]
    [SerializeField] private float retryDelay = 0.5f;

    private WorkerState state = WorkerState.Idle;

    private MineableOre currentOre;
    private int currentHitCount;
    private float workTimer;
    private float waitTimer;

    private void OnEnable()
    {
        ResetState();
    }

    /// <summary>
    /// UnitSpawnController가 생성 직후 호출합니다.
    /// 프리팹이 직접 들고 있기 어려운 Scene 참조를 여기서 주입받습니다.
    /// </summary>
    public void Initialize(UnitSpawnContext context)
    {
        if (context.OreTargets != null && context.OreTargets.Length > 0)
            oreTargets = context.OreTargets;

        if (context.OreDepositPoint != null)
            oreDepositPoint = context.OreDepositPoint;

        ResetState();

        Debug.Log(
            $"[AutoMinerWorkerUnit] Initialized. OreTargets: {(oreTargets == null ? 0 : oreTargets.Length)}, DepositPoint: {(oreDepositPoint == null ? "NULL" : oreDepositPoint.name)}",
            this
        );
    }

    private void Update()
    {
        switch (state)
        {
            case WorkerState.Idle:
                FindNextOreTarget();
                break;

            case WorkerState.MoveToOre:
                UpdateMoveToOre();
                break;

            case WorkerState.Mining:
                UpdateMining();
                break;

            case WorkerState.Wait:
                UpdateWait();
                break;
        }
    }

    /// <summary>
    /// 생성되거나 다시 켜질 때 상태를 초기화합니다.
    /// Clone마다 독립적으로 실행됩니다.
    /// </summary>
    private void ResetState()
    {
        state = WorkerState.Idle;
        currentOre = null;
        currentHitCount = 0;
        workTimer = 0f;
        waitTimer = 0f;
    }

    /// <summary>
    /// 가장 가까운 채굴 가능한 광석을 찾습니다.
    /// </summary>
    private void FindNextOreTarget()
    {
        // DepositPoint가 꽉 찼으면 광석을 캐지 않고 대기합니다.
        // 먼저 TryMine을 해버리면 광석만 사라지고 저장이 실패할 수 있기 때문입니다.
        if (oreDepositPoint == null ||
            !oreDepositPoint.CanAddFromAutomation(miningResourceType, 1))
        {
            Wait(retryDelay);
            return;
        }

        currentOre = FindClosestMineableOre();

        if (currentOre == null)
        {
            Wait(retryDelay);
            return;
        }

        currentHitCount = 0;
        workTimer = 0f;
        state = WorkerState.MoveToOre;
    }

    /// <summary>
    /// 광석 위치로 이동합니다.
    /// </summary>
    private void UpdateMoveToOre()
    {
        if (!IsValidOre(currentOre))
        {
            state = WorkerState.Idle;
            return;
        }

        Vector3 targetPosition = currentOre.transform.position;
        Vector3 currentPosition = transform.position;

        targetPosition.y = currentPosition.y;

        Vector3 direction = targetPosition - currentPosition;
        float distance = direction.magnitude;

        if (distance <= arriveDistance)
        {
            state = WorkerState.Mining;
            workTimer = 0f;
            return;
        }

        Vector3 moveDirection = direction.normalized;

        transform.position += moveDirection * moveSpeed * Time.deltaTime;
        RotateTo(moveDirection);
    }

    /// <summary>
    /// 채굴 작업을 진행합니다.
    /// requiredHitsPerOre만큼 작업한 뒤에만 MineableOre.TryMine()을 호출합니다.
    /// </summary>
    private void UpdateMining()
    {
        if (!IsValidOre(currentOre))
        {
            state = WorkerState.Idle;
            return;
        }

        // 채굴 중 DepositPoint가 꽉 차면 광석은 건드리지 않고 대기합니다.
        if (oreDepositPoint == null ||
            !oreDepositPoint.CanAddFromAutomation(miningResourceType, 1))
        {
            Wait(retryDelay);
            return;
        }

        LookAtOre();

        workTimer += Time.deltaTime;

        if (workTimer < hitInterval)
            return;

        workTimer = 0f;
        currentHitCount++;

        // 여기서 나중에 애니메이션/이펙트 연결 가능.
        // animator.SetTrigger("Mine");

        if (currentHitCount < requiredHitsPerOre)
            return;

        CompleteMine();
    }

    /// <summary>
    /// 최종 채굴 처리입니다.
    /// 여기서 MineableOre.TryMine()을 호출하므로 기존 광석 비활성/리스폰 로직이 실행됩니다.
    /// </summary>
    private void CompleteMine()
    {
        if (!IsValidOre(currentOre))
        {
            state = WorkerState.Idle;
            return;
        }

        if (!currentOre.TryMine(out int minedAmount))
        {
            state = WorkerState.Idle;
            return;
        }

        int safeAmount = Mathf.Max(1, minedAmount);

        bool deposited = oreDepositPoint != null &&
                         oreDepositPoint.TryAddFromAutomation(miningResourceType, safeAmount);

        if (!deposited)
        {
            // 여기까지 왔는데 실패하면 DepositPoint 상태가 중간에 바뀐 케이스입니다.
            // 광석은 이미 TryMine으로 처리됐기 때문에 로그만 남깁니다.
            Debug.LogWarning("[AutoMinerWorkerUnit] Ore mined but deposit failed.", this);
        }

        currentHitCount = 0;
        currentOre = null;

        Wait(waitAfterDeposit);
    }

    /// <summary>
    /// 대기 상태로 전환합니다.
    /// </summary>
    private void Wait(float duration)
    {
        waitTimer = Mathf.Max(0f, duration);
        state = WorkerState.Wait;
    }

    /// <summary>
    /// 대기 후 다시 광석을 찾습니다.
    /// </summary>
    private void UpdateWait()
    {
        waitTimer -= Time.deltaTime;

        if (waitTimer > 0f)
            return;

        state = WorkerState.Idle;
    }

    /// <summary>
    /// 현재 위치 기준 가장 가까운 채굴 가능한 광석을 찾습니다.
    /// </summary>
    private MineableOre FindClosestMineableOre()
    {
        if (oreTargets == null || oreTargets.Length == 0)
            return null;

        MineableOre closest = null;
        float closestDistanceSqr = float.MaxValue;

        Vector3 currentPosition = transform.position;

        for (int i = 0; i < oreTargets.Length; i++)
        {
            MineableOre ore = oreTargets[i];

            if (!IsValidOre(ore))
                continue;

            float distanceSqr = (ore.transform.position - currentPosition).sqrMagnitude;

            if (distanceSqr >= closestDistanceSqr)
                continue;

            closestDistanceSqr = distanceSqr;
            closest = ore;
        }

        return closest;
    }

    /// <summary>
    /// 채굴 가능한 광석인지 확인합니다.
    /// MineableOre 내부 CanMine을 사용합니다.
    /// </summary>
    private bool IsValidOre(MineableOre ore)
    {
        return ore != null && ore.CanMine;
    }

    /// <summary>
    /// 이동 방향으로 자연스럽게 회전합니다.
    /// </summary>
    private void RotateTo(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// 채굴 중 광석을 바라봅니다.
    /// </summary>
    private void LookAtOre()
    {
        if (currentOre == null)
            return;

        Vector3 direction = currentOre.transform.position - transform.position;
        RotateTo(direction);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        requiredHitsPerOre = Mathf.Max(1, requiredHitsPerOre);
        hitInterval = Mathf.Max(0.05f, hitInterval);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        arriveDistance = Mathf.Max(0.05f, arriveDistance);
        retryDelay = Mathf.Max(0.05f, retryDelay);
        waitAfterDeposit = Mathf.Max(0f, waitAfterDeposit);
    }
#endif
}