using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 장착된 무기 프리팹 안의 Trigger Collider로
/// 채굴 가능한 MineableOre를 감지합니다.
/// 
/// OnTriggerExit가 누락되는 경우를 대비해서,
/// OnTriggerStay로 최근 감지 시간을 갱신하고 일정 시간 이상 감지되지 않으면 자동 제거합니다.
/// </summary>
public sealed class PlayerMiningDetector : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool logDetection;

    [Header("Cleanup")]
    [Tooltip("이 시간 동안 OnTriggerStay가 들어오지 않으면 범위 밖으로 나간 것으로 보고 제거합니다.")]
    [SerializeField] private float staleRemoveDelay = 0.15f;

    private readonly List<MineableOre> detectedOres = new();
    private readonly Dictionary<MineableOre, float> lastSeenTimeByOre = new();

    public IReadOnlyList<MineableOre> DetectedOres => detectedOres;

    private void OnTriggerEnter(Collider other)
    {
        RefreshOre(other);
    }

    private void OnTriggerStay(Collider other)
    {
        RefreshOre(other);
    }

    private void OnTriggerExit(Collider other)
    {
        MineableOre ore = FindOre(other);

        if (ore == null)
            return;

        RemoveOre(ore);

        if (logDetection)
            Debug.Log($"[PlayerMiningDetector] Exit Ore: {ore.name}", this);
    }

    private void OnDisable()
    {
        ClearAll();
    }

    /// <summary>
    /// 현재 감지된 광석 중 실제 채굴 가능한 광석만 results에 담습니다.
    /// 오래 감지되지 않은 광석은 여기서 정리합니다.
    /// </summary>
    public int GetMineableOresNonAlloc(List<MineableOre> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        CleanupStaleOres();

        for (int i = detectedOres.Count - 1; i >= 0; i--)
        {
            MineableOre ore = detectedOres[i];

            if (ore == null)
            {
                detectedOres.RemoveAt(i);
                continue;
            }

            if (!ore.CanMine)
                continue;

            results.Add(ore);
        }

        return results.Count;
    }

    private void RefreshOre(Collider other)
    {
        MineableOre ore = FindOre(other);

        if (ore == null)
            return;

        lastSeenTimeByOre[ore] = Time.time;

        if (detectedOres.Contains(ore))
            return;

        detectedOres.Add(ore);

        if (logDetection)
            Debug.Log($"[PlayerMiningDetector] Enter Ore: {ore.name}", this);
    }

    private MineableOre FindOre(Collider other)
    {
        if (other == null)
            return null;

        return other.GetComponentInParent<MineableOre>();
    }

    private void CleanupStaleOres()
    {
        float now = Time.time;

        for (int i = detectedOres.Count - 1; i >= 0; i--)
        {
            MineableOre ore = detectedOres[i];

            if (ore == null)
            {
                detectedOres.RemoveAt(i);
                continue;
            }

            if (!lastSeenTimeByOre.TryGetValue(ore, out float lastSeenTime))
            {
                detectedOres.RemoveAt(i);
                continue;
            }

            if (now - lastSeenTime <= staleRemoveDelay)
                continue;

            detectedOres.RemoveAt(i);
            lastSeenTimeByOre.Remove(ore);

            if (logDetection)
                Debug.Log($"[PlayerMiningDetector] Stale Remove Ore: {ore.name}", this);
        }
    }

    private void RemoveOre(MineableOre ore)
    {
        if (ore == null)
            return;

        detectedOres.Remove(ore);
        lastSeenTimeByOre.Remove(ore);
    }

    private void ClearAll()
    {
        detectedOres.Clear();
        lastSeenTimeByOre.Clear();
    }
}