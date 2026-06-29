using UnityEngine;

/// <summary>
/// Converts completed unlock-point data into concrete gameplay effects.
///
/// This keeps UnlockProgressionManager focused on progression state while this
/// class owns the side effects: weapon upgrades, worker spawning, and prison
/// expansion.
/// </summary>
public sealed class UnlockResultExecutor : MonoBehaviour
{
    [Header("Result Controllers")]
    [SerializeField] private PlayerWeaponUpgradeController weaponUpgradeController;
    [SerializeField] private UnitSpawnController unitSpawnController;
    [SerializeField] private PrisonExpansionController prisonExpansionController;

    [Header("Diagnostics")]
    [SerializeField] private bool logState;

    public void Execute(UnlockPointData data)
    {
        if (data == null)
            return;

        Log(
            $"Execute. UnlockId: {data.unlockId}, " +
            $"ResultType: {data.resultType}, " +
            $"ResultTargetId: {data.resultTargetId}, " +
            $"ResultValue: {data.resultValue}"
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

    private void ExecuteWeaponUpgrade(UnlockPointData data)
    {
        if (weaponUpgradeController == null)
        {
            Debug.LogWarning("[UnlockResultExecutor] WeaponUpgradeController is null.", this);
            return;
        }

        weaponUpgradeController.ApplyUpgrade(data.resultTargetId, data.resultValue);
    }

    private void ExecuteSpawnWorker(UnlockPointData data)
    {
        Log($"ExecuteSpawnWorker. UnitId: {data.resultTargetId}, Count: {data.resultValue}");

        if (unitSpawnController == null)
        {
            Debug.LogWarning("[UnlockResultExecutor] UnitSpawnController is null.", this);
            return;
        }

        unitSpawnController.SpawnUnits(data.resultTargetId, data.resultValue);
    }

    private void ExecutePrisonExpand(UnlockPointData data)
    {
        if (prisonExpansionController == null)
        {
            Debug.LogWarning("[UnlockResultExecutor] PrisonExpansionController is null.", this);
            return;
        }

        prisonExpansionController.ExpandPrison();
    }

    private void Log(string message)
    {
        if (logState)
            Debug.Log($"[UnlockResultExecutor] {message}", this);
    }
}
