/// <summary>
/// UnitSpawnController가 유닛을 생성한 직후 전달하는 런타임 컨텍스트입니다.
/// 프리팹은 Scene 오브젝트를 직접 참조하기 어렵기 때문에,
/// 생성 시점에 필요한 Scene 참조를 주입하기 위해 사용합니다.
/// </summary>
public readonly struct UnitSpawnContext
{
    public readonly string UnitId;

    // AutoMinerWorker용
    public readonly MineableOre[] OreTargets;
    public readonly ResourcePoint OreDepositPoint;

    // HandcuffDeliveryWorker용
    public readonly ResourcePoint HandcuffPickupPoint;
    public readonly ResourcePoint HandcuffDepositPoint;
    public readonly int CarryLimit;

    public UnitSpawnContext(
        string unitId,
        MineableOre[] oreTargets,
        ResourcePoint oreDepositPoint,
        ResourcePoint handcuffPickupPoint,
        ResourcePoint handcuffDepositPoint,
        int carryLimit)
    {
        UnitId = unitId;

        OreTargets = oreTargets;
        OreDepositPoint = oreDepositPoint;

        HandcuffPickupPoint = handcuffPickupPoint;
        HandcuffDepositPoint = handcuffDepositPoint;
        CarryLimit = carryLimit;
    }
}

/// <summary>
/// 생성 직후 UnitSpawnController로부터 컨텍스트를 받을 수 있는 유닛 인터페이스입니다.
/// </summary>
public interface IUnitSpawnContextReceiver
{
    void Initialize(UnitSpawnContext context);
}