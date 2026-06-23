using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 감옥 확장 섹터 등장 연출입니다.
/// 
/// 씬에 미리 배치된 감옥 섹터를 시작 시 아래로 숨겨두고,
/// Reveal 호출 시 원래 위치로 솟구치듯 올립니다.
/// 
/// 기존 콜백 방식 Reveal(Action)은 유지하고,
/// UniTask 기반 RevealAsync를 추가해서 외부에서 await할 수 있게 합니다.
/// </summary>
public sealed class PrisonSectorExpansionView : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform sectorTransform;

    [Header("Hidden Position")]
    [Tooltip("아래에서 솟구치게 하려면 Y를 음수로 둡니다.")]
    [SerializeField] private Vector3 hiddenOffset = new Vector3(0f, -4f, 0f);

    [SerializeField] private bool hideOnAwake = true;

    [Header("Tween")]
    [SerializeField] private float revealDuration = 0.55f;
    [SerializeField] private Ease revealEase = Ease.OutBack;

    [Header("Punch")]
    [SerializeField] private bool usePunchScale = true;
    [SerializeField] private float punchScale = 0.08f;
    [SerializeField] private float punchDuration = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private Vector3 shownLocalPosition;
    private Vector3 hiddenLocalPosition;
    private Vector3 originalScale;

    private Sequence sequence;
    private bool revealed;

    public bool IsRevealed => revealed;

    private void Awake()
    {
        if (sectorTransform == null)
            sectorTransform = transform;

        shownLocalPosition = sectorTransform.localPosition;
        hiddenLocalPosition = shownLocalPosition + hiddenOffset;
        originalScale = sectorTransform.localScale;

        if (hideOnAwake)
            HideImmediate();
    }

    /// <summary>
    /// 기존 코드 호환용 콜백 API입니다.
    /// Unlock/Expansion 쪽 기존 호출부를 깨지 않기 위해 유지합니다.
    /// </summary>
    public void Reveal(Action onComplete = null)
    {
        RevealWithCallbackAsync(onComplete).Forget();
    }

    private async UniTaskVoid RevealWithCallbackAsync(Action onComplete)
    {
        try
        {
            await RevealAsync(this.GetCancellationTokenOnDestroy());
            onComplete?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// UniTask 기반 섹터 등장 연출입니다.
    /// 외부에서 await로 연출 완료 시점을 기다릴 수 있습니다.
    /// </summary>
    public async UniTask RevealAsync(CancellationToken cancellationToken)
    {
        if (revealed)
            return;

        cancellationToken.ThrowIfCancellationRequested();

        revealed = true;

        KillSequence();

        sectorTransform.gameObject.SetActive(true);
        sectorTransform.localPosition = hiddenLocalPosition;
        sectorTransform.localScale = originalScale;

        if (logState)
            Debug.Log("[PrisonSectorExpansionView] Reveal Started", this);

        sequence = DOTween.Sequence()
            .SetLink(gameObject);

        sequence.Append(
            sectorTransform
                .DOLocalMove(shownLocalPosition, revealDuration)
                .SetEase(revealEase)
        );

        if (usePunchScale)
        {
            sequence.Append(
                sectorTransform
                    .DOPunchScale(Vector3.one * punchScale, punchDuration, 8, 0.8f)
            );
        }

        UniTaskCompletionSource completionSource = new UniTaskCompletionSource();

        bool completed = false;

        void Complete()
        {
            if (completed)
                return;

            completed = true;
            completionSource.TrySetResult();
        }

        sequence.OnComplete(Complete);

        using (cancellationToken.Register(() =>
        {
            if (completed)
                return;

            completed = true;

            KillSequence();
            completionSource.TrySetCanceled(cancellationToken);
        }))
        {
            await completionSource.Task;
        }

        sectorTransform.localPosition = shownLocalPosition;
        sectorTransform.localScale = originalScale;

        if (logState)
            Debug.Log("[PrisonSectorExpansionView] Reveal Completed", this);
    }
    /// <summary>
    /// 저장된 감옥 확장 상태를 복원할 때 연출 없이 바로 표시합니다.
    /// </summary>
    public void ShowImmediate()
    {
        KillSequence();

        if (sectorTransform == null)
            return;

        sectorTransform.gameObject.SetActive(true);
        sectorTransform.localPosition = shownLocalPosition;
        sectorTransform.localScale = originalScale;

        revealed = true;
    }
    /// <summary>
    /// 에디터 테스트나 재사용이 필요할 때 섹터를 다시 숨기는 함수입니다.
    /// 런타임에서 확장을 되돌릴 일이 없다면 호출하지 않아도 됩니다.
    /// </summary>
    public void HideImmediate()
    {
        KillSequence();

        if (sectorTransform == null)
            return;

        sectorTransform.localPosition = hiddenLocalPosition;
        sectorTransform.localScale = originalScale;
        sectorTransform.gameObject.SetActive(false);

        revealed = false;
    }

    private void KillSequence()
    {
        if (sequence == null)
            return;

        if (sequence.IsActive())
            sequence.Kill(false);

        sequence = null;
    }

    private void OnDestroy()
    {
        KillSequence();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        revealDuration = Mathf.Max(0f, revealDuration);
        punchDuration = Mathf.Max(0f, punchDuration);
        punchScale = Mathf.Max(0f, punchScale);

        if (sectorTransform == null)
            sectorTransform = transform;
    }
#endif
}