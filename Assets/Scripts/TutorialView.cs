using System.Collections;
using UnityEngine;

/// <summary>
/// 튜토리얼 View 구현체입니다.
/// 오브젝트 위 고정 화살표, 플레이어 방향 화살표, 카메라 포커스, 입력 잠금을 실제로 처리합니다.
/// </summary>
public sealed class TutorialView : MonoBehaviour, ITutorialView
{
    [Header("Targets")]
    [SerializeField] private TutorialTargetRegistry targetRegistry;

    [Header("Guides")]
    [SerializeField] private TutorialWorldAnchorArrow worldAnchorArrow;
    [SerializeField] private TutorialPlayerDirectionArrow playerDirectionArrow;

    [Header("Camera")]
    [SerializeField] private QuarterViewCameraController cameraController;
    [SerializeField] private float defaultCameraHoldSeconds = 1f;

    [Header("Player")]
    [SerializeField] private PlayerController playerController;

    private Coroutine cameraFocusRoutine;

    public void PlayGuide(TutorialStepData step)
    {
        if (step == null)
            return;

        switch (step.guideType)
        {
            case TutorialGuideType.WorldAnchorArrow:
                ShowWorldAnchorArrow(step);
                break;

            case TutorialGuideType.PlayerDirectionArrow:
                ShowPlayerDirectionArrow(step);
                break;

            case TutorialGuideType.CameraFocus:
                PlayCameraFocus(step);
                break;
        }
    }

    public void StopGuide(TutorialStepData step)
    {
        if (step == null)
            return;

        switch (step.guideType)
        {
            case TutorialGuideType.WorldAnchorArrow:
                worldAnchorArrow?.Hide();
                break;

            case TutorialGuideType.PlayerDirectionArrow:
                playerDirectionArrow?.Hide();
                break;

            case TutorialGuideType.CameraFocus:
                // 카메라 포커스는 복귀 후 완료 신호가 발생하므로 여기서 강제 중단하지 않습니다.
                break;
        }
    }

    public void HideAllGuides()
    {
        worldAnchorArrow?.Hide();
        playerDirectionArrow?.Hide();

        if (cameraFocusRoutine != null)
        {
            StopCoroutine(cameraFocusRoutine);
            cameraFocusRoutine = null;
        }

        SetPlayerInputLocked(false);
    }

    private void ShowWorldAnchorArrow(TutorialStepData step)
    {
        if (worldAnchorArrow == null)
            return;

        if (!TryGetTarget(step.guideTargetId, out Transform target))
            return;

        worldAnchorArrow.Show(
            target,
            step.guideOffset,
            step.guideDirection,
            step.guideMoveDistance,
            step.guideMoveDuration
        );
    }

    private void ShowPlayerDirectionArrow(TutorialStepData step)
    {
        if (playerDirectionArrow == null)
            return;

        if (!TryGetTarget(step.guideTargetId, out Transform target))
            return;

        playerDirectionArrow.Show(
            target,
            step.hideWhenTargetVisible,
            step.guideOffset,
            step.guideMoveDistance,
            step.guideMoveDuration
        );
    }

    private void PlayCameraFocus(TutorialStepData step)
    {
        string targetId = !string.IsNullOrEmpty(step.cameraTargetId)
            ? step.cameraTargetId
            : step.guideTargetId;

        if (!TryGetTarget(targetId, out Transform target))
            return;

        if (cameraController == null)
            return;

        if (cameraFocusRoutine != null)
            StopCoroutine(cameraFocusRoutine);

        cameraFocusRoutine = StartCoroutine(CameraFocusRoutine(step, targetId, target));
    }

    private IEnumerator CameraFocusRoutine(
        TutorialStepData step,
        string targetId,
        Transform target)
    {
        if (step.lockInputDuringCamera)
            SetPlayerInputLocked(true);

        bool focusComplete = false;

        cameraController.FocusOnTarget(
            target,
            null,
            null,
            () => focusComplete = true
        );

        while (!focusComplete)
            yield return null;

        float holdSeconds = step.cameraHoldSeconds > 0f
            ? step.cameraHoldSeconds
            : defaultCameraHoldSeconds;

        if (holdSeconds > 0f)
            yield return new WaitForSeconds(holdSeconds);

        bool returnComplete = false;

        cameraController.ReturnToPlayer(
            null,
            () => returnComplete = true
        );

        while (!returnComplete)
            yield return null;

        if (step.lockInputDuringCamera)
            SetPlayerInputLocked(false);

        // 카메라가 완전히 복귀한 뒤 ObjectiveTracker가 받을 완료 신호를 발생시킵니다.
        GameStateSignals.RaiseCameraReturned(targetId);

        cameraFocusRoutine = null;
    }

    private void SetPlayerInputLocked(bool isLocked)
    {
        if (playerController == null)
            return;

        playerController.SetInputLocked(isLocked);
    }

    private bool TryGetTarget(string targetId, out Transform target)
    {
        target = null;

        if (targetRegistry == null)
            return false;

        return targetRegistry.TryGetTarget(targetId, out target);
    }
}