using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 감옥 문 컨트롤러입니다.
///
/// 역할:
/// - 문 열림/닫힘 연출만 담당합니다.
/// - 수감 카운트는 절대 처리하지 않습니다.
/// - 출시용 구조에서는 Trigger 감지보다 NpcProcessArea의 수동 호출을 권장합니다.
/// </summary>
public sealed class PrisonGateController : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private Transform doorTransform;

    [Tooltip("문이 열릴 때 아래로 내려가는 거리입니다.")]
    [SerializeField] private float openDownDistance = 2f;

    [Header("Tween")]
    [SerializeField] private float openDuration = 0.35f;
    [SerializeField] private float closeDuration = 0.35f;
    [SerializeField] private Ease openEase = Ease.OutQuad;
    [SerializeField] private Ease closeEase = Ease.InQuad;

    [Header("Trigger Option")]
    [Tooltip("true면 Trigger 감지로도 문을 열 수 있습니다. 출시용으로는 false 추천.")]
    [SerializeField] private bool useTriggerDetection;

    [Tooltip("true면 ProcessNpcUnit만 문을 열 수 있습니다.")]
    [SerializeField] private bool onlyProcessNpc = true;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private readonly HashSet<ProcessNpcUnit> detectedNpcs = new();

    private Vector3 closedLocalPosition;
    private Vector3 openedLocalPosition;

    private Tween doorTween;
    private bool isOpen;

    private void Awake()
    {
        if (doorTransform == null)
            doorTransform = transform;

        closedLocalPosition = doorTransform.localPosition;
        openedLocalPosition = closedLocalPosition + Vector3.down * openDownDistance;
    }

    public void OpenForNpc(ProcessNpcUnit npc)
    {
        if (npc != null)
            detectedNpcs.Add(npc);

        OpenDoor();
    }

    public void CloseForNpc(ProcessNpcUnit npc)
    {
        if (npc != null)
            detectedNpcs.Remove(npc);

        CleanupInvalidNpcs();

        if (detectedNpcs.Count <= 0)
            CloseDoor();
    }

    public void ForceClose()
    {
        detectedNpcs.Clear();
        CloseDoor();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useTriggerDetection)
            return;

        if (!TryGetValidNpc(other, out ProcessNpcUnit npc))
            return;

        OpenForNpc(npc);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!useTriggerDetection)
            return;

        if (!TryGetValidNpc(other, out ProcessNpcUnit npc))
            return;

        CloseForNpc(npc);
    }

    private bool TryGetValidNpc(Collider other, out ProcessNpcUnit npc)
    {
        npc = null;

        if (other == null)
            return false;

        npc = other.GetComponentInParent<ProcessNpcUnit>();

        if (onlyProcessNpc && npc == null)
            return false;

        return true;
    }

    private void OpenDoor()
    {
        if (doorTransform == null)
            return;

        if (isOpen)
            return;

        isOpen = true;

        KillDoorTween();

        doorTween = doorTransform
            .DOLocalMove(openedLocalPosition, openDuration)
            .SetEase(openEase)
            .SetLink(gameObject);

        if (logState)
            Debug.Log("[PrisonGateController] Open Door", this);
    }

    private void CloseDoor()
    {
        if (doorTransform == null)
            return;

        if (!isOpen)
            return;

        isOpen = false;

        KillDoorTween();

        doorTween = doorTransform
            .DOLocalMove(closedLocalPosition, closeDuration)
            .SetEase(closeEase)
            .SetLink(gameObject);

        if (logState)
            Debug.Log("[PrisonGateController] Close Door", this);
    }

    private void CleanupInvalidNpcs()
    {
        detectedNpcs.RemoveWhere(npc =>
            npc == null ||
            !npc.gameObject.activeInHierarchy
        );
    }

    private void KillDoorTween()
    {
        if (doorTween == null)
            return;

        if (doorTween.IsActive())
            doorTween.Kill(false);

        doorTween = null;
    }

    private void ResetDoorImmediate()
    {
        KillDoorTween();

        detectedNpcs.Clear();

        if (doorTransform != null)
            doorTransform.localPosition = closedLocalPosition;

        isOpen = false;
    }

    private void OnDisable()
    {
        ResetDoorImmediate();
    }

    private void OnDestroy()
    {
        KillDoorTween();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        openDownDistance = Mathf.Max(0f, openDownDistance);
        openDuration = Mathf.Max(0f, openDuration);
        closeDuration = Mathf.Max(0f, closeDuration);
    }
#endif
}
