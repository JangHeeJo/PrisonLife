using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 채굴 실행 컨트롤러입니다.
/// 
/// 채굴 범위는 현재 장착된 무기 프리팹 안의 PlayerMiningDetector가 담당합니다.
/// 무기가 교체되면 PlayerWeaponUpgradeController가 SetDetector를 호출해서
/// 현재 무기의 Detector를 연결합니다.
/// </summary>
public sealed class PlayerMiningController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCarryStack carryStack;
    [SerializeField] private PlayerWeaponView playerWeaponView;
    [SerializeField] private MaxTextFeedback maxTextFeedback;

    [Header("Default Stats")]
    [SerializeField] private MiningStats currentStats = new MiningStats();

    [Header("Diagnostics")]
    [SerializeField] private bool logState;

    [Header("Visual Option")]
    [SerializeField] private bool showToolOnlyWhenHasTarget = true;

    private readonly List<MineableOre> detectedMineableOres = new();

    private PlayerMiningDetector currentMiningDetector;
    private float miningTimer;

    private void Awake()
    {
        if (carryStack == null)
            carryStack = GetComponent<PlayerCarryStack>();

        if (playerWeaponView == null)
            playerWeaponView = GetComponentInChildren<PlayerWeaponView>(true);

        SetToolVisible(false);
    }

    private void Update()
    {
        int targetCount = GetCurrentMineableTargets();
        bool hasTarget = targetCount > 0;

        if (showToolOnlyWhenHasTarget)
            SetToolVisible(hasTarget);
        else
            SetToolVisible(true);

        if (!hasTarget)
        {
            miningTimer = 0f;
            return;
        }

        miningTimer += Time.deltaTime;

        if (miningTimer < currentStats.miningInterval)
            return;

        miningTimer = 0f;
        MineTick(targetCount);
    }

    /// <summary>
    /// 현재 장착된 무기 프리팹 안의 MiningDetector를 연결합니다.
    /// </summary>
    public void SetDetector(PlayerMiningDetector detector)
    {
        currentMiningDetector = detector;
        miningTimer = 0f;

        if (logState)
        {
            Debug.Log(
                $"[PlayerMiningController] SetDetector: {(detector == null ? "NULL" : detector.name)}",
                this
            );
        }
    }

    /// <summary>
    /// 외부에서 채굴 스탯을 적용합니다.
    /// </summary>
    public void ApplyMiningStats(MiningStats stats)
    {
        if (stats == null)
        {
            Debug.LogWarning("[PlayerMiningController] Tried to apply null MiningStats.", this);
            return;
        }

        currentStats = new MiningStats(
            stats.miningInterval,
            stats.maxTargetsPerHit,
            stats.carryLimit
        );

        miningTimer = 0f;

        if (logState)
        {
            Debug.Log(
                $"[PlayerMiningController] ApplyMiningStats. " +
                $"Interval: {currentStats.miningInterval}, " +
                $"Targets: {currentStats.maxTargetsPerHit}, " +
                $"CarryLimit: {currentStats.carryLimit}",
                this
            );
        }

        // 다음 단계에서 PlayerCarryStack 한도 변경 API와 연결 예정.
    }
    private int GetCurrentMineableTargets()
    {
        if (currentMiningDetector == null)
            return 0;

        return currentMiningDetector.GetMineableOresNonAlloc(detectedMineableOres);
    }

    private void MineTick(int targetCount)
    {
        if (carryStack == null)
            return;

        if (carryStack.IsFull(ResourceType.Ore))
        {
            maxTextFeedback?.Show();
            return;
        }

        SortTargetsByDistance();

        int mineCount = Mathf.Min(
            targetCount,
            Mathf.Max(1, currentStats.maxTargetsPerHit)
        );

        for (int i = 0; i < mineCount; i++)
        {
            MineableOre ore = detectedMineableOres[i];

            if (ore == null || !ore.CanMine)
                continue;

            if (carryStack.IsFull(ResourceType.Ore))
            {
                maxTextFeedback?.Show();
                break;
            }

            if (!ore.TryMine(out int amount))
                continue;

            AddOreToCarryStack(amount);
        }
    }

    private void AddOreToCarryStack(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            if (!carryStack.TryAdd(ResourceType.Ore))
                break;
        }
    }

    private void SortTargetsByDistance()
    {
        detectedMineableOres.Sort((a, b) =>
        {
            if (a == null && b == null)
                return 0;

            if (a == null)
                return 1;

            if (b == null)
                return -1;

            float distanceA = (a.transform.position - transform.position).sqrMagnitude;
            float distanceB = (b.transform.position - transform.position).sqrMagnitude;

            return distanceA.CompareTo(distanceB);
        });
    }

    private void SetToolVisible(bool visible)
    {
        if (playerWeaponView == null)
            return;

        playerWeaponView.SetVisible(visible);
    }
}