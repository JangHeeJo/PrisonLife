using System;
using DG.Tweening;
using R3;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 감옥이 가득 찼을 때 감옥 확장 해금포인트를 튜토리얼처럼 보여주는 시퀀스입니다.
///
/// 흐름:
/// 1. PrisonFull 신호 수신
/// 2. 플레이어 조작 / 카메라 팔로우 잠시 비활성화
/// 3. 카메라를 해금포인트 포커스 위치로 이동
/// 4. 해금포인트 활성화
/// 5. 잠깐 보여준 뒤 카메라 원래 위치로 복귀
/// 6. 조작 / 카메라 팔로우 복구
/// </summary>
public sealed class PrisonFullUnlockSequence : MonoBehaviour
{
    [Header("Filter")]
    [SerializeField] private string targetPrisonId = "Prison_01";

    [Header("Camera")]
    [Tooltip("움직일 카메라 Transform입니다. 비워두면 Camera.main을 사용합니다.")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("감옥 확장 해금포인트를 보여줄 카메라 위치/회전 기준점입니다.")]
    [SerializeField] private Transform focusCameraPoint;

    [SerializeField] private float moveToFocusDuration = 0.65f;
    [SerializeField] private float stayDuration = 0.65f;
    [SerializeField] private float returnDuration = 0.65f;

    [SerializeField] private Ease moveEase = Ease.InOutQuad;
    [SerializeField] private Ease returnEase = Ease.InOutQuad;

    [Header("Unlock Point")]
    [Tooltip("20명 풀 상태가 되었을 때 활성화할 감옥 확장 해금포인트입니다. 예: Slot_Unlock_05")]
    [SerializeField] private GameObject prisonExpandUnlockPoint;

    [Tooltip("카메라가 포커스 위치에 도착한 뒤 해금포인트를 켭니다.")]
    [SerializeField] private bool revealUnlockPointAfterCameraArrive = true;

    [Header("Disable During Sequence")]
    [Tooltip("시퀀스 동안 잠시 꺼둘 컴포넌트들입니다. 예: PlayerController, JoystickInput, CameraFollow")]
    [SerializeField] private Behaviour[] disableDuringSequence;

    [Header("Events")]
    [SerializeField] private UnityEvent onSequenceStarted;
    [SerializeField] private UnityEvent onUnlockPointRevealed;
    [SerializeField] private UnityEvent onSequenceCompleted;

    [Header("Option")]
    [SerializeField] private bool playOnlyOnce = true;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private IDisposable subscription;
    private Sequence sequence;

    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;

    private bool played;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (prisonExpandUnlockPoint != null)
            prisonExpandUnlockPoint.SetActive(false);
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        subscription?.Dispose();
        subscription = null;

        sequence?.Kill();
    }

    private void OnDestroy()
    {
        sequence?.Kill();
    }

    private void TrySubscribe()
    {
        if (subscription != null)
            return;

        if (GameStateSignals.Instance == null)
            return;

        subscription = GameStateSignals.Instance.PrisonFull
            .Subscribe(OnPrisonFull);
    }

    private void OnPrisonFull(PrisonStateSignal signal)
    {
        if (playOnlyOnce && played)
            return;

        if (!string.IsNullOrEmpty(targetPrisonId) &&
            signal.PrisonId != targetPrisonId)
        {
            return;
        }

        PlaySequence();
    }

    [ContextMenu("Play Sequence")]
    public void PlaySequence()
    {
        if (playOnlyOnce && played)
            return;

        if (cameraTransform == null)
        {
            Debug.LogWarning("[PrisonFullUnlockSequence] Camera Transform is null.", this);
            return;
        }

        if (focusCameraPoint == null)
        {
            Debug.LogWarning("[PrisonFullUnlockSequence] Focus Camera Point is null.", this);
            return;
        }

        played = true;

        originalCameraPosition = cameraTransform.position;
        originalCameraRotation = cameraTransform.rotation;

        SetBehavioursEnabled(false);
        onSequenceStarted?.Invoke();

        sequence?.Kill();
        sequence = DOTween.Sequence();

        sequence.Append(
            cameraTransform
                .DOMove(focusCameraPoint.position, moveToFocusDuration)
                .SetEase(moveEase)
        );

        sequence.Join(
            cameraTransform
                .DORotateQuaternion(focusCameraPoint.rotation, moveToFocusDuration)
                .SetEase(moveEase)
        );

        sequence.AppendCallback(() =>
        {
            if (revealUnlockPointAfterCameraArrive)
                RevealUnlockPoint();
        });

        sequence.AppendInterval(stayDuration);

        sequence.Append(
            cameraTransform
                .DOMove(originalCameraPosition, returnDuration)
                .SetEase(returnEase)
        );

        sequence.Join(
            cameraTransform
                .DORotateQuaternion(originalCameraRotation, returnDuration)
                .SetEase(returnEase)
        );

        sequence.OnComplete(() =>
        {
            SetBehavioursEnabled(true);
            onSequenceCompleted?.Invoke();

            if (logState)
                Debug.Log("[PrisonFullUnlockSequence] Sequence Completed", this);
        });

        if (!revealUnlockPointAfterCameraArrive)
            RevealUnlockPoint();

        if (logState)
            Debug.Log("[PrisonFullUnlockSequence] Sequence Started", this);
    }

    private void RevealUnlockPoint()
    {
        if (prisonExpandUnlockPoint != null)
            prisonExpandUnlockPoint.SetActive(true);

        onUnlockPointRevealed?.Invoke();

        if (logState)
            Debug.Log("[PrisonFullUnlockSequence] Unlock Point Revealed", this);
    }

    private void SetBehavioursEnabled(bool enabled)
    {
        if (disableDuringSequence == null)
            return;

        for (int i = 0; i < disableDuringSequence.Length; i++)
        {
            if (disableDuringSequence[i] == null)
                continue;

            disableDuringSequence[i].enabled = enabled;
        }
    }
}