using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 골드 2배 광고 선택 팝업 컨트롤러입니다.
///
/// UI 흐름:
/// - Money 획득 후 기본 골드는 즉시 지급됩니다.
/// - 최근 획득 골드가 있으면 팝업을 표시합니다.
/// - agree: 광고 시청 후 최근 획득 골드만큼 추가 지급합니다.
/// - no: 추가 보상을 포기하고 팝업을 닫습니다.
///
/// 주의:
/// - 이 스크립트는 꺼지는 PopupRoot에 붙이지 마세요.
/// - 항상 켜져 있는 Canvas, UIManager, Managers 오브젝트에 붙이고,
///   PopupRoot만 SetActive로 켜고 끄는 구조로 사용하세요.
/// </summary>
public sealed class RewardedGoldAdController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AdMobRewardedAdService adService;
    [SerializeField] private GoldHudView goldHudView;

    [Header("Popup UI")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;

    [Header("Buttons")]
    [SerializeField] private Button agreeButton;
    [SerializeField] private TMP_Text agreeButtonText;

    [SerializeField] private Button noButton;
    [SerializeField] private TMP_Text noButtonText;

    [Header("Text")]
    [SerializeField] private string titleReadyText = "Watch Ad Gold x 2";
    [SerializeField] private string titleLoadingText = "Loading Ad...";
    [SerializeField] private string agreeText = "agree";
    [SerializeField] private string noText = "no";

    [Header("Timing")]
    [Tooltip("돈 획득 후 팝업이 표시되기까지의 지연 시간입니다.")]
    [SerializeField] private float showDelaySeconds = 0.5f;

    [Tooltip("팝업 페이드 시간입니다.")]
    [SerializeField] private float fadeDuration = 0.15f;

    [Header("Option")]
    [Tooltip("광고가 준비되지 않았을 때 자동으로 광고 로드를 재시도합니다.")]
    [SerializeField] private bool loadAdWhenNotReady = true;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private CancellationTokenSource lifeCts;
    private CancellationTokenSource showCts;
    private CancellationTokenSource animationCts;

    private bool isShowing;
    private bool isProcessing;

    private void Awake()
    {
        if (popupRoot == null && canvasGroup != null)
            popupRoot = canvasGroup.gameObject;

        if (agreeButton != null)
            agreeButton.onClick.AddListener(OnClickAgree);

        if (noButton != null)
            noButton.onClick.AddListener(OnClickNo);

        HideImmediate();
        RefreshUI();
    }

    private void OnEnable()
    {
        lifeCts = new CancellationTokenSource();

        if (goldHudView != null)
            goldHudView.RewardableGoldChanged += OnRewardableGoldChanged;

        if (adService != null)
        {
            adService.AdLoaded += RefreshUI;
            adService.AdLoadFailed += RefreshUI;
            adService.AdClosed += RefreshUI;

            if (!adService.IsAdReady && loadAdWhenNotReady)
                adService.LoadAd();
        }

        RefreshUI();
    }

    private void OnDisable()
    {
        CancelAndDispose(ref showCts);
        CancelAndDispose(ref animationCts);
        CancelAndDispose(ref lifeCts);

        if (goldHudView != null)
            goldHudView.RewardableGoldChanged -= OnRewardableGoldChanged;

        if (adService != null)
        {
            adService.AdLoaded -= RefreshUI;
            adService.AdLoadFailed -= RefreshUI;
            adService.AdClosed -= RefreshUI;
        }
    }

    private void OnDestroy()
    {
        if (agreeButton != null)
            agreeButton.onClick.RemoveListener(OnClickAgree);

        if (noButton != null)
            noButton.onClick.RemoveListener(OnClickNo);
    }

    private void OnRewardableGoldChanged(int amount)
    {
        if (logState)
            Debug.Log($"[RewardedGoldAdController] RewardableGoldChanged: {amount}", this);

        if (amount > 0)
        {
            if (IapOfferPopupController.ShouldPrioritizeGoldBoostOffer())
                return;

            ShowDelayedAsync().Forget();
        }
        else
        {
            HideAsync().Forget();
        }

        RefreshUI();
    }

    private async UniTaskVoid ShowDelayedAsync()
    {
        CancelAndDispose(ref showCts);

        if (lifeCts == null)
            return;

        showCts = CancellationTokenSource.CreateLinkedTokenSource(lifeCts.Token);
        CancellationToken token = showCts.Token;

        try
        {
            if (showDelaySeconds > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(showDelaySeconds), cancellationToken: token);

            if (goldHudView == null || !goldHudView.HasRewardableGold)
                return;

            if (IapOfferPopupController.HasPendingOrVisibleOffer ||
                IapOfferPopupController.ShouldPrioritizeGoldBoostOffer())
            {
                if (logState)
                    Debug.Log("[RewardedGoldAdController] Gold boost offer has priority; rewarded ad popup skipped.", this);

                return;
            }

            if (adService != null && !adService.IsAdReady && loadAdWhenNotReady)
                adService.LoadAd();

            await ShowAsync(token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async UniTask ShowAsync(CancellationToken token)
    {
        if (isShowing)
        {
            RefreshUI();
            return;
        }

        isShowing = true;

        CancelAndDispose(ref animationCts);
        animationCts = CancellationTokenSource.CreateLinkedTokenSource(token);

        if (popupRoot != null)
            popupRoot.SetActive(true);

        if (canvasGroup == null)
        {
            RefreshUI();
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);

            await UniTask.Yield(PlayerLoopTiming.Update, animationCts.Token);
        }

        canvasGroup.alpha = 1f;
        RefreshUI();
    }

    private async UniTask HideAsync()
    {
        CancelAndDispose(ref showCts);

        if (!isShowing)
        {
            HideImmediate();
            return;
        }

        isShowing = false;

        CancelAndDispose(ref animationCts);
        animationCts = lifeCts != null
            ? CancellationTokenSource.CreateLinkedTokenSource(lifeCts.Token)
            : new CancellationTokenSource();

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            try
            {
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / fadeDuration));

                    await UniTask.Yield(PlayerLoopTiming.Update, animationCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }

            canvasGroup.alpha = 0f;
        }

        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    private void HideImmediate()
    {
        isShowing = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    private void OnClickAgree()
    {
        WatchAdAsync().Forget();
    }
    public void Click()
    {
        Debug.Log("클릭 되었음");
    }
    private async UniTaskVoid WatchAdAsync()
    {
        if (isProcessing)
            return;

        if (adService == null || goldHudView == null)
            return;

        if (!goldHudView.HasRewardableGold)
        {
            await HideAsync();
            return;
        }

        if (!adService.IsAdReady)
        {
            if (loadAdWhenNotReady)
                adService.LoadAd();

            RefreshUI();
            return;
        }

        isProcessing = true;
        RefreshUI();

        try
        {
            // 광고가 뜨기 전에 선택 팝업을 먼저 닫습니다.
            // 주의: 여기서는 ClearRewardableGold를 호출하지 않습니다.
            // 광고 성공 시 추가 보상을 줘야 하기 때문입니다.
            await HideAsync();

            bool rewarded = await adService.ShowRewardedAsync(lifeCts.Token);

            if (rewarded)
            {
                bool claimed = goldHudView.TryClaimDoubleGoldReward();

                if (logState)
                    Debug.Log($"[RewardedGoldAdController] Reward claimed: {claimed}", this);
            }
            else
            {
                if (logState)
                    Debug.Log("[RewardedGoldAdController] Ad was closed without reward.", this);

                // 광고를 끝까지 안 봤거나 보상 실패한 경우,
                // 아직 보상 선택권이 남아 있으므로 다시 팝업을 보여줍니다.
                if (goldHudView.HasRewardableGold && lifeCts != null)
                    await ShowAsync(lifeCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isProcessing = false;
            RefreshUI();

            // 보상 성공 시 TryClaimDoubleGoldReward에서 lastEarnedGold가 0이 되므로
            // 팝업은 닫힌 상태로 유지됩니다.
            if (goldHudView == null || !goldHudView.HasRewardableGold)
                await HideAsync();
        }
    }

    private void OnClickNo()
    {
        SkipAsync().Forget();
    }

    private async UniTaskVoid SkipAsync()
    {
        if (isProcessing)
            return;

        if (goldHudView != null)
            goldHudView.ClearRewardableGold();

        await HideAsync();

        if (logState)
            Debug.Log("[RewardedGoldAdController] Reward skipped.", this);
    }

    private void RefreshUI()
    {
        bool hasReward = goldHudView != null && goldHudView.HasRewardableGold;
        bool adReady = adService != null && adService.IsAdReady;

        if (titleText != null)
            titleText.text = adReady ? titleReadyText : titleLoadingText;

        if (agreeButtonText != null)
            agreeButtonText.text = agreeText;

        if (noButtonText != null)
            noButtonText.text = noText;

        if (agreeButton != null)
            agreeButton.interactable = hasReward && adReady && !isProcessing;

        if (noButton != null)
            noButton.interactable = hasReward && !isProcessing;
    }

    private static void CancelAndDispose(ref CancellationTokenSource cts)
    {
        if (cts == null)
            return;

        cts.Cancel();
        cts.Dispose();
        cts = null;
    }
}