using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자원 비주얼 스택 관리.
/// 
/// Columns x Rows 형태의 바닥 배치를 지원하고,
/// 공간이 꽉 차면 Y 방향으로 한 층씩 쌓는다.
/// 예: Columns 2, Rows 3이면 한 층에 6개 배치.
/// </summary>
public class ResourceStackView : MonoBehaviour
{
    [Header("Resource")]
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private GameObject visualPrefab;
    [SerializeField, Min(1)] private int maxCount = 20;

    [Header("Grid Layout")]
    [Tooltip("가로 배치 개수")]
    [SerializeField, Min(1)] private int columns = 1;

    [Tooltip("세로 배치 줄 수. Unity 기준 Z 방향으로 배치된다.")]
    [SerializeField, Min(1)] private int rows = 1;

    [Tooltip("가로 간격")]
    [SerializeField] private float xSpacing = 0.25f;

    [Tooltip("세로 줄 간격. Unity 기준 Z 방향 간격이다.")]
    [SerializeField] private float zSpacing = 0.25f;

    [Tooltip("층 간격. 한 층이 다 차면 Y 방향으로 올라간다.")]
    [SerializeField] private float ySpacing = 0.15f;

    private int reservedCount;
    private int currentCount;

    private readonly List<GameObject> visuals = new List<GameObject>();

    public ResourceType ResourceType => resourceType;
    public GameObject VisualPrefab => visualPrefab;

    public int CurrentCount => currentCount;
    public int MaxCount => maxCount;

    public bool IsEmpty => currentCount <= 0;
    public bool IsFull => currentCount + reservedCount >= maxCount;

    private void Awake()
    {
        CreatePool();
    }

    /// <summary>
    /// 무기 업그레이드 등으로 포인트 최대치가 바뀔 때 호출합니다.
    /// 기존 비주얼 풀보다 큰 값이 들어오면 부족한 만큼 추가 생성합니다.
    /// </summary>
    public void SetMaxCount(int newMaxCount)
    {
        int safeMaxCount = Mathf.Max(1, newMaxCount);

        if (maxCount == safeMaxCount)
            return;

        maxCount = safeMaxCount;

        EnsurePoolSize();
        RefreshVisualPositions();

        // 보통은 최대치가 증가하지만, 혹시 줄어드는 경우 초과분을 숨깁니다.
        if (currentCount > maxCount)
        {
            for (int i = maxCount; i < currentCount; i++)
            {
                if (i >= 0 && i < visuals.Count && visuals[i] != null)
                    visuals[i].SetActive(false);
            }

            currentCount = maxCount;
        }

        int availableReserveCount = Mathf.Max(0, maxCount - currentCount);

        if (reservedCount > availableReserveCount)
            reservedCount = availableReserveCount;
    }

    /// <summary>
    /// 다음 자원이 쌓일 월드 위치를 반환한다.
    /// </summary>
    public bool TryGetNextWorldPosition(out Vector3 position)
    {
        position = transform.position;

        if (IsFull)
            return false;

        Vector3 localPosition = GetLocalPosition(currentCount);
        position = transform.TransformPoint(localPosition);

        return true;
    }

    /// <summary>
    /// 현재 가장 위 또는 마지막에 쌓인 자원의 월드 위치를 반환한다.
    /// </summary>
    public bool TryGetTopWorldPosition(out Vector3 position)
    {
        position = transform.position;

        if (IsEmpty)
            return false;

        int index = currentCount - 1;

        if (index < 0 || index >= visuals.Count)
            return false;

        position = visuals[index].transform.position;
        return true;
    }

    /// <summary>
    /// 다음 위치에 자원 비주얼을 표시한다.
    /// </summary>
    public bool ShowNext()
    {
        if (IsFull)
            return false;

        EnsurePoolSize();

        if (currentCount < 0 || currentCount >= visuals.Count)
            return false;

        GameObject visual = visuals[currentCount];

        visual.transform.localPosition = GetLocalPosition(currentCount);
        visual.transform.localRotation = Quaternion.identity;
        visual.SetActive(true);

        currentCount++;
        return true;
    }

    /// <summary>
    /// 마지막에 쌓인 자원 비주얼을 숨긴다.
    /// </summary>
    public bool HideLast()
    {
        if (IsEmpty)
            return false;

        currentCount--;

        if (currentCount < 0 || currentCount >= visuals.Count)
            return false;

        GameObject visual = visuals[currentCount];
        visual.SetActive(false);

        return true;
    }

    public bool CanReserve(int amount)
    {
        if (amount <= 0)
            return false;

        return currentCount + reservedCount + amount <= maxCount;
    }

    public bool TryReserveNextWorldPosition(out Vector3 position)
    {
        position = transform.position;

        if (!CanReserve(1))
            return false;

        int index = currentCount + reservedCount;
        position = transform.TransformPoint(GetLocalPosition(index));

        reservedCount++;
        return true;
    }

    public bool ShowReserved()
    {
        if (reservedCount <= 0)
            return false;

        EnsurePoolSize();

        if (currentCount < 0 || currentCount >= visuals.Count)
            return false;

        GameObject visual = visuals[currentCount];

        visual.transform.localPosition = GetLocalPosition(currentCount);
        visual.transform.localRotation = Quaternion.identity;
        visual.SetActive(true);

        currentCount++;
        reservedCount--;

        return true;
    }

    public void CancelReserved()
    {
        if (reservedCount > 0)
            reservedCount--;
    }

    private void CreatePool()
    {
        visuals.Clear();
        EnsurePoolSize();
        RefreshVisualPositions();
    }

    private void EnsurePoolSize()
    {
        if (visualPrefab == null)
            return;

        while (visuals.Count < maxCount)
        {
            int index = visuals.Count;

            GameObject visual = Instantiate(visualPrefab, transform);
            visual.transform.localPosition = GetLocalPosition(index);
            visual.transform.localRotation = Quaternion.identity;
            visual.SetActive(false);

            visuals.Add(visual);
        }
    }

    private void RefreshVisualPositions()
    {
        for (int i = 0; i < visuals.Count; i++)
        {
            if (visuals[i] == null)
                continue;

            visuals[i].transform.localPosition = GetLocalPosition(i);
        }
    }

    /// <summary>
    /// index 기준으로 Columns x Rows 배치 위치를 계산한다.
    /// 
    /// 예: Columns 2, Rows 3
    /// index 0~5는 2x3 바닥 배치
    /// index 6~11은 그 위층 배치
    /// </summary>
    private Vector3 GetLocalPosition(int index)
    {
        int safeColumns = Mathf.Max(1, columns);
        int safeRows = Mathf.Max(1, rows);

        int countPerLayer = safeColumns * safeRows;

        int layer = index / countPerLayer;
        int indexInLayer = index % countPerLayer;

        int column = indexInLayer % safeColumns;
        int row = indexInLayer / safeColumns;

        float startX = -(safeColumns - 1) * xSpacing * 0.5f;
        float startZ = -(safeRows - 1) * zSpacing * 0.5f;

        return new Vector3(
            startX + column * xSpacing,
            layer * ySpacing,
            startZ + row * zSpacing
        );
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxCount = Mathf.Max(1, maxCount);
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
    }
#endif
}