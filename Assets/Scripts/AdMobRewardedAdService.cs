using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GoogleMobileAds.Api;
using UnityEngine;

/// <summary>
/// Google Mobile Ads 보상형 광고를 게임 로직에서 안전하게 사용할 수 있게 감싼 서비스입니다.
///
/// 역할:
/// - Google Mobile Ads SDK 초기화
/// - 보상형 광고 로드/표시
/// - 광고 시청 완료 여부를 UniTask로 반환
/// - 광고 종료 후 다음 광고 자동 로드
/// - 릴리즈 빌드에서 Google 테스트 광고 ID 요청 차단
///
/// 주의:
/// - Google Mobile Ads Settings에는 App ID를 넣어야 합니다.
/// - 이 스크립트에는 Rewarded Ad Unit ID를 넣어야 합니다.
/// </summary>
public sealed class AdMobRewardedAdService : MonoBehaviour
{
    private const string AndroidRewardedTestAdUnitId = "ca-app-pub-3940256099942544/5224354917";
    private const string IosRewardedTestAdUnitId = "ca-app-pub-3940256099942544/1712485313";

    [Header("Ad Unit Id")]
    [Tooltip("Android 보상형 광고 테스트 ID입니다. 출시 전 실제 ID로 교체하세요.")]
    [SerializeField] private string androidRewardedAdUnitId = AndroidRewardedTestAdUnitId;

    [Tooltip("iOS 보상형 광고 테스트 ID입니다.")]
    [SerializeField] private string iosRewardedAdUnitId = IosRewardedTestAdUnitId;

    [Header("Option")]
    [SerializeField] private bool loadOnStart = true;
    [Tooltip("릴리즈 빌드에서 Google 테스트 광고 ID 사용을 예외적으로 허용합니다. 출시 빌드에서는 false로 유지하세요.")]
    [SerializeField] private bool allowTestAdUnitIdInReleaseBuild;

    [Header("Debug")]
    [SerializeField] private bool logState = true;

    private RewardedAd rewardedAd;
    private CancellationTokenSource destroyCts;

    private bool isInitialized;
    private bool isLoading;
    private bool isShowing;
    private bool isDestroyed;

    public bool IsAdReady => rewardedAd != null && rewardedAd.CanShowAd();

    public event Action AdLoaded;
    public event Action AdLoadFailed;
    public event Action AdClosed;
    public event Action UserRewarded;

    private string RewardedAdUnitId
    {
        get
        {
            if (Application.platform == RuntimePlatform.IPhonePlayer)
                return iosRewardedAdUnitId;

            return androidRewardedAdUnitId;
        }
    }

    private void Awake()
    {
        destroyCts = new CancellationTokenSource();
    }

    private void Start()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        isDestroyed = true;

        destroyCts?.Cancel();
        destroyCts?.Dispose();
        destroyCts = null;

