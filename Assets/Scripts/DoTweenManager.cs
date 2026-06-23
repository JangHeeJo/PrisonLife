using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 자원 이동, 반복 연출 등 DOTween 기반 연출을 공통 처리하는 매니저입니다.
/// 게임 로직은 여기 넣지 않고, "어떻게 움직일지"만 담당합니다.
/// 
/// 기존 콜백 기반 PlayJump는 유지하고,
/// UniTask 기반 PlayJumpAsync를 추가해서 순차 연출을 await로 처리할 수 있게 합니다.
/// </summary>
public class DoTweenManager : MonoBehaviour
{
    public static DoTweenManager Instance { get; private set; }

    [Header("Pool Root")]
    [SerializeField] private Transform effectRoot;

    // 프리팹별로 풀을 따로 관리합니다.
    private readonly Dictionary<GameObject, Queue<GameObject>> poolMap = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (effectRoot == null)
            effectRoot = transform;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 고정된 목표 위치로 포물선 이동합니다.
    /// 예: 플레이어 → 고정 StackView, NPC → MoneyRewardPoint
    /// 
    /// 기존 코드 호환용 콜백 버전입니다.
    /// </summary>
    public void PlayJump(
        GameObject prefab,
        Vector3 startPosition,
        Vector3 endPosition,
        float jumpPower,
        float duration,
        Action onComplete = null,
        float delay = 0f,
        Ease ease = Ease.OutQuad)
    {
        PlayJump(
            prefab,
            startPosition,
            () => endPosition,
            jumpPower,
            duration,
            onComplete,
            delay,
            ease
        );
    }

    /// <summary>
    /// 움직이는 목표 위치를 계속 추적하면서 포물선 이동합니다.
    /// 예: 돈이 이동 중인 플레이어 스택 위치를 따라갈 때 사용합니다.
    /// 
    /// 기존 코드 호환용 콜백 버전입니다.
    /// </summary>
    public void PlayJump(
        GameObject prefab,
        Vector3 startPosition,
        Func<Vector3> getEndPosition,
        float jumpPower,
        float duration,
        Action onComplete = null,
        float delay = 0f,
        Ease ease = Ease.OutQuad)
    {
        if (prefab == null || getEndPosition == null)
        {
            onComplete?.Invoke();
            return;
        }

        GameObject visual = GetVisual(prefab, startPosition);

        Tween tween = CreateJumpTween(
            visual,
            startPosition,
            getEndPosition,
            jumpPower,
            duration,
            delay,
            ease
        );

        tween.OnComplete(() =>
        {
            ReturnVisual(prefab, visual);
            onComplete?.Invoke();
        });
    }

    /// <summary>
    /// 고정된 목표 위치로 포물선 이동합니다.
    /// UniTask 기반 버전입니다.
    /// </summary>
    public UniTask PlayJumpAsync(
        GameObject prefab,
        Vector3 startPosition,
        Vector3 endPosition,
        float jumpPower,
        float duration,
        CancellationToken cancellationToken,
        float delay = 0f,
        Ease ease = Ease.OutQuad)
    {
        return PlayJumpAsync(
            prefab,
            startPosition,
            () => endPosition,
            jumpPower,
            duration,
            cancellationToken,
            delay,
            ease
        );
    }

    /// <summary>
    /// 움직이는 목표 위치를 계속 추적하면서 포물선 이동합니다.
    /// UniTask 기반 버전입니다.
    /// </summary>
    public async UniTask PlayJumpAsync(
        GameObject prefab,
        Vector3 startPosition,
        Func<Vector3> getEndPosition,
        float jumpPower,
        float duration,
        CancellationToken cancellationToken,
        float delay = 0f,
        Ease ease = Ease.OutQuad)
    {
        if (prefab == null || getEndPosition == null)
            return;

        cancellationToken.ThrowIfCancellationRequested();

        GameObject visual = GetVisual(prefab, startPosition);

        bool returned = false;
        bool completed = false;

        void ReturnOnce()
        {
            if (returned)
                return;

            returned = true;
            ReturnVisual(prefab, visual);
        }

        // duration이 0 이하인 경우 DOTween 생성 없이 즉시 도착 처리합니다.
        if (duration <= 0f)
        {
            visual.transform.position = getEndPosition.Invoke();
            ReturnOnce();
            return;
        }

        UniTaskCompletionSource completionSource = new UniTaskCompletionSource();

        Tween tween = CreateJumpTween(
            visual,
            startPosition,
            getEndPosition,
            jumpPower,
            duration,
            delay,
            ease
        );

        tween.OnComplete(() =>
        {
            if (completed)
                return;

            completed = true;

            ReturnOnce();
            completionSource.TrySetResult();
        });

        using (cancellationToken.Register(() =>
        {
            if (completed)
                return;

            completed = true;

            if (tween != null && tween.IsActive())
                tween.Kill(false);

            ReturnOnce();
            completionSource.TrySetCanceled(cancellationToken);
        }))
        {
            await completionSource.Task;
        }
    }

