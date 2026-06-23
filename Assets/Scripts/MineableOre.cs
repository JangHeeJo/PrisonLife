using System.Collections;
using UnityEngine;

/// <summary>
/// 채굴 가능한 광석입니다.
/// 수량이 0이 되면 비주얼/콜라이더를 끄고, 일정 시간 후 다시 리스폰합니다.
/// 
/// 튜토리얼은 MineableOre를 직접 참조하지 않고,
/// TryMine 성공 시 발생하는 Interacted 신호를 관찰해서 최초 광석 상호작용을 판단합니다.
/// </summary>
public class MineableOre : MonoBehaviour
{
    [Header("Ore")]
    [SerializeField] private int maxAmount = 1;
    [SerializeField] private int amountPerHit = 1;
    [SerializeField] private float respawnDelay = 5f;

    [Header("References")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Collider oreCollider;

    [Header("State Signal")]
    [Tooltip("튜토리얼/미션에서 이 광석 필드를 구분하기 위한 ID입니다.")]
    [SerializeField] private string interactTargetId = "OreField_01";

    private int currentAmount;
    private bool isRespawning;
    private Renderer[] renderers;
    private Coroutine respawnRoutine;

    public bool CanMine => currentAmount > 0 && !isRespawning;

    private void Awake()
    {
        if (oreCollider == null)
            oreCollider = GetComponent<Collider>();

        // visualRoot가 없거나 잘못 설정됐을 때 Renderer를 직접 켜고 끄기 위해 캐싱합니다.
        renderers = GetComponentsInChildren<Renderer>(true);

        currentAmount = maxAmount;
        SetOreVisible(true);
    }

    public bool TryMine(out int amount)
    {
        amount = 0;

        if (!CanMine)
            return false;

        currentAmount--;
        amount = amountPerHit;

        // 광석과 상호작용했다는 일반 게임 상태 신호입니다.
        // Tutorial 전용 호출이 아니라, 업적/퀘스트/미션에서도 재사용 가능합니다.
        GameStateSignals.RaiseInteracted(interactTargetId);

        if (currentAmount <= 0 && respawnRoutine == null)
            respawnRoutine = StartCoroutine(RespawnRoutine());

        return true;
    }

    private IEnumerator RespawnRoutine()
    {
        isRespawning = true;
        SetOreVisible(false);

        yield return new WaitForSeconds(respawnDelay);

        currentAmount = maxAmount;
        isRespawning = false;
        SetOreVisible(true);

        respawnRoutine = null;
    }

    private void SetOreVisible(bool visible)
    {
        // visualRoot가 자기 자신이면 루트 전체를 끄면 스크립트까지 비활성화될 수 있으므로 Renderer만 제어합니다.
        if (visualRoot != null && visualRoot != gameObject)
        {
            visualRoot.SetActive(visible);
        }
        else
        {
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = visible;
        }

        if (oreCollider != null)
            oreCollider.enabled = visible;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxAmount < 1)
            maxAmount = 1;

        if (amountPerHit < 1)
            amountPerHit = 1;

        if (respawnDelay < 0f)
            respawnDelay = 0f;
    }
#endif
}
