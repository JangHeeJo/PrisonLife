# Release AdMob Checklist

Before uploading a production or open-test build, replace every Google sample AdMob ID with the real IDs from the app's AdMob account.

## Required IDs

- Android App ID
  - Unity path: `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`
  - Field: `adMobAndroidAppId`
  - Format example: `ca-app-pub-xxxxxxxxxxxxxxxx~yyyyyyyyyy`

- Android Rewarded Ad Unit ID
  - Unity scene: `Assets/Scenes/SampleScene.unity`
  - Object: `AdMobRewardedAdService`
  - Field: `androidRewardedAdUnitId`
  - Format example: `ca-app-pub-xxxxxxxxxxxxxxxx/yyyyyyyyyy`

## Current Safety Behavior

- `AdMobRewardedAdService` blocks Google sample rewarded ad unit IDs in non-development release builds.
- This prevents accidental production traffic from using Google's sample ad unit.
- Ads will not load in a release build until the real AdMob rewarded ad unit ID is configured.

## Current Sample IDs To Replace

- `ca-app-pub-3940256099942544~3347511713`
- `ca-app-pub-3940256099942544/5224354917`
- `ca-app-pub-3940256099942544/1712485313`

## Verification

1. Set the real Android App ID in Google Mobile Ads Settings.
2. Set the real Android rewarded ad unit ID on `AdMobRewardedAdService`.
3. Build a non-development Android App Bundle.
4. Install through Google Play internal testing.
5. Earn gold, wait for the ad popup, and confirm the rewarded ad loads.
6. Watch the rewarded ad and confirm:
   - extra gold is granted,
   - the 1.5x timed gold boost is active,
   - the boost persists after app restart.