        DestroyAd();
    }

    public void Initialize()
    {
        if (isInitialized)
            return;

        MobileAds.Initialize(_ =>
        {
            isInitialized = true;

            if (logState)
                Debug.Log("[AdMobRewardedAdService] Mobile Ads initialized.", this);

            if (loadOnStart)
                LoadAd();
        });
    }

    public void LoadAd()
    {
        if (isDestroyed)
            return;

        if (!isInitialized)
        {
            Initialize();
            return;
        }

        if (isLoading)
            return;

        if (IsAdReady)
            return;

        if (!CanRequestAds())
            return;

        DestroyAd();

        isLoading = true;

        if (logState)
            Debug.Log("[AdMobRewardedAdService] Loading rewarded ad...", this);

        AdRequest request = new AdRequest();

        RewardedAd.Load(RewardedAdUnitId, request, (RewardedAd ad, LoadAdError error) =>
        {
            isLoading = false;

            if (error != null || ad == null)
            {
                if (logState)
                    Debug.LogWarning($"[AdMobRewardedAdService] Failed to load rewarded ad. Error: {error}", this);

                AdLoadFailed?.Invoke();
                return;
            }

            rewardedAd = ad;

            if (logState)
                Debug.Log("[AdMobRewardedAdService] Rewarded ad loaded.", this);

            AdLoaded?.Invoke();
        });
    }

    /// <summary>
    /// 기존 코드 호환용 콜백 방식입니다.
    /// 내부적으로는 UniTask 광고 표시 흐름을 사용합니다.
    /// </summary>
    public bool ShowAd(Action onRewarded)
    {
        if (!IsAdReady)
        {
            if (logState)
                Debug.LogWarning("[AdMobRewardedAdService] Rewarded ad is not ready.", this);

            LoadAd();
            return false;
        }

        ShowAdInternalAsync(onRewarded).Forget();
        return true;
    }

    private async UniTaskVoid ShowAdInternalAsync(Action onRewarded)
    {
        bool rewarded = await ShowRewardedAsync(destroyCts.Token);

        if (rewarded)
            onRewarded?.Invoke();
    }

    /// <summary>
    /// 보상형 광고를 표시하고, 유저가 보상을 받았는지 반환합니다.
    /// 광고가 닫힌 뒤 결과를 반환합니다.
    /// </summary>
    public async UniTask<bool> ShowRewardedAsync(CancellationToken cancellationToken)
    {
        if (isShowing)
            return false;

        if (!IsAdReady)
        {
            LoadAd();
            return false;
        }

        isShowing = true;

        RewardedAd showingAd = rewardedAd;
        bool rewarded = false;
        bool completed = false;

        UniTaskCompletionSource<bool> completionSource = new UniTaskCompletionSource<bool>();

        void Complete(bool result)
        {
            if (completed)
                return;

            completed = true;
            completionSource.TrySetResult(result);
        }

        void OnClosed()
        {
            if (logState)
                Debug.Log("[AdMobRewardedAdService] Rewarded ad closed.", this);

            AdClosed?.Invoke();
            Complete(rewarded);
        }

        void OnFailedToShow(AdError error)
        {
            if (logState)
                Debug.LogWarning($"[AdMobRewardedAdService] Rewarded ad failed to show. Error: {error}", this);

            AdClosed?.Invoke();
            Complete(false);
        }

        showingAd.OnAdFullScreenContentClosed += OnClosed;
        showingAd.OnAdFullScreenContentFailed += OnFailedToShow;

        try
        {
            showingAd.Show(reward =>
            {
                rewarded = true;

                if (logState)
                {
                    Debug.Log(
                        $"[AdMobRewardedAdService] User earned reward. " +
                        $"Type: {reward.Type}, Amount: {reward.Amount}",
                        this
                    );
                }

                UserRewarded?.Invoke();
            });

            bool result = await completionSource.Task.AttachExternalCancellation(cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception e)
        {
            if (logState)
                Debug.LogException(e, this);

            return false;
        }
        finally
        {
            showingAd.OnAdFullScreenContentClosed -= OnClosed;
            showingAd.OnAdFullScreenContentFailed -= OnFailedToShow;

            isShowing = false;

            DestroyAd();

            if (!isDestroyed)
                LoadAd();
        }
    }

    private void DestroyAd()
    {
        if (rewardedAd == null)
            return;

        rewardedAd.Destroy();
        rewardedAd = null;
    }

    /// <summary>
    /// 광고 요청 직전에 호출되는 릴리즈 안전장치입니다.
    /// 실제 출시 빌드에서 Google 샘플 광고 ID로 트래픽을 보내는 실수를 방지합니다.
    /// </summary>
    private bool CanRequestAds()
    {
        string adUnitId = RewardedAdUnitId;

        if (string.IsNullOrWhiteSpace(adUnitId))
        {
            if (logState)
                Debug.LogWarning("[AdMobRewardedAdService] Rewarded ad unit id is empty.", this);

            return false;
        }

        if (Debug.isDebugBuild || Application.isEditor)
            return true;

        if (allowTestAdUnitIdInReleaseBuild)
            return true;

        if (!IsGoogleRewardedTestAdUnitId(adUnitId))
            return true;

        if (logState)
        {
            Debug.LogWarning(
                "[AdMobRewardedAdService] Google test ad unit id was blocked in a release build.",
                this
            );
        }

        return false;
    }

    private static bool IsGoogleRewardedTestAdUnitId(string adUnitId)
    {
        return adUnitId == AndroidRewardedTestAdUnitId ||
               adUnitId == IosRewardedTestAdUnitId;
    }
}