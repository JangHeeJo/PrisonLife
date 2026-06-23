using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 안의 TutorialTarget을 ID로 찾아주는 Registry입니다.
/// 비활성화된 UnlockPoint도 찾아야 하므로 includeInactive 검색을 사용합니다.
/// </summary>
public sealed class TutorialTargetRegistry : MonoBehaviour
{
    private readonly Dictionary<string, TutorialTarget> targetMap = new();

    private void Awake()
    {
        RegisterSceneTargets();
    }

    private void RegisterSceneTargets()
    {
        targetMap.Clear();

        TutorialTarget[] targets = FindObjectsByType<TutorialTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < targets.Length; i++)
        {
            TutorialTarget target = targets[i];

            if (target == null || string.IsNullOrEmpty(target.TargetId))
                continue;

            if (targetMap.ContainsKey(target.TargetId))
            {
                Debug.LogWarning($"Duplicate TutorialTarget Id : {target.TargetId}", target);
                continue;
            }

            targetMap.Add(target.TargetId, target);
        }
    }

    public bool TryGetTarget(string targetId, out Transform targetTransform)
    {
        targetTransform = null;

        if (string.IsNullOrEmpty(targetId))
            return false;

        if (!targetMap.TryGetValue(targetId, out TutorialTarget target))
        {
            Debug.LogWarning($"TutorialTarget not found : {targetId}");
            return false;
        }

        targetTransform = target.TargetTransform;
        return targetTransform != null;
    }
}
