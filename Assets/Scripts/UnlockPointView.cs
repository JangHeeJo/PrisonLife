using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Slot_Unlock 하위에 들어가는 해금 포인트 View입니다.
///
/// 중요:
/// - 이 스크립트가 붙은 UnlockPointView 오브젝트 자체는 끄지 않습니다.
/// - 하위 ViewRoot만 켜고 끕니다.
/// - 이렇게 해야 Manager/Presenter가 항상 View를 안정적으로 참조할 수 있습니다.
/// </summary>
public sealed class UnlockPointView : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("실제로 켜고 끌 UI Root입니다. 보통 하위 Canvas 또는 UI Content Root를 연결합니다.")]
    [SerializeField] private RectTransform viewRoot;

    [Header("Text")]
    [Tooltip("비용 또는 진행도 텍스트입니다. 예: 0/10")]
    [SerializeField] private TMP_Text costText;

    [Header("Image")]
    [Tooltip("해금 대상 아이콘 이미지입니다.")]
    [SerializeField] private Image iconImage;

    [Tooltip("진행도 Fill 이미지입니다. Image Type은 Filled로 설정해야 합니다.")]
    [SerializeField] private Image fillImage;

    [Header("Follow Option")]
    [Tooltip("true면 followTarget 위치를 따라갑니다. 현재 Slot 하위 배치 구조에서는 false 권장입니다.")]
    [SerializeField] private bool followTargetPosition = false;

    [Tooltip("followTargetPosition이 true일 때 사용할 월드 위치 보정값입니다.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.8f, 0f);

    [Tooltip("true면 카메라를 바라보도록 회전합니다. 프리팹에서 회전을 직접 맞췄다면 false 권장입니다.")]
    [SerializeField] private bool faceCamera = false;

    [SerializeField] private Camera targetCamera;

    [Header("Pop")]
    [Tooltip("데이터 PopScale이 1 이하일 때 사용할 기본 증가 비율입니다. 0.15 = 현재 스케일의 15%")]
    [SerializeField] private float defaultPunchScaleRatio = 0.15f;

    [SerializeField] private int punchVibrato = 1;
    [SerializeField] private float punchElasticity = 0.4f;
    [SerializeField] private Ease popEase = Ease.OutQuad;

    private Transform followTarget;
    private Vector3 originalScale;
    private Tween popTween;

    private void Awake()
    {
        CacheReferences();

        if (viewRoot != null)
            originalScale = viewRoot.localScale;

        Hide();
    }

    private void LateUpdate()
    {
        if (!followTargetPosition)
            return;

        if (followTarget == null)
            return;

        UpdateWorldTransform();
    }

    private void CacheReferences()
    {
        // ViewRoot를 비워둔 경우, 첫 번째 자식 RectTransform을 자동 사용합니다.
        // 예: UnlockPointView
        //     └─ Canvas
        if (viewRoot == null && transform.childCount > 0)
            viewRoot = transform.GetChild(0) as RectTransform;

        // 자식이 없으면 마지막 fallback으로 자기 RectTransform을 사용합니다.
        // 단, 이 경우 자기 자신이 꺼질 수 있으니 되도록 하위 Canvas/ViewRoot를 연결하세요.
        if (viewRoot == null)
            viewRoot = transform as RectTransform;

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    /// <summary>
    /// 데이터와 아이콘을 View에 반영합니다.
    /// </summary>
    public void Bind(UnlockPointData data, Sprite iconSprite)
    {
        if (data == null)
            return;

        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.enabled = iconSprite != null;
        }

        SetProgress(0, data.costAmount);
    }

    /// <summary>
    /// View를 표시합니다.
    /// UnlockPointView 오브젝트 자체가 아니라 viewRoot만 켭니다.
    /// </summary>
    public void Show(Transform newFollowTarget, float popScale, float popDuration)
    {
        CacheReferences();

        followTarget = newFollowTarget;

        if (viewRoot == null)
            return;

        originalScale = viewRoot.localScale;

        viewRoot.gameObject.SetActive(true);

        if (followTargetPosition)
            UpdateWorldTransform();

        PlayPop(popScale, popDuration);
    }

    /// <summary>
    /// View를 숨깁니다.
    /// UnlockPointView 오브젝트 자체는 끄지 않고 viewRoot만 끕니다.
    /// </summary>
    public void Hide()
    {
        followTarget = null;

        if (popTween != null)
        {
            popTween.Kill();
            popTween = null;
        }

        if (viewRoot != null)
        {
            if (originalScale != Vector3.zero)
                viewRoot.localScale = originalScale;

            viewRoot.gameObject.SetActive(false);
        }

        if (fillImage != null)
            fillImage.fillAmount = 0f;

        if (costText != null)
            costText.text = string.Empty;
    }

    /// <summary>
    /// 해금 진행도를 UI에 반영합니다.
    /// </summary>
    public void SetProgress(int current, int max)
    {
        int safeMax = Mathf.Max(1, max);
        int safeCurrent = Mathf.Clamp(current, 0, safeMax);

        float progress = (float)safeCurrent / safeMax;

        if (fillImage != null)
            fillImage.fillAmount = progress;

        if (costText != null)
            costText.text = $"{safeCurrent}/{safeMax}";
    }

    /// <summary>
    /// followTargetPosition이 true일 때만 사용합니다.
    /// 현재 Slot 하위 배치 구조에서는 보통 사용하지 않습니다.
    /// </summary>
    private void UpdateWorldTransform()
    {
        if (followTarget == null || viewRoot == null)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        viewRoot.position = followTarget.position + worldOffset;

        if (!faceCamera || targetCamera == null)
            return;

        Vector3 directionToCamera = viewRoot.position - targetCamera.transform.position;

        if (directionToCamera.sqrMagnitude <= 0.001f)
            return;

        viewRoot.rotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up);
    }

    /// <summary>
    /// 생성 시 현재 스케일 기준으로 살짝 커졌다가 돌아오는 연출입니다.
    /// Vector3.one으로 스케일을 돌리지 않습니다.
    /// </summary>
    private void PlayPop(float popScale, float popDuration)
    {
        if (viewRoot == null)
            return;

        if (popTween != null)
        {
            popTween.Kill();
            popTween = null;
        }

        if (originalScale == Vector3.zero)
            originalScale = viewRoot.localScale;

        float safeDuration = popDuration > 0f ? popDuration : 0.25f;

        float ratio = popScale > 1f
            ? popScale - 1f
            : defaultPunchScaleRatio;

        Vector3 punchAmount = originalScale * ratio;

        viewRoot.localScale = originalScale;

        popTween = viewRoot
            .DOPunchScale(
                punchAmount,
                safeDuration,
                punchVibrato,
                punchElasticity
            )
            .SetEase(popEase)
            .OnComplete(() =>
            {
                if (viewRoot != null)
                    viewRoot.localScale = originalScale;
            });
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (viewRoot == null && transform.childCount > 0)
            viewRoot = transform.GetChild(0) as RectTransform;
    }
#endif
}