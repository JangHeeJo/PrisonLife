using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임에서 사용하는 IconId를 실제 Sprite로 변환하는 데이터베이스입니다.
/// UnlockPointView는 Sprite를 직접 찾지 않고 Presenter/Manager를 통해 이 DB에서 조회합니다.
/// </summary>
[CreateAssetMenu(
    fileName = "GameIconDatabase",
    menuName = "Game/Data/Game Icon Database"
)]
public sealed class GameIconDatabase : ScriptableObject
{
    [Serializable]
    private sealed class IconEntry
    {
        [Tooltip("데이터 테이블에서 사용하는 IconId입니다. 예: Icon_Weapon_01")]
        public string iconId = string.Empty;

        [Tooltip("IconId에 매칭될 실제 Sprite입니다.")]
        public Sprite sprite = null;
    }

    [Header("Icon List")]
    [SerializeField] private List<IconEntry> iconEntries = new();

    private readonly Dictionary<string, Sprite> iconMap = new();

    private bool isBuilt;

    /// <summary>
/// 게임에서 사용하는 IconId를 실제 Sprite로 변환하는 데이터베이스입니다.
/// UnlockPointView는 Sprite를 직접 찾지 않고 Presenter/Manager를 통해 이 DB에서 조회합니다.
/// </summary>
    public Sprite GetIcon(string iconId)
    {
        BuildIfNeeded();

        if (string.IsNullOrEmpty(iconId))
            return null;

        if (iconMap.TryGetValue(iconId, out Sprite sprite))
            return sprite;

        Debug.LogWarning($"[GameIconDatabase] IconId not found: {iconId}");
        return null;
    }

    /// <summary>
/// 게임에서 사용하는 IconId를 실제 Sprite로 변환하는 데이터베이스입니다.
/// UnlockPointView는 Sprite를 직접 찾지 않고 Presenter/Manager를 통해 이 DB에서 조회합니다.
/// </summary>
    private void BuildIfNeeded()
    {
        if (isBuilt)
            return;

        iconMap.Clear();

        for (int i = 0; i < iconEntries.Count; i++)
        {
            IconEntry entry = iconEntries[i];

            if (entry == null)
                continue;

            if (string.IsNullOrEmpty(entry.iconId))
                continue;

            if (iconMap.ContainsKey(entry.iconId))
            {
                Debug.LogWarning($"[GameIconDatabase] Duplicate IconId: {entry.iconId}");
                continue;
            }

            iconMap.Add(entry.iconId, entry.sprite);
        }

        isBuilt = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 에디터에서 아이콘 리스트를 수정하면 다음 GetIcon 호출 때 다시 캐싱되도록 초기화합니다.
        isBuilt = false;
    }
#endif
}