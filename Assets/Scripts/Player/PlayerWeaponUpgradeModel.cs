using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 채굴에 실제 적용되는 최종 스탯입니다.
/// 
/// 채굴 범위는 데이터가 아니라
/// 현재 장착된 무기 프리팹 안의 MiningDetector Collider 크기로 결정합니다.
/// </summary>
[Serializable]
public sealed class MiningStats
{
    [Tooltip("채굴 1회가 실행되는 주기입니다. 낮을수록 빠릅니다.")]
    public float miningInterval = 0.5f;

    [Tooltip("한 번의 채굴 Tick에서 동시에 처리할 수 있는 광석 수입니다.")]
    public int maxTargetsPerHit = 1;

    [Tooltip("플레이어가 들 수 있는 자원 한도입니다.")]
    public int carryLimit = 10;

    public MiningStats()
    {
    }

    public MiningStats(
        float miningInterval,
        int maxTargetsPerHit,
        int carryLimit)
    {
        this.miningInterval = Mathf.Max(0.05f, miningInterval);
        this.maxTargetsPerHit = Mathf.Max(1, maxTargetsPerHit);
        this.carryLimit = Mathf.Max(1, carryLimit);
    }
}

/// <summary>
/// PlayerWeaponUpgradeData.txt의 한 줄에 해당하는 데이터입니다.
/// </summary>
[Serializable]
public sealed class PlayerWeaponUpgradeData
{
    public string weaponLevelId;

    public MiningStats miningStats = new MiningStats();

    public string weaponVisualId;

    public string note;
}

/// <summary>
/// 무기 업그레이드 데이터를 조회하기 위한 런타임 Model입니다.
/// </summary>
public sealed class PlayerWeaponUpgradeModel
{
    private readonly Dictionary<string, PlayerWeaponUpgradeData> dataByLevelId = new();

    public PlayerWeaponUpgradeModel(List<PlayerWeaponUpgradeData> loadedData)
    {
        BuildLookup(loadedData);
    }

    private void BuildLookup(List<PlayerWeaponUpgradeData> loadedData)
    {
        dataByLevelId.Clear();

        if (loadedData == null)
            return;

        for (int i = 0; i < loadedData.Count; i++)
        {
            PlayerWeaponUpgradeData data = loadedData[i];

            if (data == null)
                continue;

            if (string.IsNullOrEmpty(data.weaponLevelId))
                continue;

            if (dataByLevelId.ContainsKey(data.weaponLevelId))
            {
                Debug.LogWarning($"[PlayerWeaponUpgradeModel] Duplicate WeaponLevelId: {data.weaponLevelId}");
                continue;
            }

            dataByLevelId.Add(data.weaponLevelId, data);
        }
    }

    public bool TryGet(string weaponLevelId, out PlayerWeaponUpgradeData data)
    {
        data = null;

        if (string.IsNullOrEmpty(weaponLevelId))
            return false;

        return dataByLevelId.TryGetValue(weaponLevelId, out data);
    }
}

/// <summary>
/// PlayerWeaponUpgradeData.txt를 PlayerWeaponUpgradeData 리스트로 변환하는 Loader입니다.
/// 
/// 전제:
/// - 탭 구분 TSV 형식
/// - 첫 줄은 헤더
/// - 컬럼 순서는 바뀌어도 됨
/// - 헤더 이름 기준으로 파싱
/// </summary>
public static class PlayerWeaponUpgradeTableLoader
{
    public static List<PlayerWeaponUpgradeData> Load(TextAsset textAsset)
    {
        List<PlayerWeaponUpgradeData> result = new();

        if (textAsset == null)
        {
            Debug.LogError("[PlayerWeaponUpgradeTableLoader] TextAsset is null.");
            return result;
        }

        string text = textAsset.text;

        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogError("[PlayerWeaponUpgradeTableLoader] TextAsset is empty.");
            return result;
        }

        string[] lines = text.Replace("\r", string.Empty).Split('\n');

        if (lines.Length <= 1)
        {
            Debug.LogError("[PlayerWeaponUpgradeTableLoader] Data has no rows.");
            return result;
        }

        Dictionary<string, int> headerMap = BuildHeaderMap(lines[0]);

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.TrimStart().StartsWith("#"))
                continue;

            string[] columns = line.Split('\t');

            PlayerWeaponUpgradeData data = ParseRow(columns, headerMap, i + 1);

            if (data != null)
                result.Add(data);
        }

        return result;
    }

    private static Dictionary<string, int> BuildHeaderMap(string headerLine)
    {
        Dictionary<string, int> map = new();
        string[] headers = headerLine.Split('\t');

        for (int i = 0; i < headers.Length; i++)
        {
            string key = headers[i].Trim().Trim('\uFEFF');

            if (string.IsNullOrEmpty(key))
                continue;

            if (!map.ContainsKey(key))
                map.Add(key, i);
        }

        return map;
    }

    private static PlayerWeaponUpgradeData ParseRow(
        string[] columns,
        Dictionary<string, int> headerMap,
        int lineNumber)
    {
        string weaponLevelId = Get(columns, headerMap, "WeaponLevelId");

        if (string.IsNullOrEmpty(weaponLevelId))
        {
            Debug.LogWarning($"[PlayerWeaponUpgradeTableLoader] Line {lineNumber} has empty WeaponLevelId.");
            return null;
        }

        PlayerWeaponUpgradeData data = new PlayerWeaponUpgradeData
        {
            weaponLevelId = weaponLevelId,

            miningStats = new MiningStats(
                ParseFloat(Get(columns, headerMap, "MiningInterval"), 0.5f),
                ParseInt(Get(columns, headerMap, "MaxTargetsPerHit"), 1),
                ParseInt(Get(columns, headerMap, "CarryLimit"), 10)
            ),

            weaponVisualId = Get(columns, headerMap, "WeaponVisualId"),
            note = Get(columns, headerMap, "Note")
        };

        return data;
    }

    private static string Get(
        string[] columns,
        Dictionary<string, int> headerMap,
        string columnName)
    {
        if (!headerMap.TryGetValue(columnName, out int index))
            return string.Empty;

        if (index < 0 || index >= columns.Length)
            return string.Empty;

        return columns[index].Trim();
    }

    private static int ParseInt(string value, int defaultValue)
    {
        if (int.TryParse(value, out int result))
            return result;

        return defaultValue;
    }

    private static float ParseFloat(string value, float defaultValue)
    {
        if (float.TryParse(value, out float result))
            return result;

        return defaultValue;
    }
}