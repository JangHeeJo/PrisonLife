using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 클리어 후 현재 스테이지를 다시 시작하는 컨트롤러입니다.
/// 
/// 역할:
/// - 클리어 UI 표시
/// - 잠깐 대기
/// - 현재 씬 다시 로드
/// 
/// 씬을 다시 로드하면 플레이어 위치, 자원, NPC, 감옥 상태, Unlock 상태가
/// 씬 초기 배치 상태로 돌아갑니다.
/// </summary>
public sealed class GameClearController : MonoBehaviour
{
    [Header("Clear UI")]
    [SerializeField] private GameObject clearPanelRoot;
    [SerializeField] private CanvasGroup clearCanvasGroup;
    [SerializeField] private TMP_Text clearText;

    [Header("Text")]
    [SerializeField] private string clearMessage = "STAGE CLEAR";

    [Header("Timing")]
    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private float holdSeconds = 1.5f;

    [Header("Reload")]
    [Tooltip("true면 현재 씬을 다시 로드합니다.")]
    [SerializeField] private bool reloadCurrentScene = true;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private bool isClearing;
    private CancellationTokenSource clearCts;

    private void Awake()
    {
        HideClearUIImmediate();
    }

    private void OnDisable()
    {
        CancelClear();
    }

    private void OnDestroy()
    {
        CancelClear();
    }

    /// <summary>
    /// 외부에서 게임 클리어를 요청할 때 호출합니다.
    /// 예: 감옥 확장 완료 후 호출.
    /// </summary>
    public void PlayClearAndRestart()
    {
        if (isClearing)
            return;

        clearCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy()
        );

        PlayClearAndRestartAsync(clearCts.Token).Forget();
    }

    private async UniTaskVoid PlayClearAndRestartAsync(CancellationToken token)
    {
        isClearing = true;

        try
        {
            if (logState)
                Debug.Log("[GameClearController] Stage clear started.", this);

            await ShowClearUIAsync(token);

            if (holdSeconds > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(holdSeconds),
                    cancellationToken: token
                );
            }

            ReloadStage();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async UniTask ShowClearUIAsync(CancellationToken token)
    {
        if (clearText != null)
            clearText.text = clearMessage;

        if (clearPanelRoot != null)
            clearPanelRoot.SetActive(true);

        if (clearCanvasGroup == null)
            return;

        clearCanvasGroup.alpha = 0f;
        clearCanvasGroup.interactable = false;
        clearCanvasGroup.blocksRaycasts = true;

        if (fadeInDuration <= 0f)
        {
            clearCanvasGroup.alpha = 1f;
            return;
        }

        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            clearCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        clearCanvasGroup.alpha = 1f;
    }

    private void ReloadStage()
    {
        if (!reloadCurrentScene)
            return;

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.ResetProgressAndReloadCurrentScene();
            return;
        }

        Time.timeScale = 1f;

        int activeSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(activeSceneIndex);
    }

    private void HideClearUIImmediate()
    {
        if (clearCanvasGroup != null)
        {
            clearCanvasGroup.alpha = 0f;
            clearCanvasGroup.interactable = false;
            clearCanvasGroup.blocksRaycasts = false;
        }

        if (clearPanelRoot != null)
            clearPanelRoot.SetActive(false);
    }

    private void CancelClear()
    {
        if (clearCts == null)
            return;

        clearCts.Cancel();
        clearCts.Dispose();
        clearCts = null;

        isClearing = false;
    }
}