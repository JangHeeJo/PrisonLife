using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 월드에 배치되는 NPC 공통 기능.
/// 이동, 상태 텍스트, 색상 변경 정도만 담당한다.
/// 수갑 개수 같은 처리 규칙은 NpcProcessArea가 관리한다.
/// </summary>
public class NpcUnit : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float rotateSpeed = 10f;
    [SerializeField] private float stopDistance = 0.05f;

    [Header("Visual")]
    [SerializeField] private Renderer[] renderers;

    [Header("Status Bubble")]
    [SerializeField] private GameObject statusBubbleRoot;
    [SerializeField] private TMP_Text statusText;

    [Header("Receive")]
    [SerializeField] private Vector3 receiveOffset = new Vector3(0f, 1f, 0f);

    [Header("Ground")]
    [SerializeField] private bool lockYPosition = true;
    [SerializeField] private float fixedYPosition;

    private Coroutine moveRoutine;
    private MaterialPropertyBlock propertyBlock;

    public Vector3 ReceivePosition => transform.position + receiveOffset;
    public bool IsMoving => moveRoutine != null;

    private void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();

        propertyBlock = new MaterialPropertyBlock();

        fixedYPosition = transform.position.y;

        HideStatusText();
    }

    private void OnDisable()
    {
        StopMove();
    }

    public void ResetUnit()
    {
        StopMove();
        HideStatusText();
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
        moveRoutine = StartCoroutine(MoveToRoutine(targetPosition, onArrived));
    }

    public void MoveAlongPath(Transform[] pathPoints, Action onArrived = null)
    {
        StopMove();

        if (pathPoints == null || pathPoints.Length == 0)
        {
            onArrived?.Invoke();
            return;
        }

        moveRoutine = StartCoroutine(MoveAlongPathRoutine(pathPoints, onArrived));
    }

    public void StopMove()
    {
        if (moveRoutine == null)
            return;

        StopCoroutine(moveRoutine);
        moveRoutine = null;
    }

    public void ShowStatusText(string text)
    {
        if (statusBubbleRoot != null)
            statusBubbleRoot.SetActive(true);

        if (statusText != null)
            statusText.text = text;
    }

    public void HideStatusText()
    {
        if (statusBubbleRoot != null)
            statusBubbleRoot.SetActive(false);
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

    private IEnumerator MoveAlongPathRoutine(Transform[] pathPoints, Action onArrived)
    {
        for (int i = 0; i < pathPoints.Length; i++)
        {
            Transform point = pathPoints[i];

            if (point == null)
                continue;

            yield return MoveStepRoutine(point.position);
        }

        moveRoutine = null;
        onArrived?.Invoke();
    }

    private IEnumerator MoveToRoutine(Vector3 targetPosition, Action onArrived)
    {
        yield return MoveStepRoutine(targetPosition);

        moveRoutine = null;
        onArrived?.Invoke();
    }

    private IEnumerator MoveStepRoutine(Vector3 targetPosition)
    {
        if (lockYPosition)
            targetPosition.y = fixedYPosition;

        float stopDistanceSqr = stopDistance * stopDistance;

        while ((transform.position - targetPosition).sqrMagnitude > stopDistanceSqr)
        {
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

            yield return null;
        }

        transform.position = targetPosition;
    }
}