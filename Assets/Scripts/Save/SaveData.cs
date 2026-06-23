using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 진행도를 디스크에 저장하기 위한 루트 DTO입니다.
/// Unity JsonUtility가 직렬화할 수 있도록 public field 기반의 단순 데이터 구조로 유지합니다.
/// </summary>
[Serializable]
public sealed class GameSaveData
{
    public int version = 1;

    [Header("Currency")]
    public int gold;

    [Header("Player Upgrade")]
    public string weaponLevelId;
    public int weaponLevel;
    public int carryLimit;
    public int pointCapacity;

    [Header("Legacy Prison State")]
    public int prisonCurrentCount;
    public int prisonMaxCount;
    public bool prisonExpanded;

    [Header("Unlock Progress")]
    public List<string> completedUnlockIds = new List<string>();
    public List<string> revealedUnlockGroupIds = new List<string>();

    [Header("World State")]
    public List<SpawnedUnitSaveData> spawnedUnits = new List<SpawnedUnitSaveData>();
    public List<MoneyRewardPointSaveData> moneyRewardPoints = new List<MoneyRewardPointSaveData>();
    public List<ResourcePointSaveData> resourcePoints = new List<ResourcePointSaveData>();
    public List<PrisonAreaSaveData> prisonAreas = new List<PrisonAreaSaveData>();
    public List<PrisonExpansionSaveData> prisonExpansions = new List<PrisonExpansionSaveData>();

    [Header("Purchase Entitlements")]
    public List<IapEntitlementSaveData> iapEntitlements = new List<IapEntitlementSaveData>();
}

[Serializable]
public sealed class SpawnedUnitSaveData
{
    public string unitId;
    public int count;
}

[Serializable]
public sealed class ResourcePointSaveData
{
    public string pointId;
    public ResourceType resourceType;
    public int amount;
}

[Serializable]
public sealed class MoneyRewardPointSaveData
{
    public string saveId;
    public int amount;
}

[Serializable]
public sealed class PrisonAreaSaveData
{
    public string prisonId;
    public int currentCount;
    public int maxCount;
    public bool hasRaisedFull;
}

[Serializable]
public sealed class PrisonExpansionSaveData
{
    public string expansionId;
    public bool expanded;
}

[Serializable]
public sealed class IapEntitlementSaveData
{
    public string productId;
    public bool active;
}
