using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using UnityEngine;

/// <summary>
/// 해금 포인트가 어떤 조건에서 공개되는지 정의합니다.
/// </summary>
public enum UnlockTriggerType
{
    None,

    /// <summary>
    /// 특정 자원이 처음 증가했을 때.
    /// 예: 첫 돈 획득.
    /// </summary>
    FirstResourceChanged,

    /// <summary>
    /// 특정 해금이 완료되었을 때.
    /// 예: 무기 강화 1단계 완료 후 다음 해금 그룹 공개.
    /// </summary>
    UnlockCompleted,

    /// <summary>
    /// 감옥 수용 인원이 최대치에 도달했을 때.
    /// </summary>
    PrisonFull,

    /// <summary>
    /// 특정 RevealGroup이 완료되었을 때.
    /// 지금 당장 필수는 아니지만, 확장용으로 둡니다.
    /// </summary>
    RevealGroupCompleted
}

/// <summary>
/// 해금 완료 시 어떤 동작을 실행할지 정의합니다.
/// </summary>
public enum UnlockResultType
{
    None,

    /// <summary>
    /// 플레이어 무기/도구 레벨 업그레이드.
    /// </summary>
    UpgradeWeapon,

    /// <summary>
    /// 자동 일꾼 생성.
    /// 예: 채광 자동 일꾼 3명.
    /// </summary>
    SpawnAutoWorker,

    /// <summary>
    /// 감옥 수용량 확장.
    /// </summary>
    ExpandPrison,

    /// <summary>
    /// 특정 오브젝트 활성화.
    /// </summary>
    ActivateObject
}

/// <summary>
/// UnlockPointProgressionData.txt의 한 줄에 해당하는 데이터입니다.
/// 
/// 이 데이터는 "해금 포인트 UI/비용/결과/공개 조건"을 정의합니다.
/// 실제 상호작용, UI 표시, 결과 실행은 다른 클래스가 담당합니다.
/// </summary>
[Serializable]
public sealed class UnlockPointData
{
    [Header("Identity")]
    public string unlockId;

    /// <summary>
    /// 같은 RevealGroupId를 가진 데이터는 같은 타이밍에 공개됩니다.
    /// 예: 무기 강화 2단계 + 채광 자동 일꾼 해금.
    /// </summary>
    public string revealGroupId;

    /// <summary>
    /// 이 해금 데이터를 표시할 월드 슬롯 ID입니다.
    /// SlotId가 비어 있으면 unlockId를 슬롯 ID로 대체해서 사용할 수 있습니다.
    /// </summary>
    public string slotId;

    public int revealOrder;

    [Header("Trigger")]
    public UnlockTriggerType triggerType;
    public string triggerTargetId;
    public ResourceType? triggerResourceType;
    public int triggerValue;

    [Header("View")]
    public string displayName;
    public string iconId;
    public ResourceType costResourceType;
    public int costAmount;

    [Header("Result")]
    public UnlockResultType resultType;
    public string resultTargetId;

    /// <summary>
    /// 결과 값입니다.
    /// 예: 무기 레벨 1, 자동 일꾼 3명, 감옥 수용량 +20.
    /// </summary>
    public int resultValue;

    /// <summary>
    /// 이 해금이 완료된 뒤 자동으로 공개할 다음 그룹입니다.
    /// 비어 있으면 다음 그룹 없음.
    /// </summary>
    public string nextRevealGroupId;

    [Header("Camera")]
    public string cameraTargetId;
    public bool useCameraFocus;
    public bool lockPlayer;
    public float cameraHoldSeconds;

    [Header("Reveal Motion")]
    public float popScale;
    public float popDuration;

    [Header("Option")]
    public bool deactivateAfterUnlock;
    public string note;

    /// <summary>
    /// SlotId가 비어 있을 때 사용할 안전한 슬롯 ID입니다.
    /// 현재 기존 데이터가 UnlockPointId 기준으로 되어 있어도 동작하게 하기 위한 보정입니다.
    /// </summary>
    public string EffectiveSlotId => !string.IsNullOrEmpty(slotId) ? slotId : unlockId;
}

/// <summary>
/// 해금 데이터 전체를 조회하기 위한 Model입니다.
/// 
/// 역할:
/// - UnlockId로 개별 데이터 조회
/// - RevealGroupId로 같은 타이밍에 공개될 데이터 조회
/// - Trigger 조건에 맞는 데이터 조회
/// </summary>
public sealed class UnlockProgressionModel
{
    private readonly List<UnlockPointData> allData;
    private readonly Dictionary<string, UnlockPointData> dataByUnlockId = new();
    private readonly Dictionary<string, List<UnlockPointData>> dataByRevealGroupId = new();

    public IReadOnlyList<UnlockPointData> AllData => allData;

