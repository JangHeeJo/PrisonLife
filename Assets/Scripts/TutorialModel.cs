using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Excel/TSV 한 줄에 해당하는 튜토리얼 Step 데이터입니다.
/// 기획 데이터는 Excel에서 관리하고, Unity에서는 TSV TextAsset으로 읽는 구조입니다.
/// </summary>
[Serializable]
public sealed class TutorialStepData
{
    public string stepId;
    public bool enabled = true;
    public string group;
    public TutorialRunMode runMode;

    public TutorialStartConditionType startConditionType;
    public string startTargetId;
    public ResourceType? startResourceType;
    public int startValue;

    public TutorialGuideType guideType;
    public string guideTargetId;

    /// <summary>
    /// 화살표 위치 보정값입니다.
    /// WorldAnchorArrow에서는 GuideTarget 기준 로컬 Offset,
    /// PlayerDirectionArrow에서는 Player 기준 로컬 Offset으로 사용합니다.
    /// </summary>
    public Vector3 guideOffset;

    public TutorialGuideDirection guideDirection;
    public bool hideWhenTargetVisible;

    /// <summary>
    /// 화살표가 목표 방향으로 움직이는 거리입니다.
    /// 0 이하이면 화살표 컴포넌트의 기본값을 사용합니다.
    /// </summary>
    public float guideMoveDistance;

    /// <summary>
    /// 화살표가 갔다가 돌아오는 Tween 시간입니다.
    /// 0 이하이면 화살표 컴포넌트의 기본값을 사용합니다.
    /// </summary>
    public float guideMoveDuration;

    public TutorialCompleteConditionType completeConditionType;
    public string completeTargetId;
    public ResourceType? completeResourceType;
    public int completeValue;
    public TutorialCompleteMode completeMode;

    public string cameraTargetId;
    public float cameraHoldSeconds;
    public bool lockInputDuringCamera;

    public string worldProgressionSignal;
    public string nextStepId;
    public string notes;
}

/// <summary>
/// 튜토리얼 데이터와 완료 상태를 관리하는 Model입니다.
/// 카메라, 화살표, GameObject 같은 View 요소는 전혀 알지 않습니다.
/// </summary>
public sealed class TutorialModel
{
    private readonly Dictionary<string, TutorialStepData> stepMap = new();
    private readonly HashSet<string> completedStepIds = new();

    public IReadOnlyList<TutorialStepData> Steps { get; }

    public TutorialModel(IReadOnlyList<TutorialStepData> steps)
    {
        Steps = steps;
        BuildStepMap(steps);
    }

    private void BuildStepMap(IReadOnlyList<TutorialStepData> steps)
    {
        stepMap.Clear();

        for (int i = 0; i < steps.Count; i++)
        {
            TutorialStepData step = steps[i];

            if (step == null || string.IsNullOrEmpty(step.stepId))
                continue;

            if (stepMap.ContainsKey(step.stepId))
            {
                Debug.LogWarning($"Duplicate Tutorial StepId : {step.stepId}");
                continue;
            }

            stepMap.Add(step.stepId, step);
        }
    }

    public bool TryGetStep(string stepId, out TutorialStepData step)
    {
        step = null;

        if (string.IsNullOrEmpty(stepId))
            return false;

        return stepMap.TryGetValue(stepId, out step);
    }

    public bool IsCompleted(string stepId)
    {
        return !string.IsNullOrEmpty(stepId) && completedStepIds.Contains(stepId);
    }

    public void MarkCompleted(string stepId)
    {
        if (string.IsNullOrEmpty(stepId))
            return;

        completedStepIds.Add(stepId);
    }
}

/// <summary>
/// Excel에서 Export한 TSV TextAsset을 TutorialStepData 리스트로 변환합니다.
/// 컬럼 순서가 바뀌어도 헤더 이름으로 읽습니다.
/// </summary>
public static class TutorialTableLoader
{
    public static List<TutorialStepData> LoadFromTextAsset(TextAsset textAsset)
    {
        List<TutorialStepData> steps = new();

        if (textAsset == null)
        {
            Debug.LogError("Tutorial table TextAsset is null.");
            return steps;
        }

        string[] lines = textAsset.text.Replace("\r", string.Empty).Split('\n');

        if (lines.Length <= 1)
        {
            Debug.LogError("Tutorial table has no data rows.");
            return steps;
        }

        Dictionary<string, int> headerMap = BuildHeaderMap(lines[0]);

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // #으로 시작하는 줄은 임시 주석으로 취급합니다.
            if (line.TrimStart().StartsWith("#"))
                continue;

            string[] columns = line.Split('\t');
            TutorialStepData step = ParseStep(columns, headerMap, i + 1);

            if (step != null)
                steps.Add(step);
        }

