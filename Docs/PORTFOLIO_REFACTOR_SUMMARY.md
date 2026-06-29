# PrisonLife Portfolio Architecture Summary

## Positioning

`PrisonLife` is presented as a Unity mobile idle game prepared for Google Play internal testing, with monetization, persistence, data-driven progression, and release QA structure in place.

The portfolio angle is not just "implemented features"; it is "owned release-ready gameplay systems with clear runtime boundaries."

## Showcase Code

### Monetization Boundary

- `Assets/Scripts/IAP/UnityIapPurchaseService.cs`
  - Owns Unity IAP initialization and Play Billing product registration.
  - Queues purchase requests made before store initialization completes.
  - Validates product availability before opening the native purchase sheet.
  - Forwards completed purchases to the reward execution layer.

- `Assets/Scripts/IAP/IapRewardExecutor.cs`
  - Converts purchased product IDs into gameplay effects.
  - Keeps billing SDK callbacks separate from game-side reward logic.

- `Assets/Scripts/AdMobRewardedAdService.cs`
  - Wraps Google Mobile Ads rewarded-ad loading and display.
  - Blocks Google sample ad unit IDs in release builds unless explicitly enabled for internal QA.

### Unified Gold Boost Policy

- `Assets/Scripts/IAP/GoldMultiplierProvider.cs`
  - Exposes one multiplier API for both subscription and rewarded-ad boosts.
  - Persists subscription entitlement and timed ad boost expiry.
  - Keeps `GoldHudView` focused on display and reward arithmetic.

- `Assets/GoldHudView.cs`
  - Converts picked-up Money into Gold.
  - Applies `GoldMultiplierProvider.CurrentMultiplier`.
  - Keeps the latest earned amount for rewarded-ad double-gold claims.

### Save Reliability

- `Assets/Scripts/Save/SaveManager.cs`
  - Central save/load entry point.
  - Normalizes legacy/null save fields before runtime use.
  - Uses staged temp save files and backup restore to reduce data-loss risk.
  - Guards reset flow so scene unload saves do not restore stale progress.

- `Assets/Scripts/Save/SaveData.cs`
  - Stores currency, unlock progression, world state, IAP entitlement, ad boost expiry, and offer history.

### Data-Driven Progression

- `Assets/Scripts/Unlock/UnlockProgressionManager.cs`
  - Loads unlock table data into a progression model.
  - Subscribes to gameplay signals through R3.
  - Reveals groups of unlock points from resource, prison, and unlock-completion events.
  - Restores completed and revealed unlock state from save data.

- `Assets/Scripts/Unlock/UnlockResultExecutor.cs`
  - Keeps unlock-result side effects separate from progression state.
  - Routes unlock results to weapon upgrades, worker spawning, and prison expansion.

## Technical Stack

- Unity 6
- C#
- Unity IAP / Google Play Billing
- Google Mobile Ads rewarded ads
- Google Play Console internal testing workflow
- R3 reactive event subscriptions
- UniTask async ad flow
- TextMeshPro UI
- JsonUtility local persistence
- Git-based release/refactor history

## Resume Bullets

- Prepared a Unity mobile idle game for Google Play internal testing with rewarded ads, Unity IAP, save/load, and release QA safeguards.
- Built a monetization boundary that separates Play Billing callbacks from product reward execution and gameplay side effects.
- Unified rewarded-ad and subscription boosts behind a single gold multiplier provider with persisted entitlement and timed boost expiry.
- Refactored local persistence with save-data normalization, staged writes, backup restoration, and reset-safe scene reload handling.
- Implemented data-driven unlock progression using table data, R3 gameplay signals, presenter-managed unlock UI, and result execution routing.
- Added release safeguards for debug buttons, Google sample ad IDs, IAP test helpers, and generated Unity project files.

## Verification

- Latest C# build: passed.
- Compile errors: 0.
- Known warning: existing `System.Threading.Tasks.Extensions` version conflict from GoogleMobileAds.Editor dependencies.

## Internal Test Notes

- Current build intentionally allows Google rewarded-ad test IDs in release builds for internal QA.
- Before production release:
  - replace AdMob App ID and rewarded ad unit ID,
  - disable `allowTestAdUnitIdInReleaseBuild`,
  - upload a new AAB with incremented Android version code,
  - retest rewarded ads, gold boost, premium worker purchase, save/load, and reset.
