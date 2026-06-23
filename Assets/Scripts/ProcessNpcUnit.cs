using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProcessNpcUnit : NpcUnitBase
{
    [Header("Receive")]
    [SerializeField] private Vector3 receiveOffset = new Vector3(0f, 1f, 0f);

    [Header("Visual State")]
    [Tooltip("범죄자 상태 모델 Root입니다.")]
    [SerializeField] private GameObject criminalModelRoot;

    [Tooltip("죄수 상태 모델 Root입니다.")]
    [SerializeField] private GameObject prisonerModelRoot;

    [Header("Progress UI Anchor")]
    [Tooltip("전역 NpcProcessProgressUI가 따라갈 NPC 기준 위치입니다. 비워두면 NPC Transform을 사용합니다.")]
    [SerializeField] private Transform progressAnchor;

    [Header("Legacy Bubble UI")]
    [Tooltip("NPC 프리팹 내부에 붙어 있는 기존 말풍선 UI Root입니다. 현재 전역 Progress UI를 쓰면 비워둬도 됩니다.")]
    [SerializeField] private GameObject bubbleRoot;

    [Tooltip("기존 말풍선 Fill 이미지입니다. Image Type은 Filled로 설정해야 fillAmount가 정상 동작합니다.")]
    [SerializeField] private Image bubbleFill;

    [Tooltip("기존 말풍선 수량 텍스트입니다.")]
    [SerializeField] private TMP_Text bubbleText;

    private bool isPrisonerVisual;

    public Vector3 ReceivePosition => transform.position + receiveOffset;

    public Transform ProgressAnchor => progressAnchor != null ? progressAnchor : transform;

    public bool IsPrisonerVisual => isPrisonerVisual;

    protected override void Awake()
    {
        base.Awake();

        SetCriminalVisual();
        HideProgress();
    }

    public override void ResetUnit()
    {
        base.ResetUnit();

        SetCriminalVisual();
        HideProgress();
    }

    public void SetCriminalVisual()
    {
        isPrisonerVisual = false;

        if (criminalModelRoot != null)
            criminalModelRoot.SetActive(true);

        if (prisonerModelRoot != null)
            prisonerModelRoot.SetActive(false);
    }

    public void SetPrisonerVisual()
    {
        isPrisonerVisual = true;

        if (criminalModelRoot != null)
            criminalModelRoot.SetActive(false);

        if (prisonerModelRoot != null)
            prisonerModelRoot.SetActive(true);
    }

    public void ShowProgress(int current, int max)
    {
        if (bubbleRoot != null)
            bubbleRoot.SetActive(true);

        if (bubbleText != null)
            bubbleText.text = $"{current}/{max}";

        float progress = max <= 0 ? 0f : (float)current / max;
        SetFill(progress);
    }

    public void HideProgress()
    {
        if (bubbleRoot != null)
            bubbleRoot.SetActive(false);

        if (bubbleText != null)
            bubbleText.text = string.Empty;

        SetFill(0f);
    }

    private void SetFill(float value)
    {
        if (bubbleFill == null)
            return;

        bubbleFill.fillAmount = Mathf.Clamp01(value);
    }
}