using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NPC 수갑 처리 진행도를 표시하는 UI.
/// 화면 UI지만 월드의 특정 Transform 위치를 따라가도록 구성한다.
/// </summary>
public class NpcProcessProgressUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private RectTransform root;

    [Header("Images")]
    [SerializeField] private Image bubbleBgImage;
    [SerializeField] private Image fillImage;

    [Header("Text")]
    [SerializeField] private TMP_Text countText;

    [Header("Follow")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.8f, 0f);

    private Canvas canvas;
    private Camera worldCamera;
    private RectTransform canvasRect;
    private Transform followTarget;

    private bool isVisible;

    /// <summary>
    /// UIManager에서 공통 Canvas / Camera를 전달받는다.
    /// </summary>
    public void Initialize(Canvas targetCanvas, Camera targetCamera)
    {
        canvas = targetCanvas;
        worldCamera = targetCamera;

        if (root == null)
            root = transform as RectTransform;

        if (canvas != null)
            canvasRect = canvas.transform as RectTransform;

        Hide();
    }

    private void Awake()
    {
        if (root == null)
            root = transform as RectTransform;
    }

    private void LateUpdate()
    {
        if (!isVisible)
            return;

        UpdateScreenPosition();
    }

    /// <summary>
    /// UI가 따라갈 월드 기준점을 지정한다.
    /// 예: NpcProcessArea의 ProgressAnchor 또는 QueuePoint[0].
    /// </summary>
    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        UpdateScreenPosition();
    }

    /// <summary>
    /// UI를 표시하고 현재 진행도를 갱신한다.
    /// </summary>
    public void Show(int current, int max)
    {
        isVisible = true;

        if (root != null)
            root.gameObject.SetActive(true);

        UpdateProgress(current, max);
        UpdateScreenPosition();
    }

    /// <summary>
    /// 진행도 텍스트와 Fill 값을 갱신한다.
    /// </summary>
    public void UpdateProgress(int current, int max)
    {
        int safeMax = Mathf.Max(1, max);
        int safeCurrent = Mathf.Clamp(current, 0, safeMax);

        if (countText != null)
            countText.text = $"{safeCurrent}/{safeMax}";

        if (fillImage != null)
            fillImage.fillAmount = (float)safeCurrent / safeMax;
    }

    /// <summary>
    /// UI를 숨기고 진행도를 초기화한다.
    /// </summary>
    public void Hide()
    {
        isVisible = false;
        followTarget = null;

        if (root != null)
            root.gameObject.SetActive(false);

        if (fillImage != null)
            fillImage.fillAmount = 0f;

        if (countText != null)
            countText.text = string.Empty;
    }

    /// <summary>
    /// 월드 좌표를 UI 좌표로 변환해서 root 위치를 갱신한다.
    /// </summary>
    private void UpdateScreenPosition()
    {
        if (followTarget == null || root == null || worldCamera == null)
            return;

        Vector3 worldPosition = followTarget.position + worldOffset;
        Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);

        // 카메라 뒤에 있으면 표시하지 않는다.
        if (screenPosition.z < 0f)
        {
            root.gameObject.SetActive(false);
            return;
        }

        if (!root.gameObject.activeSelf)
            root.gameObject.SetActive(true);

        // Screen Space - Overlay Canvas일 때는 screenPosition을 바로 사용.
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            root.position = screenPosition;
            return;
        }

        // Screen Space - Camera 또는 World Space Canvas 대응.
        if (canvasRect == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            canvas != null ? canvas.worldCamera : null,
            out Vector2 localPoint
        );

        root.anchoredPosition = localPoint;
    }
}