        return steps;
    }

    private static Dictionary<string, int> BuildHeaderMap(string headerLine)
    {
        Dictionary<string, int> map = new();
        string[] headers = headerLine.Split('\t');

        for (int i = 0; i < headers.Length; i++)
        {
            string key = headers[i].Trim();

            if (string.IsNullOrEmpty(key))
                continue;

            if (!map.ContainsKey(key))
                map.Add(key, i);
        }

        return map;
    }

    private static TutorialStepData ParseStep(
        string[] columns,
        Dictionary<string, int> headerMap,
        int lineNumber)
    {
        TutorialStepData step = new();

        step.stepId = Get(columns, headerMap, "StepId");
        step.enabled = ParseBool(Get(columns, headerMap, "Enabled"), true);
        step.group = Get(columns, headerMap, "Group");
        step.runMode = ParseEnum(Get(columns, headerMap, "RunMode"), TutorialRunMode.Sequential);

        step.startConditionType = ParseEnum(Get(columns, headerMap, "StartConditionType"), TutorialStartConditionType.None);
        step.startTargetId = Get(columns, headerMap, "StartTargetId");
        step.startResourceType = ParseNullableEnum<ResourceType>(Get(columns, headerMap, "StartResourceType"));
        step.startValue = ParseInt(Get(columns, headerMap, "StartValue"), 1);

        step.guideType = ParseEnum(Get(columns, headerMap, "GuideType"), TutorialGuideType.None);
        step.guideTargetId = Get(columns, headerMap, "GuideTargetId");

        float guideOffsetX = ParseFloat(Get(columns, headerMap, "GuideOffsetX"), 0f);
        float guideOffsetY = ParseFloat(Get(columns, headerMap, "GuideOffsetY"), 0f);
        float guideOffsetZ = ParseFloat(Get(columns, headerMap, "GuideOffsetZ"), 0f);
        step.guideOffset = new Vector3(guideOffsetX, guideOffsetY, guideOffsetZ);

        step.guideDirection = ParseEnum(Get(columns, headerMap, "GuideDirection"), TutorialGuideDirection.Down);
        step.hideWhenTargetVisible = ParseBool(Get(columns, headerMap, "HideWhenTargetVisible"), false);

        step.guideMoveDistance = ParseFloat(Get(columns, headerMap, "GuideMoveDistance"), 0f);
        step.guideMoveDuration = ParseFloat(Get(columns, headerMap, "GuideMoveDuration"), 0f);

        step.completeConditionType = ParseEnum(Get(columns, headerMap, "CompleteConditionType"), TutorialCompleteConditionType.None);
        step.completeTargetId = Get(columns, headerMap, "CompleteTargetId");
        step.completeResourceType = ParseNullableEnum<ResourceType>(Get(columns, headerMap, "CompleteResourceType"));
        step.completeValue = ParseInt(Get(columns, headerMap, "CompleteValue"), 1);
        step.completeMode = ParseEnum(Get(columns, headerMap, "CompleteMode"), TutorialCompleteMode.FirstChanged);

        step.cameraTargetId = GetFirst(columns, headerMap, "CameraTargetId", "GuideTargetId");
        step.cameraHoldSeconds = ParseFloat(Get(columns, headerMap, "CameraHoldSeconds"), 0f);
        step.lockInputDuringCamera = ParseBool(
            GetFirst(columns, headerMap, "LockInputDuringCamera", "LockPlayer"),
            false
        );

        step.worldProgressionSignal = Get(columns, headerMap, "WorldProgressionSignal");
        step.nextStepId = Get(columns, headerMap, "NextStepId");
        step.notes = GetFirst(columns, headerMap, "Notes", "Note");

        if (string.IsNullOrEmpty(step.stepId))
        {
            Debug.LogWarning($"Tutorial table line {lineNumber} has empty StepId.");
            return null;
        }

        return step;
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


    private static T ParseEnum<T>(string value, T defaultValue) where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (Enum.TryParse(value, true, out T result))
            return result;

        Debug.LogWarning($"Invalid enum value : {value}");
        return defaultValue;
    }

    private static T? ParseNullableEnum<T>(string value) where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Enum.TryParse(value, true, out T result))
            return result;

        Debug.LogWarning($"Invalid nullable enum value : {value}");
        return null;
    }

    private static int ParseInt(string value, int defaultValue)
    {
        return int.TryParse(value, out int result) ? result : defaultValue;
    }

    private static float ParseFloat(string value, float defaultValue)
    {
        return float.TryParse(value, out float result) ? result : defaultValue;
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
            "y" => true,
            "yes" => true,
            "false" => false,
            "0" => false,
            "n" => false,
            "no" => false,
            _ => defaultValue
        };
    }
}