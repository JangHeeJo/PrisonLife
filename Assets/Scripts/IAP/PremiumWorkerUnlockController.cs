using UnityEngine;

/// <summary>
/// 비소모성 IAP 상품으로 프리미엄 작업자/스킨 해금 상태를 관리합니다.
///
/// 현재 단계에서는 실제 결제 SDK 없이도 IapRewardExecutor 또는 디버그 버튼에서
/// UnlockPremiumWorker()를 호출해 효과 적용을 테스트할 수 있습니다.
/// </summary>
public sealed class PremiumWorkerUnlockController : MonoBehaviour
{
    [Header("Product")]
    [SerializeField] private string productId = IapProductIds.PremiumWorkerUnlock;

    [Header("Visual / Feature")]
    [Tooltip("프리미엄 해금 시 켤 오브젝트들입니다. 예: 스킨 버튼, 프리미엄 작업자 표시 UI")]
    [SerializeField] private GameObject[] objectsToEnableOnUnlocked;

    [Header("Optional Spawn")]
    [Tooltip("해금 순간 프리미엄 유닛을 1회 생성할지 여부입니다.")]
    [SerializeField] private bool spawnUnitOnFirstUnlock;

    [SerializeField] private UnitSpawnController unitSpawnController;

    [Tooltip("UnitSpawnController의 UnitId와 동일해야 합니다. 예: PremiumWorker")]
    [SerializeField] private string premiumUnitId = "PremiumWorker";

    [SerializeField] private int spawnCount = 1;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private bool unlocked;

    public bool IsUnlocked => unlocked;

    private void Start()
    {
        RefreshFromSave();
    }

    public void RefreshFromSave()
    {
        bool active = false;

        if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
            active = IapEntitlementState.IsActive(SaveManager.Instance.CurrentData, productId);

        SetUnlockedVisual(active);
        unlocked = active;

        if (logState)
            Debug.Log($"[PremiumWorkerUnlockController] RefreshFromSave. Unlocked: {unlocked}", this);
    }

    public void UnlockPremiumWorker()
    {
        if (unlocked)
        {
            SetUnlockedVisual(true);
            return;
        }

        unlocked = true;

        if (SaveManager.Instance != null &&
            SaveManager.Instance.CurrentData != null &&
            !SaveManager.Instance.IsResetting)
        {
            IapEntitlementState.SetActive(
                SaveManager.Instance.CurrentData,
                productId,
                true
            );

            SaveManager.Instance.MarkDirtyAndSave();
        }

        SetUnlockedVisual(true);

        if (spawnUnitOnFirstUnlock && unitSpawnController != null && !string.IsNullOrEmpty(premiumUnitId))
            unitSpawnController.SpawnUnits(premiumUnitId, Mathf.Max(1, spawnCount));

        if (logState)
            Debug.Log("[PremiumWorkerUnlockController] Premium worker unlocked.", this);
    }

    public void ClearUnlockForTest()
    {
        unlocked = false;

        if (SaveManager.Instance != null &&
            SaveManager.Instance.CurrentData != null &&
            !SaveManager.Instance.IsResetting)
        {
            IapEntitlementState.SetActive(
                SaveManager.Instance.CurrentData,
                productId,
                false
            );

            SaveManager.Instance.MarkDirtyAndSave();
        }

        SetUnlockedVisual(false);
    }

    private static bool CanRunTestActions()
    {
        return Debug.isDebugBuild || Application.isEditor;
    }

    private void SetUnlockedVisual(bool active)
    {
        if (objectsToEnableOnUnlocked == null)
            return;

        for (int i = 0; i < objectsToEnableOnUnlocked.Length; i++)
        {
            if (objectsToEnableOnUnlocked[i] != null)
                objectsToEnableOnUnlocked[i].SetActive(active);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        spawnCount = Mathf.Max(1, spawnCount);

        if (string.IsNullOrEmpty(productId))
            productId = IapProductIds.PremiumWorkerUnlock;
    }
#endif
}
