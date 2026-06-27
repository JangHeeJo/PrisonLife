using UnityEngine;
using R3;
using System;
/// <summary>
/// 광석을 받아 수갑으로 변환하는 제작기.
/// 플레이어는 ResourcePoint를 통해 Ore를 넣고, Handcuff를 가져간다.
/// </summary>
public class HandcuffMachine : MonoBehaviour, IResourceReceiver, IResourceProvider
{
    [Header("Storage")]
    [SerializeField] private int oreCapacity = 20;
    [SerializeField] private int handcuffCapacity = 10;

    [Header("Production")]
    [SerializeField] private int oreCostPerHandcuff = 1;
    [SerializeField] private float productionInterval = 1f;

    [Header("Diagnostics")]
    [SerializeField] private bool logState;

    [Header("Stack View")]
    [SerializeField] private ResourceStackView oreStackView;
    [SerializeField] private ResourceStackView handcuffStackView;

    private IDisposable carryLimitSubscription;

    private int oreAmount;
    private int handcuffAmount;
    private float productionTimer;

    private void Update()
    {
        ProduceTick();
    }
    private void OnEnable()
    {
        TrySubscribeCarryLimit();
        ApplyCurrentPointCapacityIfPossible();
    }

    private void Start()
    {
        // GameStateSignals Awake 순서가 늦는 경우를 대비합니다.
        TrySubscribeCarryLimit();
        ApplyCurrentPointCapacityIfPossible();
    }

    private void OnDisable()
    {
        carryLimitSubscription?.Dispose();
        carryLimitSubscription = null;
    }

    private void TrySubscribeCarryLimit()
    {
        if (carryLimitSubscription != null)
            return;

        if (GameStateSignals.Instance == null)
            return;

        carryLimitSubscription = GameStateSignals.Instance.PlayerCarryLimitChanged
            .Subscribe(OnPlayerCarryLimitChanged);
    }

    private void ApplyCurrentPointCapacityIfPossible()
    {
        if (GameStateSignals.Instance == null)
            return;

        ApplyPointCapacity(GameStateSignals.Instance.CurrentPointCapacity);
    }

    private void OnPlayerCarryLimitChanged(PlayerCarryLimitChangedSignal signal)
    {
        ApplyPointCapacity(signal.PointCapacity);
    }

    /// <summary>
    /// HandcuffMachine의 내부 저장 한도를 갱신합니다.
    /// 규칙: 제작기 내부 저장량 = 플레이어 CarryLimit * 2
    /// </summary>
    private void ApplyPointCapacity(int pointCapacity)
    {
        int safeCapacity = Mathf.Max(1, pointCapacity);

        // 현재 보유량보다 작아지면 데이터가 잘릴 수 있으므로,
        // 현재 수량 이상은 보장합니다.
        oreCapacity = Mathf.Max(safeCapacity, oreAmount);
        handcuffCapacity = Mathf.Max(safeCapacity, handcuffAmount);

        if (oreStackView != null)
            oreStackView.SetMaxCount(oreCapacity);

        if (handcuffStackView != null)
            handcuffStackView.SetMaxCount(handcuffCapacity);

        if (logState)
        {
            Debug.Log(
                $"[HandcuffMachine] ApplyPointCapacity. " +
                $"OreCapacity: {oreCapacity}, HandcuffCapacity: {handcuffCapacity}",
                this
            );
        }
    }
    private void ProduceTick()
    {
        if (oreAmount < oreCostPerHandcuff)
        {
            productionTimer = 0f;
            return;
        }

        if (handcuffAmount >= handcuffCapacity)
        {
            productionTimer = 0f;
            return;
        }

        if (handcuffStackView != null && handcuffStackView.IsFull)
        {
            productionTimer = 0f;
            return;
        }

        // ResourcePoint에서 광석이 아직 날아오는 중이면, 비주얼 도착 후 제작 시작
        if (oreStackView != null && oreStackView.IsEmpty)
            return;

        productionTimer += Time.deltaTime;

        if (productionTimer < productionInterval)
            return;

        productionTimer = 0f;
        ProduceHandcuff();
    }

    private void ProduceHandcuff()
    {
        oreAmount -= oreCostPerHandcuff;
        handcuffAmount++;

        // 광석 비주얼 제거
        if (oreStackView != null)
        {
            for (int i = 0; i < oreCostPerHandcuff; i++)
                oreStackView.HideLast();
        }

        // 수갑 비주얼 즉시 생성
        if (handcuffStackView != null)
            handcuffStackView.ShowNext();
    }

    public bool CanReceive(ResourceType type, int amount)
    {
        if (type != ResourceType.Ore)
            return false;

        return oreAmount + amount <= oreCapacity;
    }

    public bool TryReceive(ResourceType type, int amount)
    {
        if (!CanReceive(type, amount))
            return false;

        oreAmount += amount;
        return true;
    }

    public bool CanProvide(ResourceType type, int amount)
    {
        if (type != ResourceType.Handcuff)
            return false;

        return handcuffAmount >= amount;
    }

    public bool TryProvide(ResourceType type, int amount)
    {
        if (!CanProvide(type, amount))
            return false;

        handcuffAmount -= amount;
        return true;
    }
}