    public UnlockProgressionModel(List<UnlockPointData> loadedData)
    {
        allData = loadedData ?? new List<UnlockPointData>();
        BuildLookup();
    }

    private void BuildLookup()
    {
        dataByUnlockId.Clear();
        dataByRevealGroupId.Clear();

        for (int i = 0; i < allData.Count; i++)
        {
            UnlockPointData data = allData[i];

            if (data == null)
                continue;

            if (!string.IsNullOrEmpty(data.unlockId))
            {
                if (!dataByUnlockId.ContainsKey(data.unlockId))
                    dataByUnlockId.Add(data.unlockId, data);
                else
                    Debug.LogWarning($"Duplicate UnlockId found: {data.unlockId}");
            }

            if (!string.IsNullOrEmpty(data.revealGroupId))
            {
                if (!dataByRevealGroupId.TryGetValue(data.revealGroupId, out List<UnlockPointData> groupList))
                {
                    groupList = new List<UnlockPointData>();
                    dataByRevealGroupId.Add(data.revealGroupId, groupList);
                }

                groupList.Add(data);
            }
        }
    }

    public bool TryGetByUnlockId(string unlockId, out UnlockPointData data)
    {
        data = null;

        if (string.IsNullOrEmpty(unlockId))
            return false;

        return dataByUnlockId.TryGetValue(unlockId, out data);
    }

    public bool TryGetRevealGroup(string revealGroupId, out IReadOnlyList<UnlockPointData> groupData)
    {
        groupData = null;

        if (string.IsNullOrEmpty(revealGroupId))
            return false;

        if (!dataByRevealGroupId.TryGetValue(revealGroupId, out List<UnlockPointData> list))
            return false;

        groupData = list;
        return true;
    }

    /// <summary>
    /// 특정 트리거 조건과 일치하는 해금 데이터를 찾습니다.
    /// 같은 조건으로 여러 개가 있을 수 있으므로 List로 반환합니다.
    /// </summary>
    public List<UnlockPointData> FindByTrigger(
        UnlockTriggerType triggerType,
        string triggerTargetId,
        ResourceType? triggerResourceType)
    {
        List<UnlockPointData> result = new();

        for (int i = 0; i < allData.Count; i++)
        {
            UnlockPointData data = allData[i];

            if (data == null)
                continue;

            if (data.triggerType != triggerType)
                continue;

            if (!IsTargetMatched(data.triggerTargetId, triggerTargetId))
                continue;

            if (!IsResourceMatched(data.triggerResourceType, triggerResourceType))
                continue;

            result.Add(data);
        }

        return result;
    }

    private bool IsTargetMatched(string dataTargetId, string signalTargetId)
    {
        // 데이터 쪽 TargetId가 비어 있으면 모든 타겟 허용으로 봅니다.
        if (string.IsNullOrEmpty(dataTargetId))
            return true;

        return dataTargetId == signalTargetId;
    }

    private bool IsResourceMatched(ResourceType? dataResourceType, ResourceType? signalResourceType)
    {
        // 데이터 쪽 ResourceType이 비어 있으면 모든 자원 허용으로 봅니다.
        if (!dataResourceType.HasValue)
            return true;

        if (!signalResourceType.HasValue)
            return false;

        return dataResourceType.Value == signalResourceType.Value;
    }
}

/// <summary>
/// UnlockPointProgressionData.txt를 UnlockPointData 리스트로 변환하는 Loader입니다.
/// 
/// 전제:
/// - 탭 구분 TSV 형식
/// - 첫 줄은 헤더
/// - 컬럼 순서는 바뀌어도 됨
/// - 헤더 이름 기준으로 파싱
/// </summary>
public static class UnlockPointProgressionTableLoader
{
    public static List<UnlockPointData> Load(TextAsset textAsset)
    {
        List<UnlockPointData> result = new();

        if (textAsset == null)
        {
            Debug.LogError("[UnlockPointProgressionTableLoader] TextAsset is null.");
            return result;
        }

        string text = textAsset.text;

        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogError("[UnlockPointProgressionTableLoader] TextAsset is empty.");
            return result;
        }

        string[] lines = text.Replace("\r", string.Empty).Split('\n');

        if (lines.Length <= 1)
        {
            Debug.LogError("[UnlockPointProgressionTableLoader] Data has no rows.");
            return result;
        }

        Dictionary<string, int> headerMap = BuildHeaderMap(lines[0]);

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // #으로 시작하는 줄은 주석으로 처리합니다.
            if (line.TrimStart().StartsWith("#"))
                continue;

            string[] columns = line.Split('\t');
            UnlockPointData data = ParseRow(columns, headerMap, i + 1);

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
            // UTF-8 BOM이 붙어 있을 수 있어서 제거합니다.
            string key = headers[i].Trim().Trim('\uFEFF');

