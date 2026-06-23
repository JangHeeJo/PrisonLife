using UnityEngine;

/// <summary>
/// 해금 포인트가 등장할 월드 위치 슬롯입니다.
///
/// 현재 구조:
/// Slot_Unlock
/// ├─ UnlockPoint
/// ├─ UnlockPointSlot
/// ├─ Collider
/// └─ UnlockPointView
///
/// 이 클래스는 SlotId, UnlockPoint, UnlockPointView 참조를 제공합니다.
/// 실제 돈 소모는 UnlockPoint가 담당하고,
/// UI 표시는 UnlockPointView가 담당합니다.
/// </summary>
public sealed class UnlockPointSlot : MonoBehaviour
{
    [Header("Slot")]
    [Tooltip("데이터의 SlotId와 매칭되는 ID입니다. 비워두면 오브젝트 이름을 사용합니다.")]
    [SerializeField] private string slotId;

    [Header("References")]
    [Tooltip("이 슬롯에서 실제 돈 투입/해금 처리를 담당하는 UnlockPoint입니다.")]
    [SerializeField] private UnlockPoint unlockPoint;

    [Tooltip("이 슬롯에 포함된 UnlockPointView입니다. 현재 구조에서는 Slot 하위에 미리 배치해둔 View를 사용합니다.")]
    [SerializeField] private UnlockPointView unlockPointView;

    [Tooltip("UI 위치 기준점입니다. 비워두면 Slot 자신의 Transform을 사용합니다.")]
    [SerializeField] private Transform uiAnchor;

    public string SlotId => slotId;

    public UnlockPoint UnlockPoint => unlockPoint;

    public UnlockPointView UnlockPointView => unlockPointView;

    /// <summary>
    /// UI가 따라갈 기준 위치입니다.
    /// 지금은 View가 Slot 하위에 있으므로 비워둬도 됩니다.
    /// </summary>
    public Transform UIAnchor => uiAnchor != null ? uiAnchor : transform;

    /// <summary>
    /// 특정 SlotId와 이 슬롯이 일치하는지 확인합니다.
    /// </summary>
    public bool IsMatch(string targetSlotId)
    {
        if (string.IsNullOrEmpty(targetSlotId))
            return false;

        return slotId == targetSlotId;
    }

    /// <summary>
    /// 슬롯의 상호작용 오브젝트를 켜거나 끕니다.
    /// Slot 자체는 꺼지지 않고, UnlockPoint만 제어합니다.
    /// </summary>
    public void SetPointActive(bool active)
    {
        if (unlockPoint == null)
            return;

        unlockPoint.gameObject.SetActive(active);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(slotId))
            slotId = gameObject.name;

        if (unlockPoint == null)
            unlockPoint = GetComponent<UnlockPoint>();

        if (unlockPointView == null)
            unlockPointView = GetComponentInChildren<UnlockPointView>(true);
    }
#endif
}