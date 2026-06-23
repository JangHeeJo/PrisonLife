using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// MAX 텍스트를 위로 이동시키며 페이드 아웃한다.
/// 텍스트 오브젝트만 켰다 끄고, 이 스크립트 오브젝트는 항상 Active 상태로 둔다.
/// </summary>
public class MaxTextFeedback : MonoBehaviour
{
    [SerializeField] private TMP_Text maxText;

    [Header("Animation")]
    [SerializeField] private float showDuration = 0.7f;
    [SerializeField] private float moveUpDistance = 35f;

    [Header("Cooldown")]
    [SerializeField] private float cooldown = 2f;

    private RectTransform rectTransform;
    private Vector2 startPosition;
    private Color startColor;

    private bool isCooldown;
    private Coroutine routine;

    private void Awake()
    {
        if (maxText == null)
            maxText = GetComponentInChildren<TMP_Text>(true);

        if (maxText == null)
            return;

        rectTransform = maxText.rectTransform;
        startPosition = rectTransform.anchoredPosition;
        startColor = maxText.color;

        maxText.gameObject.SetActive(false);
    }

    public void Show()
    {
        if (isCooldown || maxText == null)
            return;

        if (routine != null)
            StopCoroutine(routine);

        // 코루틴 시작 전에 텍스트 오브젝트만 활성화
        maxText.gameObject.SetActive(true);

        routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        isCooldown = true;

        ResetText();

        float elapsed = 0f;

        while (elapsed < showDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / showDuration);

            rectTransform.anchoredPosition = Vector2.Lerp(
                startPosition,
                startPosition + Vector2.up * moveUpDistance,
                t
            );

            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t);
            maxText.color = color;

            yield return null;
        }

        maxText.gameObject.SetActive(false);

        float remainCooldown = Mathf.Max(0f, cooldown - showDuration);
        yield return new WaitForSeconds(remainCooldown);

        isCooldown = false;
        routine = null;
    }

    private void ResetText()
    {
        rectTransform.anchoredPosition = startPosition;
        maxText.color = startColor;
    }
}