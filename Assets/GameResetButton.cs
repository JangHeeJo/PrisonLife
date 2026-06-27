using UnityEngine;

/// <summary>
/// UI bridge for clearing local progress during development and QA.
/// </summary>
public sealed class GameResetButton : MonoBehaviour
{
    [Header("Option")]
    [Tooltip("When enabled, reset works only in the Editor or development builds.")]
    [SerializeField] private bool onlyDevelopmentBuild = false;

    [Tooltip("Logs reset attempts and guard results.")]
    [SerializeField] private bool logState;

    private void Awake()
    {
        if (onlyDevelopmentBuild && !Debug.isDebugBuild && !Application.isEditor)
            gameObject.SetActive(false);
    }

    public void ResetProgress()
    {
        if (onlyDevelopmentBuild && !Debug.isDebugBuild && !Application.isEditor)
        {
            if (logState)
                Debug.Log("[GameResetButton] Reset ignored. Not development build.", this);

            return;
        }

        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("[GameResetButton] SaveManager is null.", this);
            return;
        }

        if (logState)
            Debug.Log("[GameResetButton] Reset progress.", this);

        SaveManager.Instance.ResetProgressAndReloadCurrentScene();
    }
}
