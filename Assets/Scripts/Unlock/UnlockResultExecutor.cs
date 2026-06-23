using UnityEngine;

/// <summary>
/// UnlockPoint 해금 완료 후 실제 결과를 실행하는 클래스입니다.
/// 
/// 역할:
/// - UnlockPointData의 ResultType을 보고 어떤 시스템에 넘길지 결정합니다.
/// - 직접 무기 강화, 유닛 생성, 감옥 확장 로직을 길게 들고 있지 않습니다.
/// - 실제 세부 처리는 각 전용 Controller에게 위임합니다.
/// 
/// 흐름:
/// UnlockPoint 완료
/// → UnlockProgressionManager
/// → UnlockResultExecutor.Execute(data)
/// → ResultType에 따라 각 Controller 호출
/// </summary>
public sealed class UnlockResultExecutor : MonoBehaviour
{
    [Header("Result Controllers")]
    [SerializeField] private PlayerWeaponUpgradeController weaponUpgradeController;
    [SerializeField] private UnitSpawnController unitSpawnController;
    [SerializeField] private PrisonExpansionController prisonExpansionController;

    /// <summary>
    /// 해금 결과를 실행합니다.
    /// 어떤 ResultType으로 들어오는지 확인하기 위해 로그를 명확하게 남깁니다.
    /// </summary>
    public void Execute(UnlockPointData data)
    {
        if (data == null)
            return;

        Debug.Log(
            $"[UnlockResultExecutor] Execute. " +
            $"UnlockId: {data.unlockId}, " +
            $"ResultType: {data.resultType}, " +
            $"ResultTargetId: {data.resultTargetId}, " +
            $"ResultValue: {data.resultValue}",
            this
        );

        switch (data.resultType)
        {
            case UnlockResultType.None:
                Debug.LogWarning($"[UnlockResultExecutor] ResultType is None. UnlockId: {data.unlockId}", this);
                break;

            case UnlockResultType.UpgradeWeapon:
                ExecuteWeaponUpgrade(data);
                break;

            case UnlockResultType.SpawnAutoWorker:
                ExecuteSpawnWorker(data);
                break;

            case UnlockResultType.ExpandPrison:
                ExecutePrisonExpand(data);
                break;

            case UnlockResultType.ActivateObject:
                Debug.LogWarning(
                    $"[UnlockResultExecutor] ActivateObject is not implemented yet. Target: {data.resultTargetId}",
                    this
                );
                break;

            default:
                Debug.LogWarning(
                    $"[UnlockResultExecutor] Unsupported result type: {data.resultType}, UnlockId: {data.unlockId}",
                    this
                );
                break;
        }
    }

    /// <summary>
    /// 플레이어 무기/채굴 도구 강화 결과를 실행합니다.
    /// </summary>
    private void ExecuteWeaponUpgrade(UnlockPointData data)
    {
        if (weaponUpgradeController == null)
        {
            Debug.LogWarning("[UnlockResultExecutor] WeaponUpgradeController is null.", this);
            return;
        }

        weaponUpgradeController.ApplyUpgrade(
            data.resultTargetId,
            data.resultValue
        );
    }

    /// <summary>
    /// 자동화 유닛 생성 결과를 실행합니다.
    /// </summary>
    private void ExecuteSpawnWorker(UnlockPointData data)
    {
        Debug.Log(
            $"[UnlockResultExecutor] ExecuteSpawnWorker. UnitId: {data.resultTargetId}, Count: {data.resultValue}",
            this
        );

        if (unitSpawnController == null)
        {
            Debug.LogWarning("[UnlockResultExecutor] UnitSpawnController is null.", this);
            return;
        }

        unitSpawnController.SpawnUnits(
            data.resultTargetId,
            data.resultValue
        );
    }

    /// <summary>
    /// 감옥 확장 결과를 실행합니다.
    /// </summary>
    private void ExecutePrisonExpand(UnlockPointData data)
    {
        if (prisonExpansionController == null)
        {
            Debug.LogWarning("[UnlockResultExecutor] PrisonExpansionController is null.", this);
            return;
        }

        prisonExpansionController.ExpandPrison(
        );
    }
}