using UnityEngine;

/// <summary>
/// 진행도 초기화 버튼입니다.
/// 
/// 사용처:
/// - 테스트용 초기화 버튼
/// - 옵션 화면의 Reset Progress 버튼
/// </summary>
public sealed class GameResetButton : MonoBehaviour
{
    [Header("Option")]
    [Tooltip("true면 에디터/개발 빌드에서만 동작합니다.")]
    [SerializeField] private bool onlyDevelopmentBuild = false;

    [Tooltip("초기화 실행 전 로그를 출력합니다.")]
    [SerializeField] private bool logState = true;

    public void ResetProgress()
    {
        if (onlyDevelopmentBuild)
        {
            if (!Debug.isDebugBuild && !Application.isEditor)
            {
                if (logState)
                    Debug.Log("[GameResetButton] Reset ignored. Not development build.", this);

                return;
            }
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