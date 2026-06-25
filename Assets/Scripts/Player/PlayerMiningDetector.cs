using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks mineable ore objects currently inside the player's mining trigger.
/// Stale entries are cleaned up so mining can recover even when trigger exit events are missed.
/// </summary>
public sealed class PlayerMiningDetector : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool logDetection;

    [Header("Cleanup")]
    [Tooltip("Ore entries are removed when they have not been seen for this many seconds.")]
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
        Log($"Exit Ore: {ore.name}");
    }

    private void OnDisable()
    {
        ClearAll();
    }

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

            if (ore.CanMine)
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
        Log($"Enter Ore: {ore.name}");
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
            Log($"Stale Remove Ore: {ore.name}");
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

    private void Log(string message)
    {
        if (logDetection)
            Debug.Log($"[PlayerMiningDetector] {message}", this);
    }
}