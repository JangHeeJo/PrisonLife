using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Development-only helper that previews an UnlockPointView with progression data.
/// It disables itself outside the editor and development builds so test UI cannot run in release.
/// </summary>
public sealed class UnlockPointPresenterDebugTester : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("TextAsset containing UnlockPointProgressionData rows.")]
    [SerializeField] private TextAsset progressionTable;

    [Tooltip("UnlockId used by this development preview.")]
    [SerializeField] private string testUnlockId = "Weapon_Upgrade_01";

    [Header("World")]
    [Tooltip("World slot used by this development preview.")]
    [SerializeField] private UnlockPointSlot testSlot;

    [Header("Icon")]
    [SerializeField] private GameIconDatabase iconDatabase;

    [Header("Option")]
    [SerializeField] private bool revealOnStart = true;

    private UnlockPointPresenter presenter;

    private void Start()
    {
        if (revealOnStart)
            RevealTestUnlockPoint();
    }

    [ContextMenu("Reveal Test Unlock Point")]
    public void RevealTestUnlockPoint()
    {
        DisposeCurrentPresenter();

        if (progressionTable == null)
        {
            Debug.LogError("[UnlockPointPresenterDebugTester] Progression table is null.", this);
            return;
        }

        if (testSlot == null)
        {
            Debug.LogError("[UnlockPointPresenterDebugTester] Test slot is null.", this);
            return;
        }

        if (testSlot.UnlockPointView == null)
        {
            Debug.LogError("[UnlockPointPresenterDebugTester] Test slot has no UnlockPointView.", this);
            return;
        }

        List<UnlockPointData> loadedData = UnlockPointProgressionTableLoader.Load(progressionTable);
        UnlockPointData targetData = FindData(loadedData, testUnlockId);

        if (targetData == null)
        {
            Debug.LogError($"[UnlockPointPresenterDebugTester] Unlock data not found. UnlockId: {testUnlockId}", this);
            return;
        }

        presenter = new UnlockPointPresenter(
            targetData,
            testSlot,
            testSlot.UnlockPointView,
            iconDatabase,
            OnTestUnlocked
        );

        presenter.Reveal();

        Debug.Log($"[UnlockPointPresenterDebugTester] Revealed test unlock point: {targetData.unlockId}", this);
    }

    private static bool CanRunTestActions()
    {
        return Debug.isDebugBuild || Application.isEditor;
    }

    private UnlockPointData FindData(List<UnlockPointData> loadedData, string unlockId)
    {
        if (loadedData == null || string.IsNullOrEmpty(unlockId))
            return null;

        for (int i = 0; i < loadedData.Count; i++)
        {
            UnlockPointData data = loadedData[i];

            if (data == null)
                continue;

            if (data.unlockId == unlockId)
                return data;
        }

        return null;
    }

    private void OnTestUnlocked(UnlockPointData data)
    {
        Debug.Log($"[UnlockPointPresenterDebugTester] Unlock completed: {data.unlockId}", this);
    }

    private void DisposeCurrentPresenter()
    {
        if (presenter != null)
        {
            presenter.Dispose();
            presenter = null;
        }
    }

    private void OnDestroy()
    {
        DisposeCurrentPresenter();
    }
}