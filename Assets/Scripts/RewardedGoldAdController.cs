using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the rewarded-ad popup shown after the player earns gold.
/// Watching the ad grants the regular double-gold reward and a timed gold boost.
/// </summary>
public sealed class RewardedGoldAdController : MonoBehaviour
{
    private static RewardedGoldAdController instance;

    public static bool HasActivePrompt =>
        instance != null && (instance.hasPendingShow || instance.isShowing || instance.isProcessing);

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
    [Tooltip("Delay before showing the popup after gold is earned.")]
    [SerializeField] private float showDelaySeconds = 0.5f;

    [Tooltip("Fade duration for opening and closing the popup.")]
    [SerializeField] private float fadeDuration = 0.15f;

    [Header("Option")]
    [Tooltip("Automatically request a new ad when the popup opens and no ad is ready.")]
    [SerializeField] private bool loadAdWhenNotReady = true;
    [SerializeField, Min(0.1f)] private float adLoadWaitSeconds = 6f;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private CancellationTokenSource lifeCts;
    private CancellationTokenSource showCts;
    private CancellationTokenSource animationCts;

    private bool hasPendingShow;
    private bool hasShownPromptThisSession;
    private bool isShowing;
    private bool isProcessing;

    private void Awake()
    {
        instance = this;
        IapOfferPopupController.GetOrCreate();

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
        if (instance == this)
            instance = null;
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
            if (hasShownPromptThisSession || HasActiveAdBoost())
            {
                hasPendingShow = false;
                goldHudView?.ClearRewardableGold();
                HideAsync().Forget();
                return;
            }

            hasShownPromptThisSession = true;

            // Money pickup creates a reward opportunity, then both monetization prompts can react.
            hasPendingShow = true;
            IapOfferPopupController.GetOrCreate().ShowGoldBoostOfferNowForMoneyPickup();
            ShowDelayedAsync().Forget();
        }
        else
        {
            hasPendingShow = false;
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

            if (goldHudView == null || !goldHudView.HasRewardableGold || HasActiveAdBoost())
                return;

            if (adService != null && !adService.IsAdReady && loadAdWhenNotReady)
                adService.LoadAd();

            hasPendingShow = false;
            await ShowAsync(token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async UniTask ShowAsync(CancellationToken token)
    {
        hasPendingShow = false;
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
        hasPendingShow = false;
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
        HideAsync().Forget();
        WatchAdAsync().Forget();
    }

    public void Click()
    {
        // Legacy scene event hook. Runtime wiring uses OnClickAgree.
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
            adService.LoadAd();

            if (!await WaitUntilAdReadyAsync(lifeCts.Token))
            {
                goldHudView.ClearRewardableGold();
                await HideAsync();
                return;
            }
        }

        isProcessing = true;
        RefreshUI();

        try
        {
            await HideAsync();

            bool rewarded = await adService.ShowRewardedAsync(lifeCts.Token);

            if (rewarded)
            {
                // A completed rewarded ad grants one extra copy of the latest gold reward.
                bool claimed = goldHudView.TryClaimDoubleGoldReward();
                // The same ad also extends the timed 1.5x gold boost policy.
                GoldMultiplierProvider.Instance?.GrantAdBoostForOneDay();

                if (logState)
                    Debug.Log($"[RewardedGoldAdController] Reward claimed: {claimed}", this);
            }
            else
            {
                if (logState)
                    Debug.Log("[RewardedGoldAdController] Ad was closed without reward.", this);

                goldHudView.ClearRewardableGold();
                await HideAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isProcessing = false;
            RefreshUI();

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
            agreeButton.interactable = hasReward && !isProcessing;

        if (noButton != null)
            noButton.interactable = hasReward && !isProcessing;
    }

    private async UniTask<bool> WaitUntilAdReadyAsync(CancellationToken token)
    {
        float elapsed = 0f;

        while (elapsed < adLoadWaitSeconds)
        {
            if (adService != null && adService.IsAdReady)
                return true;

            await UniTask.Yield(PlayerLoopTiming.Update, token);
            elapsed += Time.unscaledDeltaTime;
        }

        return adService != null && adService.IsAdReady;
    }

    private static bool HasActiveAdBoost()
    {
        GoldMultiplierProvider provider = GoldMultiplierProvider.Instance;
        return provider != null && provider.IsAdBoostActive;
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