    /// <summary>
    /// 0에서 targetValue까지 갔다가 다시 0으로 돌아오는 값을 반복 Tween합니다.
    /// 화살표가 목표 방향으로 살짝 움직였다가 돌아오는 연출에 사용합니다.
    /// </summary>
    public Tween PlayPingPongFloat(
        Action<float> onValueChanged,
        float targetValue,
        float duration,
        Ease ease = Ease.InOutSine)
    {
        if (onValueChanged == null)
            return null;

        if (duration <= 0f)
        {
            onValueChanged.Invoke(0f);
            return null;
        }

        float value = 0f;

        Tween tween = DOTween.To(
                () => value,
                changedValue =>
                {
                    value = changedValue;
                    onValueChanged.Invoke(value);
                },
                targetValue,
                duration
            )
            .SetEase(ease)
            .SetLoops(-1, LoopType.Yoyo);

        return tween;
    }

    /// <summary>
    /// 외부에서 보관 중인 Tween을 안전하게 종료합니다.
    /// </summary>
    public void KillTween(Tween tween, bool complete = false)
    {
        if (tween == null)
            return;

        if (tween.IsActive())
            tween.Kill(complete);
    }

    /// <summary>
    /// 포물선 이동 Tween을 생성합니다.
    /// 비주얼 오브젝트 풀 반환은 여기서 하지 않습니다.
    /// 콜백 버전/Async 버전에서 각각 완료 시점에 반환합니다.
    /// </summary>
    private Tween CreateJumpTween(
        GameObject visual,
        Vector3 startPosition,
        Func<Vector3> getEndPosition,
        float jumpPower,
        float duration,
        float delay,
        Ease ease)
    {
        float progress = 0f;

        Tween tween = DOTween.To(
                () => progress,
                value =>
                {
                    progress = value;

                    Vector3 currentEndPosition = getEndPosition.Invoke();

                    // 기본 위치 보간
                    Vector3 position = Vector3.Lerp(
                        startPosition,
                        currentEndPosition,
                        progress
                    );

                    // 포물선 높이
                    float arc = 4f * progress * (1f - progress);
                    position.y += arc * jumpPower;

                    visual.transform.position = position;
                },
                1f,
                duration
            )
            .SetDelay(delay)
            .SetEase(ease);

        return tween;
    }

    /// <summary>
    /// 풀에서 비주얼 오브젝트를 가져옵니다.
    /// 없으면 새로 생성합니다.
    /// </summary>
    private GameObject GetVisual(GameObject prefab, Vector3 startPosition)
    {
        if (!poolMap.TryGetValue(prefab, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            poolMap.Add(prefab, pool);
        }

        GameObject visual;

        if (pool.Count > 0)
        {
            visual = pool.Dequeue();
            visual.SetActive(true);
        }
        else
        {
            visual = Instantiate(prefab, effectRoot);
        }

        // 이전 Tween이 남아있을 수 있으므로 안전하게 제거합니다.
        visual.transform.DOKill();

        visual.transform.position = startPosition;
        visual.transform.rotation = Quaternion.identity;

        return visual;
    }

    /// <summary>
    /// 사용이 끝난 비주얼 오브젝트를 풀에 반환합니다.
    /// </summary>
    private void ReturnVisual(GameObject prefab, GameObject visual)
    {
        if (visual == null)
            return;

        visual.transform.DOKill();
        visual.SetActive(false);

        if (!poolMap.TryGetValue(prefab, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            poolMap.Add(prefab, pool);
        }

        pool.Enqueue(visual);
    }
}