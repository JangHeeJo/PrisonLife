using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WeaponVisualId를 실제 무기 프리팹으로 변환하는 데이터베이스입니다.
/// 테이블 데이터는 문자열 ID만 보관하고, 실제 프리팹 참조는 이 ScriptableObject에서 관리합니다.
/// </summary>
[CreateAssetMenu(
    fileName = "WeaponVisualDatabase",
    menuName = "Game/Data/Weapon Visual Database"
)]
public sealed class WeaponVisualDatabase : ScriptableObject
{
    [Serializable]
    private sealed class WeaponVisualEntry
    {
        [Tooltip("테이블의 WeaponVisualId와 매칭됩니다. 예: Weapon_Pickaxe_01")]
        public string visualId = string.Empty;

        [Tooltip("WeaponSocket 하위에 생성할 무기 프리팹입니다.")]
        public GameObject prefab = null;
    }

    [Header("Weapon Visuals")]
    [SerializeField] private List<WeaponVisualEntry> entries = new();

    private readonly Dictionary<string, GameObject> prefabMap = new();

    private bool isBuilt;

    /// <summary>
/// WeaponVisualId를 실제 무기 프리팹으로 변환하는 데이터베이스입니다.
/// 테이블 데이터는 문자열 ID만 보관하고, 실제 프리팹 참조는 이 ScriptableObject에서 관리합니다.
/// </summary>
    public GameObject GetPrefab(string visualId)
    {
        BuildIfNeeded();

        if (string.IsNullOrEmpty(visualId))
            return null;

        if (prefabMap.TryGetValue(visualId, out GameObject prefab))
            return prefab;

        Debug.LogWarning($"[WeaponVisualDatabase] VisualId not found: {visualId}");
        return null;
    }

    /// <summary>
/// WeaponVisualId를 실제 무기 프리팹으로 변환하는 데이터베이스입니다.
/// 테이블 데이터는 문자열 ID만 보관하고, 실제 프리팹 참조는 이 ScriptableObject에서 관리합니다.
/// </summary>
    private void BuildIfNeeded()
    {
        if (isBuilt)
            return;

        prefabMap.Clear();

        for (int i = 0; i < entries.Count; i++)
        {
            WeaponVisualEntry entry = entries[i];

            if (entry == null)
                continue;

            if (string.IsNullOrEmpty(entry.visualId))
                continue;

            if (entry.prefab == null)
            {
                Debug.LogWarning($"[WeaponVisualDatabase] Prefab is null. VisualId: {entry.visualId}");
                continue;
            }

            if (prefabMap.ContainsKey(entry.visualId))
            {
                Debug.LogWarning($"[WeaponVisualDatabase] Duplicate VisualId: {entry.visualId}");
                continue;
            }

            prefabMap.Add(entry.visualId, entry.prefab);
        }

        isBuilt = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 에디터에서 리스트 값이 바뀌면 다음 GetPrefab 호출 때 다시 캐싱되도록 초기화합니다.
        isBuilt = false;
    }
#endif
}