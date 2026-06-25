using UnityEngine;

/// <summary>
/// Swaps the player's weapon visual and exposes the mining detector on the equipped model.
/// </summary>
public sealed class PlayerWeaponView : MonoBehaviour
{
    [Header("Database")]
    [SerializeField] private WeaponVisualDatabase visualDatabase;

    [Header("Socket")]
    [Tooltip("Parent transform used for spawned weapon visuals. Defaults to this transform.")]
    [SerializeField] private Transform weaponSocket;

    [Header("Visual Root")]
    [Tooltip("Child object name used to hide or show only the visible weapon model.")]
    [SerializeField] private string visualRootName = "VisualRoot";

    [Header("Option")]
    [Tooltip("When enabled, SetVisible toggles the visual root while keeping mining detectors active.")]
    [SerializeField] private bool allowVisibilityControl = true;

    [Header("Debug")]
    [SerializeField] private bool logState;

    private GameObject currentWeaponInstance;
    private GameObject currentVisualRoot;
    private PlayerMiningDetector currentMiningDetector;
    private string currentVisualId;

    public string CurrentVisualId => currentVisualId;
    public PlayerMiningDetector CurrentMiningDetector => currentMiningDetector;

    private void Awake()
    {
        if (weaponSocket == null)
            weaponSocket = transform;
    }

    public PlayerMiningDetector ApplyVisual(string weaponVisualId)
    {
        Log($"ApplyVisual called. VisualId: {weaponVisualId}");

        if (string.IsNullOrEmpty(weaponVisualId))
        {
            Debug.LogWarning("[PlayerWeaponView] WeaponVisualId is empty.", this);
            return null;
        }

        if (visualDatabase == null)
        {
            Debug.LogWarning("[PlayerWeaponView] VisualDatabase is null.", this);
            return null;
        }

        if (weaponSocket == null)
        {
            Debug.LogWarning("[PlayerWeaponView] WeaponSocket is null.", this);
            return null;
        }

        if (currentWeaponInstance != null && currentVisualId == weaponVisualId)
        {
            Log($"Same weapon already equipped: {weaponVisualId}");
            return currentMiningDetector;
        }

        GameObject weaponPrefab = visualDatabase.GetPrefab(weaponVisualId);
        if (weaponPrefab == null)
        {
            Debug.LogWarning($"[PlayerWeaponView] Weapon prefab not found. VisualId: {weaponVisualId}", this);
            return null;
        }

        ClearCurrentWeapon();

        currentWeaponInstance = Instantiate(weaponPrefab, weaponSocket);
        currentWeaponInstance.name = weaponPrefab.name;
        currentVisualId = weaponVisualId;
        currentWeaponInstance.SetActive(true);

        CacheCurrentWeaponParts();

        Log(
            $"Weapon created. VisualId: {weaponVisualId}, " +
            $"Instance: {currentWeaponInstance.name}, " +
            $"Detector: {(currentMiningDetector == null ? "NULL" : currentMiningDetector.name)}"
        );

        return currentMiningDetector;
    }

    public void SetVisible(bool visible)
    {
        if (!allowVisibilityControl || currentVisualRoot == null)
            return;

        if (currentVisualRoot.activeSelf != visible)
            currentVisualRoot.SetActive(visible);
    }

    private void CacheCurrentWeaponParts()
    {
        currentVisualRoot = null;
        currentMiningDetector = null;

        if (currentWeaponInstance == null)
            return;

        Transform visualRoot = currentWeaponInstance.transform.Find(visualRootName);

        if (visualRoot != null)
        {
            currentVisualRoot = visualRoot.gameObject;
        }
        else
        {
            Debug.LogWarning(
                $"[PlayerWeaponView] VisualRoot not found. Expected child name: {visualRootName}. " +
                "Weapon visibility control will be ignored.",
                currentWeaponInstance
            );
        }

        currentMiningDetector = currentWeaponInstance.GetComponentInChildren<PlayerMiningDetector>(true);

        if (currentMiningDetector != null)
            currentMiningDetector.gameObject.SetActive(true);
    }

    private void ClearCurrentWeapon()
    {
        if (currentWeaponInstance == null)
            return;

        Destroy(currentWeaponInstance);

        currentWeaponInstance = null;
        currentVisualRoot = null;
        currentMiningDetector = null;
        currentVisualId = string.Empty;
    }

    private void Log(string message)
    {
        if (logState)
            Debug.Log($"[PlayerWeaponView] {message}", this);
    }
}