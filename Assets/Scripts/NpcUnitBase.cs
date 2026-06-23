using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class NpcUnitBase : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float rotateSpeed = 10f;
    [SerializeField] private float stopDistance = 0.05f;
    [SerializeField] private bool lockYPosition = true;

    [Header("Visual")]
    [SerializeField] private Renderer[] renderers;

    private CancellationTokenSource moveCts;
    private MaterialPropertyBlock propertyBlock;
    private float fixedYPosition;

    public bool IsMoving => moveCts != null;

    protected virtual void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();

        propertyBlock = new MaterialPropertyBlock();
        fixedYPosition = transform.position.y;
    }

    protected virtual void OnDisable()
    {
        StopMove();
    }

    protected virtual void OnDestroy()
    {
        StopMove();
    }

    public virtual void ResetUnit()
    {
        StopMove();
        fixedYPosition = transform.position.y;
    }

    public void MoveTo(Transform target, Action onArrived = null)
    {
        if (target == null)
        {
            onArrived?.Invoke();
            return;
        }

        MoveTo(target.position, onArrived);
    }

    public void MoveTo(Vector3 targetPosition, Action onArrived = null)
    {
        StopMove();

        moveCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy()
        );

        MoveToAsync(
            targetPosition,
            onArrived,
            moveCts,
            moveCts.Token
        ).Forget();
    }

    public void MoveAlongPath(Transform[] pathPoints, Action onArrived = null)
    {
        StopMove();

        if (pathPoints == null || pathPoints.Length == 0)
        {
            onArrived?.Invoke();
            return;
        }

        moveCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy()
        );

        MoveAlongPathAsync(
            pathPoints,
            onArrived,
            moveCts,
            moveCts.Token
        ).Forget();
    }

    public void StopMove()
    {
        if (moveCts == null)
            return;

        // Dispose는 async 루프의 finally에서 처리합니다.
        // 여기서 바로 Dispose하면 실행 중인 UniTask가 ObjectDisposedException을 낼 수 있습니다.
        moveCts.Cancel();
        moveCts = null;
    }

    public void SetColor(Color color)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];

            if (targetRenderer == null)
                continue;

            targetRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);

            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private async UniTaskVoid MoveToAsync(
        Vector3 targetPosition,
        Action onArrived,
        CancellationTokenSource localCts,
        CancellationToken token)
    {
        bool arrived = false;

        try
        {
            await MoveStepAsync(targetPosition, token);
            arrived = true;
        }
        catch (OperationCanceledException)
        {
            arrived = false;
        }
        finally
        {
            CleanupMoveToken(localCts);

            if (arrived)
                onArrived?.Invoke();
        }
    }

    private async UniTaskVoid MoveAlongPathAsync(
        Transform[] pathPoints,
        Action onArrived,
        CancellationTokenSource localCts,
        CancellationToken token)
    {
        bool arrived = false;

        try
        {
            for (int i = 0; i < pathPoints.Length; i++)
            {
                token.ThrowIfCancellationRequested();

                Transform point = pathPoints[i];

                if (point == null)
                    continue;

                await MoveStepAsync(point.position, token);
            }

            arrived = true;
        }
        catch (OperationCanceledException)
        {
            arrived = false;
        }
        finally
        {
            CleanupMoveToken(localCts);

            if (arrived)
                onArrived?.Invoke();
        }
    }

    private async UniTask MoveStepAsync(Vector3 targetPosition, CancellationToken token)
    {
        if (lockYPosition)
            targetPosition.y = fixedYPosition;

        float stopDistanceSqr = stopDistance * stopDistance;

        while ((transform.position - targetPosition).sqrMagnitude > stopDistanceSqr)
        {
            token.ThrowIfCancellationRequested();

            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotateSpeed * Time.deltaTime
                );
            }

            Vector3 nextPosition = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            if (lockYPosition)
                nextPosition.y = fixedYPosition;

            transform.position = nextPosition;

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        transform.position = targetPosition;
    }

    private void CleanupMoveToken(CancellationTokenSource localCts)
    {
        if (ReferenceEquals(moveCts, localCts))
            moveCts = null;

        localCts?.Dispose();
    }
}