            if (string.IsNullOrEmpty(key))
                continue;

            if (!map.ContainsKey(key))
                map.Add(key, i);
        }

        return map;
    }

    private static UnlockPointData ParseRow(
        string[] columns,
        Dictionary<string, int> headerMap,
        int lineNumber)
    {
        UnlockPointData data = new();

        // 현재 테이블의 UnlockPointId도 받고,
        // 나중에 UnlockId로 바꿔도 동작하게 둘 다 지원합니다.
        data.unlockId = GetFirst(columns, headerMap, "UnlockId", "UnlockPointId");

        if (string.IsNullOrEmpty(data.unlockId))
        {
            Debug.LogWarning($"[UnlockPointProgressionTableLoader] Line {lineNumber} has empty UnlockId.");
            return null;
        }

        data.revealGroupId = Get(columns, headerMap, "RevealGroupId");
        data.slotId = Get(columns, headerMap, "SlotId");
        data.revealOrder = ParseInt(Get(columns, headerMap, "RevealOrder"), 0);

        data.triggerType = ParseEnum(
            Get(columns, headerMap, "TriggerType"),
            UnlockTriggerType.None
        );

        data.triggerTargetId = GetFirst(columns, headerMap, "TriggerTargetId", "TriggerSourceId");
        data.triggerResourceType = ParseNullableEnum<ResourceType>(
            Get(columns, headerMap, "TriggerResourceType")
        );
        data.triggerValue = ParseInt(Get(columns, headerMap, "TriggerValue"), 1);

        data.displayName = Get(columns, headerMap, "DisplayName");
        data.iconId = Get(columns, headerMap, "IconId");

        data.costResourceType = ParseEnum(
            Get(columns, headerMap, "CostResourceType"),
            ResourceType.Money
        );
        data.costAmount = Mathf.Max(1, ParseInt(Get(columns, headerMap, "CostAmount"), 1));

        data.resultType = ParseEnum(
            Get(columns, headerMap, "ResultType"),
            UnlockResultType.None
        );

        data.resultTargetId = Get(columns, headerMap, "ResultTargetId");

        // 현재 파일은 ResultCount를 쓰고,
        // 재활용 구조에서는 ResultValue라는 이름이 더 범용적이라 둘 다 지원합니다.
        data.resultValue = ParseInt(
            GetFirst(columns, headerMap, "ResultValue", "ResultCount"),
            0
        );

        data.nextRevealGroupId = Get(columns, headerMap, "NextRevealGroupId");

        data.cameraTargetId = GetFirst(columns, headerMap, "CameraTargetId", "CameraTargetSlotId");
        data.useCameraFocus = ParseBool(Get(columns, headerMap, "UseCameraFocus"), false);
        data.lockPlayer = ParseBool(Get(columns, headerMap, "LockPlayer"), false);
        data.cameraHoldSeconds = ParseFloat(Get(columns, headerMap, "CameraHoldSeconds"), 0f);

        data.popScale = ParseFloat(Get(columns, headerMap, "PopScale"), 1.15f);
        data.popDuration = ParseFloat(Get(columns, headerMap, "PopDuration"), 0.25f);

        data.deactivateAfterUnlock = ParseBool(Get(columns, headerMap, "DeactivateAfterUnlock"), true);
        data.note = Get(columns, headerMap, "Note");

        return data;
    }

    private static string Get(string[] columns, Dictionary<string, int> headerMap, string columnName)
    {
        if (!headerMap.TryGetValue(columnName, out int index))
            return string.Empty;

        if (index < 0 || index >= columns.Length)
            return string.Empty;

        return columns[index].Trim();
    }

    private static string GetFirst(
        string[] columns,
        Dictionary<string, int> headerMap,
        params string[] columnNames)
    {
        for (int i = 0; i < columnNames.Length; i++)
        {
            string value = Get(columns, headerMap, columnNames[i]);

            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return string.Empty;
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

    private static bool ParseBool(string value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        value = value.Trim().ToLowerInvariant();

        return value switch
        {
            "true" => true,
            "1" => true,
            "yes" => true,
            "y" => true,
            "false" => false,
            "0" => false,
            "no" => false,
            "n" => false,
            _ => defaultValue
        };
    }

    private static T ParseEnum<T>(string value, T defaultValue) where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (Enum.TryParse(value, true, out T result))
            return result;

        Debug.LogWarning($"[UnlockPointProgressionTableLoader] Invalid enum value: {value}");
        return defaultValue;
    }

    private static T? ParseNullableEnum<T>(string value) where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Enum.TryParse(value, true, out T result))
            return result;

        Debug.LogWarning($"[UnlockPointProgressionTableLoader] Invalid nullable enum value: {value}");
        return null;
    }
}