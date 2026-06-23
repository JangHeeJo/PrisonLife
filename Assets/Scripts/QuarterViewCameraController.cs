using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class QuarterViewCameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform followTarget;

    [Header("View")]
    [SerializeField] private bool useOrthographic = true;
    [SerializeField] private float orthographicSize = 6.2f;

    [Tooltip("쿼터뷰 카메라 각도")]
    [SerializeField] private Vector3 cameraEuler = new Vector3(55f, 45f, 0f);

    [Tooltip("플레이어 기준 카메라 위치 오프셋")]
    [SerializeField] private Vector3 followOffset = new Vector3(-6f, 8f, -6f);

    [Header("Follow")]
    [SerializeField] private float followSmoothTime = 0.12f;
    [SerializeField] private float maxFollowSpeed = 100f;

    [Header("Focus")]
    [SerializeField] private float focusDuration = 0.6f;
    [SerializeField] private float focusOrthographicSize = 5.8f;

    private Camera cam;
    private Vector3 followVelocity;
    private Coroutine focusRoutine;
    private bool isFocusing;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        cam.orthographic = useOrthographic;

        if (useOrthographic)
            cam.orthographicSize = orthographicSize;

        transform.rotation = Quaternion.Euler(cameraEuler);
    }

    private void LateUpdate()
    {
        if (isFocusing || followTarget == null)
            return;

        FollowTarget();
    }

    private void FollowTarget()
    {
        Vector3 targetPosition = followTarget.position + followOffset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref followVelocity,
            followSmoothTime,
            maxFollowSpeed,
            Time.deltaTime
        );

        transform.rotation = Quaternion.Euler(cameraEuler);

        if (useOrthographic)
        {
            cam.orthographicSize = Mathf.Lerp(
                cam.orthographicSize,
                orthographicSize,
                Time.deltaTime * 8f
            );
        }
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;

        if (target == null)
            return;

        transform.position = target.position + followOffset;
        transform.rotation = Quaternion.Euler(cameraEuler);
    }

    public void FocusOnTarget(
        Transform target,
        float? customSize = null,
        float? customDuration = null,
        Action onComplete = null)
    {
        if (target == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (focusRoutine != null)
            StopCoroutine(focusRoutine);

        float size = customSize ?? focusOrthographicSize;
        float duration = customDuration ?? focusDuration;

        focusRoutine = StartCoroutine(FocusRoutine(target.position, size, duration, onComplete));
    }

    public void ReturnToPlayer(float? customDuration = null, Action onComplete = null)
    {
        if (followTarget == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (focusRoutine != null)
            StopCoroutine(focusRoutine);

        float duration = customDuration ?? focusDuration;
        Vector3 targetPosition = followTarget.position + followOffset;

        focusRoutine = StartCoroutine(FocusRoutine(targetPosition - followOffset, orthographicSize, duration, () =>
        {
            isFocusing = false;
            onComplete?.Invoke();
        }));
    }

    private IEnumerator FocusRoutine(
        Vector3 worldTargetPosition,
        float targetSize,
        float duration,
        Action onComplete)
    {
        isFocusing = true;

        Vector3 startPosition = transform.position;
        Vector3 endPosition = worldTargetPosition + followOffset;

        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = Quaternion.Euler(cameraEuler);

        float startSize = cam.orthographicSize;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(startPosition, endPosition, smoothT);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, smoothT);

            if (useOrthographic)
                cam.orthographicSize = Mathf.Lerp(startSize, targetSize, smoothT);

            yield return null;
        }

        transform.position = endPosition;
        transform.rotation = endRotation;

        if (useOrthographic)
            cam.orthographicSize = targetSize;

        focusRoutine = null;
        onComplete?.Invoke();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (followSmoothTime < 0.01f)
            followSmoothTime = 0.01f;

        if (orthographicSize < 1f)
            orthographicSize = 1f;

        if (focusOrthographicSize < 1f)
            focusOrthographicSize = 1f;
    }
#endif
}