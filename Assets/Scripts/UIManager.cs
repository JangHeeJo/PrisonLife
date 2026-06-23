using UnityEngine;

/// <summary>
/// 게임 내 UI View들을 한 곳에서 참조하기 위한 최소 UI 매니저.
/// 각 UI의 세부 갱신 로직은 개별 UI View가 담당합니다.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Common")]
    [SerializeField] private Canvas mainCanvas;
    [SerializeField] private Camera mainCamera;

    [Header("Gameplay UI")]
    [SerializeField] private NpcProcessProgressUI npcProcessProgressUI;

    public Canvas MainCanvas => mainCanvas;
    public Camera MainCamera => mainCamera;
    public NpcProcessProgressUI NpcProcessProgressUI => npcProcessProgressUI;

    private void Awake()
    {
        // 씬에 UIManager가 중복 생성되는 것을 방지합니다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        CacheReferences();
    }

    private void Start()
    {
        InitializeGameplayUI();
    }

    /// <summary>
    /// UIManager에 필요한 공통 참조를 찾습니다.
    /// Managers 오브젝트가 Canvas 아래에 없을 수도 있으므로 씬에서도 한 번 찾습니다.
    /// </summary>
    private void CacheReferences()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCanvas == null)
            mainCanvas = GetComponentInParent<Canvas>();

        if (mainCanvas == null)
            mainCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

        if (npcProcessProgressUI == null)
            npcProcessProgressUI = FindFirstObjectByType<NpcProcessProgressUI>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Gameplay UI View들을 초기화합니다.
    /// </summary>
    private void InitializeGameplayUI()
    {
        if (npcProcessProgressUI == null)
        {
            Debug.LogError("[UIManager] NpcProcessProgressUI가 연결되지 않았습니다.", this);
            return;
        }

        // NpcProcessProgressUIRoot 자체는 항상 켜져 있어야 합니다.
        // 실수로 비활성화되어 있으면 여기서 다시 켭니다.
        if (!npcProcessProgressUI.gameObject.activeSelf)
            npcProcessProgressUI.gameObject.SetActive(true);

        npcProcessProgressUI.Initialize(mainCanvas, mainCamera);
    }
}