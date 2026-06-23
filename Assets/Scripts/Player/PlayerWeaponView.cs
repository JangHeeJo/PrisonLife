using UnityEngine;

/// <summary>
/// 플레이어 무기 모델 표시를 담당하는 View입니다.
/// 
/// 역할:
/// - WeaponVisualId로 무기 프리팹을 찾습니다.
/// - 기존 무기 모델을 제거합니다.
/// - WeaponSocket 하위에 새 무기 모델을 생성합니다.
/// - 생성된 무기 안의 PlayerMiningDetector를 찾아 반환합니다.
/// </summary>
public sealed class PlayerWeaponView : MonoBehaviour
{
    [Header("Database")]
    [SerializeField] private WeaponVisualDatabase visualDatabase;

    [Header("Socket")]
    [Tooltip("무기 프리팹이 생성될 위치입니다. 이 스크립트가 WeaponSocket에 붙어 있다면 비워둬도 됩니다.")]
    [SerializeField] private Transform weaponSocket;

    [Header("Visual Root")]
    [Tooltip("무기 프리팹 안에서 모델만 담고 있는 자식 이름입니다.")]
    [SerializeField] private string visualRootName = "VisualRoot";

    [Header("Option")]
    [Tooltip("채굴 대상이 없을 때 무기 모델만 숨길지 여부입니다. MiningDetector는 꺼지면 안 됩니다.")]
    [SerializeField] private bool allowVisibilityControl = true;

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

    /// <summary>
    /// WeaponVisualId에 해당하는 무기 모델로 교체합니다.
    /// 교체 후 현재 무기 프리팹 안의 PlayerMiningDetector를 반환합니다.
    /// </summary>
    public PlayerMiningDetector ApplyVisual(string weaponVisualId)
    {
        Debug.Log($"[PlayerWeaponView] ApplyVisual called. VisualId: {weaponVisualId}", this);

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
            Debug.Log($"[PlayerWeaponView] Same weapon already equipped: {weaponVisualId}", this);
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

        //currentWeaponInstance.transform.localPosition = Vector3.zero;
        //currentWeaponInstance.transform.localRotation = Quaternion.identity;
        //currentWeaponInstance.transform.localScale = Vector3.one;

        currentWeaponInstance.SetActive(true);

        CacheCurrentWeaponParts();

        Debug.Log(
            $"[PlayerWeaponView] Weapon created. " +
            $"VisualId: {weaponVisualId}, " +
            $"Instance: {currentWeaponInstance.name}, " +
            $"Detector: {(currentMiningDetector == null ? "NULL" : currentMiningDetector.name)}",
            this
        );

        return currentMiningDetector;
    }

    /// <summary>
    /// 현재 무기 모델만 보이거나 숨깁니다.
    /// 무기 전체를 끄면 MiningDetector도 꺼지므로 VisualRoot만 제어합니다.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (!allowVisibilityControl)
            return;

        if (currentVisualRoot == null)
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
}