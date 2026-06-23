using DG.Tweening;
using UnityEngine;

/// <summary>
/// 특정 오브젝트 위에 고정되어 표시되는 월드 화살표입니다.
/// 예: 광석 위, Deposit 포인트 위, Pickup 포인트 위 안내.
/// 
/// 데이터에서 받은 GuideOffsetX/Y/Z를 타겟 기준 로컬 Offset으로 사용합니다.
/// </summary>
public sealed class TutorialWorldAnchorArrow : MonoBehaviour
{
    [Header("Arrow")]
    [SerializeField] private Transform arrowRoot;

    [Tooltip("모델 자체의 축이 맞지 않을 때 보정할 회전값입니다.")]
    [SerializeField] private Vector3 rotationOffsetEuler;

    [Header("Motion Default")]
    [Tooltip("데이터의 GuideMoveDistance가 0 이하일 때 사용할 기본 이동 거리입니다.")]
    [SerializeField] private float defaultMoveDistance = 0.25f;

    [Tooltip("데이터의 GuideMoveDuration이 0 이하일 때 사용할 기본 이동 시간입니다.")]
    [SerializeField] private float defaultMoveDuration = 0.45f;

    [SerializeField] private Ease moveEase = Ease.InOutSine;

    private Transform target;
    private Vector3 guideOffset;
    private TutorialGuideDirection direction;

    private Tween moveTween;
    private float motionAmount;
    private float currentMoveDistance;
    private float currentMoveDuration;

    private void Awake()
    {
        Hide();
    }

    private void LateUpdate()
    {
        if (target == null || arrowRoot == null)
            return;

        UpdatePosition();
        UpdateRotation();
    }

    /// <summary>
    /// 월드 고정 화살표를 표시합니다.
    /// guideOffset은 GuideTarget 기준 로컬 위치 보정값입니다.
    /// </summary>
    public void Show(
        Transform newTarget,
        Vector3 newGuideOffset,
        TutorialGuideDirection newDirection,
        float dataMoveDistance,
        float dataMoveDuration)
    {
        target = newTarget;
        guideOffset = newGuideOffset;
        direction = newDirection;

        currentMoveDistance = dataMoveDistance > 0f ? dataMoveDistance : defaultMoveDistance;
        currentMoveDuration = dataMoveDuration > 0f ? dataMoveDuration : defaultMoveDuration;

        motionAmount = 0f;

        if (arrowRoot != null)
            arrowRoot.gameObject.SetActive(true);

        UpdatePosition();
        UpdateRotation();

        StartMoveMotion();
    }

    /// <summary>
    /// 월드 고정 화살표를 숨깁니다.
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
    /// 타겟 위치 + 데이터 Offset + 움직임 연출값으로 최종 위치를 계산합니다.
    /// </summary>
    private void UpdatePosition()
    {
        if (target == null || arrowRoot == null)
            return;

        Vector3 basePosition = target.position + target.TransformDirection(guideOffset);
        Vector3 motionOffset = GetMoveDirection() * motionAmount;

        arrowRoot.position = basePosition + motionOffset;
    }

    /// <summary>
    /// 화살표 방향을 GuideDirection 기준으로 맞춥니다.
    /// </summary>
    private void UpdateRotation()
    {
        if (target == null || arrowRoot == null)
            return;

        Quaternion baseRotation = direction switch
        {
            TutorialGuideDirection.Down => Quaternion.LookRotation(Vector3.down, Vector3.forward),
            TutorialGuideDirection.Forward => Quaternion.LookRotation(target.forward, Vector3.up),
            _ => Quaternion.identity
        };

        arrowRoot.rotation = baseRotation * Quaternion.Euler(rotationOffsetEuler);
    }

    /// <summary>
    /// 화살표가 콕콕 움직일 방향을 계산합니다.
    /// </summary>
    private Vector3 GetMoveDirection()
    {
        return direction switch
        {
            TutorialGuideDirection.Down => Vector3.down,
            TutorialGuideDirection.Forward when target != null => target.forward.normalized,
            _ => Vector3.down
        };
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
                UpdatePosition();
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