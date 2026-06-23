using UnityEngine;

/// <summary>
/// 자동화 유닛이 들고 다니는 자원 스택입니다.
///
/// 플레이어의 CarryStack처럼 시각적으로 자원을 쌓아 보여주는 역할을 합니다.
/// 실제 위치 계산과 시각 배치는 ResourceStackView가 담당합니다.
///
/// 사용 예:
/// - HandcuffDeliveryWorker가 수갑을 가져오면 TryAdd(Cuff)
/// - 수갑을 내려놓으면 TryRemove(Cuff)
/// </summary>
public sealed class UnitCarryStack : MonoBehaviour
{
    [Header("Resource")]
    [SerializeField] private ResourceType resourceType = ResourceType.Handcuff;

    [Header("Capacity")]
    [SerializeField, Min(1)] private int capacity = 10;

    [Header("View")]
    [Tooltip("플레이어처럼 Stack Layout 기반으로 표시하는 ResourceStackView입니다.")]
    [SerializeField] private ResourceStackView stackView;

    [Header("Debug")]
    [SerializeField] private int currentCount;

    public ResourceType ResourceType => resourceType;
    public int Capacity => capacity;
    public int CurrentCount => currentCount;
    public bool IsEmpty => currentCount <= 0;
    public bool IsFull => currentCount >= capacity;

    /// <summary>
    /// 자동화 유닛 생성 시 현재 CarryLimit을 적용합니다.
    /// </summary>
    public void SetCapacity(int newCapacity)
    {
        capacity = Mathf.Max(1, newCapacity);

        if (currentCount > capacity)
            currentCount = capacity;
    }

    public bool CanAdd(ResourceType addResourceType, int amount = 1)
    {
        if (amount <= 0)
            return false;

        if (resourceType != addResourceType)
            return false;

        if (currentCount + amount > capacity)
            return false;

        if (stackView != null && stackView.IsFull)
            return false;

        return true;
    }

    public bool TryAdd(ResourceType addResourceType, int amount = 1)
    {
        if (!CanAdd(addResourceType, amount))
            return false;

        for (int i = 0; i < amount; i++)
        {
            if (stackView != null && stackView.IsFull)
                break;

            currentCount++;

            if (stackView != null)
                stackView.ShowNext();
        }

        return true;
    }

    public bool CanRemove(ResourceType removeResourceType, int amount = 1)
    {
        if (amount <= 0)
            return false;

        if (resourceType != removeResourceType)
            return false;

        return currentCount >= amount;
    }

    public bool TryRemove(ResourceType removeResourceType, int amount = 1)
    {
        if (!CanRemove(removeResourceType, amount))
            return false;

        for (int i = 0; i < amount; i++)
        {
            if (currentCount <= 0)
                break;

            currentCount--;

            if (stackView != null)
                stackView.HideLast();
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        capacity = Mathf.Max(1, capacity);
        currentCount = Mathf.Clamp(currentCount, 0, capacity);
    }
#endif
}