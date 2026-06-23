using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 저장 데이터를 로드, 정규화, 저장하는 단일 진입점입니다.
///
/// 설계 의도:
/// - 저장 파일 경로와 직렬화 방식을 한 곳에서 관리합니다.
/// - 저장 데이터가 오래된 버전이거나 일부 리스트가 비어 있어도 런타임 코드가 안전하게 접근하도록 정규화합니다.
/// - 임시 파일에 먼저 기록한 뒤 실제 저장 파일로 교체해 저장 중단 시 데이터 손상 가능성을 줄입니다.
/// </summary>
public sealed class SaveManager : MonoBehaviour
{
    private const string SaveFileName = "save_data.json";
    private const int CurrentSaveVersion = 1;

    public static SaveManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool logState = true;

    private GameSaveData currentData;
    private bool isResetting;

    public GameSaveData CurrentData => currentData;
    public bool IsResetting => isResetting;

    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    private string TempSavePath => SavePath + ".tmp";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadOrCreate();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
            Save();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    /// <summary>
    /// 저장 파일을 읽어 현재 저장 데이터로 사용합니다.
    /// 파일이 없거나 읽기 실패 시 새 저장 데이터를 생성합니다.
    /// </summary>
    public void LoadOrCreate()
    {
        if (!File.Exists(SavePath))
        {
            CreateNewSaveData("New save created");
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            currentData = JsonUtility.FromJson<GameSaveData>(json) ?? new GameSaveData();
            NormalizeCurrentData();

            Log($"Save loaded. Path: {SavePath}");
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            CreateNewSaveData("Save load failed. New save created");
        }
    }

    /// <summary>
    /// 현재 저장 데이터를 디스크에 기록합니다.
    /// 리셋 중에는 다른 컴포넌트의 OnDisable 저장 요청을 무시합니다.
    /// </summary>
    public void Save()
    {
        if (isResetting)
        {
            Log("Save ignored because reset is in progress.");
            return;
        }

        EnsureCurrentData();
        NormalizeCurrentData();

        try
        {
            EnsureSaveDirectory();

            string json = JsonUtility.ToJson(currentData, true);
            File.WriteAllText(TempSavePath, json);

            if (File.Exists(SavePath))
                File.Delete(SavePath);

            File.Move(TempSavePath, SavePath);
            Log($"Saved. Path: {SavePath}");
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
    }

    /// <summary>
    /// 외부 시스템이 저장 데이터 변경 후 호출하는 명시적 저장 요청입니다.
    /// 현재는 즉시 저장하지만, 추후 지연 저장이나 배치 저장으로 바꿔도 호출부를 유지할 수 있습니다.
    /// </summary>
    public void MarkDirtyAndSave()
    {
        Save();
    }

    /// <summary>
    /// 저장 파일과 임시 저장 파일을 제거하고 런타임 데이터를 초기 상태로 되돌립니다.
    /// </summary>
    public void ClearSave()
    {
        currentData = new GameSaveData();
        NormalizeCurrentData();

        DeleteIfExists(SavePath);
        DeleteIfExists(TempSavePath);

        Log("Save cleared.");
    }

    /// <summary>
    /// 진행도를 초기화한 뒤 현재 씬을 다시 로드합니다.
    /// 씬 언로드 중 호출되는 저장 요청이 초기화 데이터를 덮어쓰지 않도록 isResetting으로 보호합니다.
    /// </summary>
    public void ResetProgressAndReloadCurrentScene()
    {
        if (isResetting)
            return;

        isResetting = true;
        Time.timeScale = 1f;

        ClearSave();

        int activeSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(activeSceneIndex);
    }

    private void CreateNewSaveData(string reason)
    {
        currentData = new GameSaveData();
        NormalizeCurrentData();
        Log($"{reason}. Path: {SavePath}");
    }

    private void EnsureCurrentData()
    {
        currentData ??= new GameSaveData();
    }

    private void EnsureSaveDirectory()
    {
        string directory = Path.GetDirectoryName(SavePath);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    /// 저장 데이터의 null 컬렉션과 버전을 보정합니다.
    /// 새 필드가 추가되어도 기존 저장 파일을 최대한 안전하게 이어서 사용할 수 있게 하는 방어 계층입니다.
    /// </summary>
    private void NormalizeCurrentData()
    {
        EnsureCurrentData();

        if (currentData.version <= 0)
            currentData.version = CurrentSaveVersion;

        currentData.completedUnlockIds = EnsureList(currentData.completedUnlockIds);
        currentData.revealedUnlockGroupIds = EnsureList(currentData.revealedUnlockGroupIds);
        currentData.spawnedUnits = EnsureList(currentData.spawnedUnits);
        currentData.moneyRewardPoints = EnsureList(currentData.moneyRewardPoints);
        currentData.resourcePoints = EnsureList(currentData.resourcePoints);
        currentData.prisonAreas = EnsureList(currentData.prisonAreas);
        currentData.prisonExpansions = EnsureList(currentData.prisonExpansions);
        currentData.iapEntitlements = EnsureList(currentData.iapEntitlements);
    }

    private static List<T> EnsureList<T>(List<T> list)
    {
        return list ?? new List<T>();
    }

    private void Log(string message)
    {
        if (logState)
            Debug.Log($"[SaveManager] {message}", this);
    }
}
