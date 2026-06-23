using DG.Tweening;
using UnityEngine;

/// <summary>
/// 플레이어와 목표 사이에 표시되는 방향 안내 화살표입니다.
/// 
/// 핵심:
/// - 플레이어 근처에 있지만, 플레이어가 보는 방향이 아니라 목표 방향 쪽에 배치합니다.
/// - ArrowRoot 안에 DirectionMarker를 두고, ArrowRoot -> DirectionMarker 방향을 화살표의 실제 앞 방향으로 사용합니다.
/// - 화살표는 항상 목표 지점을 바라봅니다.
/// - DOTween 움직임도 화살표가 바라보는 방향으로 움직입니다.
/// </summary>
public sealed class TutorialPlayerDirectionArrow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform arrowRoot;

    [Tooltip("화살표 모델의 앞 방향을 표시하는 빈 오브젝트입니다. ArrowRoot의 자식으로 두고, 화살표 머리 쪽에 배치하세요.")]
    [SerializeField] private Transform directionMarker;

    [SerializeField] private Camera targetCamera;

    [Header("Position Default")]
    [Tooltip("데이터 GuideOffset이 모두 0일 때 사용할 기본 위치입니다. X=목표 방향 기준 좌우, Y=높이, Z=목표 방향 거리")]
    [SerializeField] private Vector3 defaultOffset = new Vector3(0f, 1.8f, 1.2f);

    [Header("Visible Check")]
    [SerializeField, Range(0f, 0.45f)] private float viewportPadding = 0.08f;

    [Header("Motion Default")]
    [Tooltip("데이터의 GuideMoveDistance가 0 이하일 때 사용할 기본 이동 거리입니다.")]
    [SerializeField] private float defaultMoveDistance = 0.25f;

    [Tooltip("데이터의 GuideMoveDuration이 0 이하일 때 사용할 기본 이동 시간입니다.")]
    [SerializeField] private float defaultMoveDuration = 0.45f;

    [SerializeField] private Ease moveEase = Ease.InOutSine;

    private Transform target;
    private bool hideWhenTargetVisible;

    private Vector3 currentOffset;

    private Tween moveTween;
    private float motionAmount;
    private float currentMoveDistance;
    private float currentMoveDuration;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        currentOffset = defaultOffset;

        Hide();
    }

    private void LateUpdate()
    {
        if (target == null || player == null || arrowRoot == null)
            return;

        UpdateTransform();
        UpdateVisibility();
    }

    /// <summary>
    /// 플레이어 방향 안내 화살표를 표시합니다.
    /// 
    /// dataOffset:
    /// X = 목표 방향 기준 좌우 보정
    /// Y = 높이
    /// Z = 플레이어에서 목표 방향으로 떨어질 거리
    /// </summary>
    public void Show(
        Transform newTarget,
        bool shouldHideWhenTargetVisible,
        Vector3 dataOffset,
        float dataMoveDistance,
        float dataMoveDuration)
    {
        target = newTarget;
        hideWhenTargetVisible = shouldHideWhenTargetVisible;

        currentOffset = dataOffset.sqrMagnitude > 0.0001f
            ? dataOffset
            : defaultOffset;

        currentMoveDistance = dataMoveDistance > 0f
            ? dataMoveDistance
            : defaultMoveDistance;

        currentMoveDuration = dataMoveDuration > 0f
            ? dataMoveDuration
            : defaultMoveDuration;

        motionAmount = 0f;

        if (arrowRoot != null)
            arrowRoot.gameObject.SetActive(true);

        UpdateTransform();
        UpdateVisibility();

        StartMoveMotion();
    }

    /// <summary>
    /// 플레이어 방향 안내 화살표를 숨깁니다.
    /// </summary>
    public void Hide()
    {
        StopMoveMotion();

        target = null;
        motionAmount = 0f;

        if (arrowRoot != null)
            arrowRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 위치와 회전을 함께 갱신합니다.
    /// 위치는 플레이어와 목표 사이에 배치하고,
    /// 회전은 DirectionMarker 기준으로 목표를 바라보게 맞춥니다.
    /// </summary>
    private void UpdateTransform()
    {
        if (player == null || target == null || arrowRoot == null)
            return;

        Vector3 targetDirection = GetDirectionFromPlayerToTarget();

        Vector3 right = Vector3.Cross(Vector3.up, targetDirection).normalized;

        float forwardDistance = Mathf.Max(0f, currentOffset.z);
        float distanceToTarget = GetFlatDistanceToTarget();

        // 목표가 가까우면 화살표가 목표를 넘어가지 않도록 제한합니다.
        if (distanceToTarget >= 0f)
        {
            float safeDistance = Mathf.Max(0f, distanceToTarget - 0.5f);
            forwardDistance = Mathf.Min(forwardDistance, safeDistance);
        }

        Vector3 basePosition =
            player.position
            + targetDirection * forwardDistance
            + right * currentOffset.x
            + Vector3.up * currentOffset.y;

        // 화살표가 실제 바라봐야 할 방향.
        Vector3 arrowForwardDirection = GetDirectionFromPositionToTarget(basePosition);

        // DOTween 움직임도 화살표가 바라보는 방향으로 적용합니다.
        Vector3 motionOffset = arrowForwardDirection * motionAmount;

        arrowRoot.position = basePosition + motionOffset;

        RotateArrowToDirection(arrowForwardDirection);
    }

    /// <summary>
    /// DirectionMarker를 기준으로 화살표가 목표 방향을 바라보게 회전합니다.
    /// </summary>
    private void RotateArrowToDirection(Vector3 desiredDirection)
    {
        if (arrowRoot == null)
            return;

        desiredDirection.y = 0f;

        if (desiredDirection.sqrMagnitude <= 0.001f)
            return;

        desiredDirection.Normalize();

        Vector3 currentVisualDirection = GetCurrentVisualDirection();
        currentVisualDirection.y = 0f;

        if (currentVisualDirection.sqrMagnitude <= 0.001f)
        {
            arrowRoot.rotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
            return;
        }

        currentVisualDirection.Normalize();

        // 현재 화살표의 실제 앞 방향을 desiredDirection에 맞추는 회전 차이.
        Quaternion deltaRotation = Quaternion.FromToRotation(
            currentVisualDirection,
            desiredDirection
        );

        arrowRoot.rotation = deltaRotation * arrowRoot.rotation;
    }

    /// <summary>
    /// 현재 화살표 모델이 실제로 바라보는 방향을 계산합니다.
    /// DirectionMarker가 있으면 ArrowRoot -> DirectionMarker 방향을 사용합니다.
    /// 없으면 ArrowRoot.forward를 사용합니다.
    /// </summary>
    private Vector3 GetCurrentVisualDirection()
    {
        if (arrowRoot == null)
            return Vector3.forward;

        if (directionMarker != null)
        {
            Vector3 markerDirection = directionMarker.position - arrowRoot.position;

            if (markerDirection.sqrMagnitude > 0.001f)
                return markerDirection.normalized;
        }

        return arrowRoot.forward;
    }

    /// <summary>
    /// 플레이어에서 목표까지의 평면 방향을 반환합니다.
    /// </summary>
    private Vector3 GetDirectionFromPlayerToTarget()
    {
        if (player == null || target == null)
            return Vector3.forward;

        Vector3 direction = target.position - player.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return player.forward;

        return direction.normalized;
    }

    /// <summary>
    /// 특정 위치에서 목표까지의 평면 방향을 반환합니다.
    /// </summary>
    private Vector3 GetDirectionFromPositionToTarget(Vector3 fromPosition)
    {
        if (target == null)
            return GetDirectionFromPlayerToTarget();

        Vector3 direction = target.position - fromPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return GetDirectionFromPlayerToTarget();

        return direction.normalized;
    }

    /// <summary>
    /// 플레이어와 목표 사이의 평면 거리입니다.
    /// </summary>
    private float GetFlatDistanceToTarget()
    {
        if (player == null || target == null)
            return -1f;

        Vector3 playerPosition = player.position;
        Vector3 targetPosition = target.position;

        playerPosition.y = 0f;
        targetPosition.y = 0f;

        return Vector3.Distance(playerPosition, targetPosition);
    }

    private void UpdateVisibility()
    {
        if (!hideWhenTargetVisible)
        {
            SetArrowActive(true);
            return;
        }

        bool isTargetVisible = IsTargetVisibleInCamera();
        SetArrowActive(!isTargetVisible);
    }

    private bool IsTargetVisibleInCamera()
    {
        if (targetCamera == null || target == null)
            return false;

        Vector3 viewport = targetCamera.WorldToViewportPoint(target.position);

        if (viewport.z <= 0f)
            return false;

        return viewport.x >= viewportPadding &&
               viewport.x <= 1f - viewportPadding &&
               viewport.y >= viewportPadding &&
               viewport.y <= 1f - viewportPadding;
    }

    private void SetArrowActive(bool active)
    {
        if (arrowRoot != null && arrowRoot.gameObject.activeSelf != active)
            arrowRoot.gameObject.SetActive(active);
    }

    private void StartMoveMotion()
    {
        StopMoveMotion();

        if (DoTweenManager.Instance == null)
            return;

        if (currentMoveDistance <= 0f || currentMoveDuration <= 0f)
            return;

        moveTween = DoTweenManager.Instance.PlayPingPongFloat(
            value =>
            {
                motionAmount = value;
                UpdateTransform();
            },
            currentMoveDistance,
            currentMoveDuration,
            moveEase
        );
    }

    private void StopMoveMotion()
    {
        if (DoTweenManager.Instance != null)
            DoTweenManager.Instance.KillTween(moveTween);

        moveTween = null;
        motionAmount = 0f;
    }
}