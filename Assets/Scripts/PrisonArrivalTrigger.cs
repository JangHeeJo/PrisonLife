using UnityEngine;

/// <summary>
/// NPC가 감옥 안쪽에 실제로 들어왔을 때 PrisonAreaState에 수감 인원을 추가하는 Trigger입니다.
/// 
/// NpcProcessArea를 수정하지 않고 감옥 시스템 안에서 인원 카운트를 처리합니다.
/// </summary>
public sealed class PrisonArrivalTrigger : MonoBehaviour
{
    [Header("Prison")]
    [SerializeField] private PrisonAreaState prisonAreaState;

    [Header("Filter")]
    [Tooltip("true면 ProcessNpcUnit이 있는 오브젝트만 카운트합니다.")]
    [SerializeField] private bool onlyProcessNpc = true;

    [Header("Option")]
    [Tooltip("수감 처리 후 NPC 오브젝트를 비활성화할지 여부입니다.")]
    [SerializeField] private bool deactivateNpcOnArrival = true;

    [Tooltip("감옥이 가득 찬 상태에서는 더 이상 카운트하지 않습니다.")]
    [SerializeField] private bool ignoreWhenFull = true;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private void Awake()
    {
        if (prisonAreaState == null)
            prisonAreaState = GetComponentInParent<PrisonAreaState>();
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessNpcUnit npc = other.GetComponentInParent<ProcessNpcUnit>();

        if (onlyProcessNpc && npc == null)
            return;

        if (npc == null)
            return;

        if (prisonAreaState == null)
        {
            Debug.LogWarning("[PrisonArrivalTrigger] PrisonAreaState is null.", this);
            return;
        }

        if (ignoreWhenFull && prisonAreaState.IsFull)
        {
            if (logState)
                Debug.Log("[PrisonArrivalTrigger] Prison is full. Count ignored.", this);

            return;
        }

        prisonAreaState.AddPrisoner(1);

        if (logState)
        {
            Debug.Log(
                $"[PrisonArrivalTrigger] NPC Arrived: {npc.name}, " +
                $"Count: {prisonAreaState.CurrentCount}/{prisonAreaState.MaxCount}",
                this
            );
        }

        if (deactivateNpcOnArrival)
            npc.gameObject.SetActive(false);
    }
}