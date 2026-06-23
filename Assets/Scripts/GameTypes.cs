using System;

/// <summary>
/// 플레이어가 들고 다니거나, 포인트에서 주고받을 수 있는 자원 타입입니다.
/// </summary>
public enum ResourceType
{
    Handcuff,
    Ore,
    Money
}

/// <summary>
/// ResourcePoint가 어떤 방식으로 동작할지 결정하는 모드입니다.
/// </summary>
public enum ResourcePointMode
{
    Deposit,
    Pickup,
    Unlock,
    DepositAndPickup
}

/// <summary>
/// 자원 변화가 어디에서 발생했는지 구분하기 위한 타입입니다.
/// 튜토리얼뿐 아니라 업적, 미션, 퀘스트에서도 재사용할 수 있습니다.
/// </summary>
public enum ResourceOwnerType
{
    None,
    Player,
    Point,
    Storage,
    Machine,
    NpcArea,
    Prison,
    UnlockPoint
}

/// <summary>
/// 튜토리얼 Step 실행 방식입니다.
/// Sequential은 이전 Step 완료 후 이어지는 기본 흐름,
/// Reactive는 특정 게임 상태가 발생했을 때 독립적으로 시작되는 흐름입니다.
/// </summary>
public enum TutorialRunMode
{
    Sequential,
    Reactive
}

/// <summary>
/// 튜토리얼 Step이 시작되는 조건입니다.
/// Excel/TSV의 StartConditionType 컬럼 값과 이름이 일치해야 합니다.
/// </summary>
public enum TutorialStartConditionType
{
    None,
    OnTutorialStart,
    AfterStep,
    PlayerResourceFull,
    ResourceDeposited,
    UnlockPointAvailable,
    PrisonFull
}

/// <summary>
/// 튜토리얼에서 표시할 안내 연출 타입입니다.
/// Excel/TSV의 GuideType 컬럼 값과 이름이 일치해야 합니다.
/// </summary>
public enum TutorialGuideType
{
    None,
    WorldAnchorArrow,
    PlayerDirectionArrow,
    CameraFocus
}

/// <summary>
/// 튜토리얼 Step이 완료되는 조건입니다.
/// Excel/TSV의 CompleteConditionType 컬럼 값과 이름이 일치해야 합니다.
/// </summary>
public enum TutorialCompleteConditionType
{
    None,
    FirstInteract,
    ResourceChanged,
    ResourceDeposited,
    UnlockPointAvailable,
    UnlockPointUnlocked,
    PrisonFull,
    CameraReturned
}

/// <summary>
/// 목표 완료 판정 방식입니다.
/// FirstChanged는 최초 1회 반응,
/// Delta는 Step 시작 이후 증가량 기준,
/// State는 현재 상태 기준으로 사용합니다.
/// </summary>
public enum TutorialCompleteMode
{
    FirstChanged,
    Delta,
    State
}

/// <summary>
/// 월드 고정 화살표가 어떤 방향을 바라볼지 결정합니다.
/// </summary>
public enum TutorialGuideDirection
{
    Down,
    Forward
}

/// <summary>
/// 해금 포인트 상태입니다.
/// 해금 포인트 생성/활성화는 Tutorial이 아니라 WorldProgressionManager가 관리합니다.
/// </summary>
public enum UnlockPointState
{
    Hidden,
    Available,
    Unlocked
}
