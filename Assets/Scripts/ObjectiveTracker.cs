using R3;
using UnityEngine;

/// <summary>
/// TutorialStepData의 StartCondition / CompleteCondition을 실제 GameStateSignals 스트림으로 변환합니다.
/// Presenter는 이 Tracker가 만들어준 Observable만 구독하면 됩니다.
/// </summary>
public sealed class ObjectiveTracker
{
    private readonly GameStateSignals signals;
    private readonly Subject<Unit> tutorialStarted = new();
    private readonly Subject<string> stepCompleted = new();

    public ObjectiveTracker(GameStateSignals signals)
    {
        this.signals = signals;
    }

    public void NotifyTutorialStarted()
    {
        tutorialStarted.OnNext(Unit.Default);
    }

    public void NotifyStepCompleted(string stepId)
    {
        if (string.IsNullOrEmpty(stepId))
            return;

        stepCompleted.OnNext(stepId);
    }

    public Observable<Unit> ObserveStartCondition(TutorialStepData step)
    {
        if (step == null || signals == null)
            return Observable.Empty<Unit>();

        switch (step.startConditionType)
        {
            case TutorialStartConditionType.OnTutorialStart:
                return tutorialStarted;

            case TutorialStartConditionType.AfterStep:
                return stepCompleted
                    .Where(completedStepId => completedStepId == step.startTargetId)
                    .Select(_ => Unit.Default);

            case TutorialStartConditionType.PlayerResourceFull:
                return signals.ResourceFull
                    .Where(signal =>
                        signal.OwnerType == ResourceOwnerType.Player &&
                        IsTargetMatch(step.startTargetId, signal.TargetId) &&
                        IsResourceMatch(step.startResourceType, signal.ResourceType))
                    .Select(_ => Unit.Default);

            case TutorialStartConditionType.ResourceDeposited:
                return signals.ResourceDeposited
                    .Where(signal =>
                        IsTargetMatch(step.startTargetId, signal.TargetId) &&
                        IsResourceMatch(step.startResourceType, signal.ResourceType))
                    .Select(_ => Unit.Default);

            case TutorialStartConditionType.UnlockPointAvailable:
                return signals.UnlockPointStateChanged
                    .Where(signal =>
                        signal.State == UnlockPointState.Available &&
                        IsTargetMatch(step.startTargetId, signal.UnlockPointId))
                    .Select(_ => Unit.Default);

            case TutorialStartConditionType.PrisonFull:
                return signals.PrisonFull
                    .Where(signal => IsTargetMatch(step.startTargetId, signal.PrisonId))
                    .Select(_ => Unit.Default);
        }

        return Observable.Empty<Unit>();
    }

    public Observable<Unit> ObserveCompleteCondition(TutorialStepData step)
    {
        if (step == null || signals == null)
            return Observable.Empty<Unit>();

        switch (step.completeConditionType)
        {
            case TutorialCompleteConditionType.FirstInteract:
                return signals.Interacted
                    .Where(signal => IsTargetMatch(step.completeTargetId, signal.TargetId))
                    .Select(_ => Unit.Default);

            case TutorialCompleteConditionType.ResourceChanged:
                return signals.ResourceAmountChanged
                    .Where(signal =>
                        IsTargetMatch(step.completeTargetId, signal.TargetId) &&
                        IsResourceMatch(step.completeResourceType, signal.ResourceType))
                    .Select(_ => Unit.Default);

            case TutorialCompleteConditionType.ResourceDeposited:
                return signals.ResourceDeposited
                    .Where(signal =>
                        IsTargetMatch(step.completeTargetId, signal.TargetId) &&
                        IsResourceMatch(step.completeResourceType, signal.ResourceType))
                    .Select(_ => Unit.Default);

            case TutorialCompleteConditionType.UnlockPointAvailable:
                return signals.UnlockPointStateChanged
                    .Where(signal =>
                        signal.State == UnlockPointState.Available &&
                        IsTargetMatch(step.completeTargetId, signal.UnlockPointId))
                    .Select(_ => Unit.Default);

            case TutorialCompleteConditionType.UnlockPointUnlocked:
                return signals.UnlockPointStateChanged
                    .Where(signal =>
                        signal.State == UnlockPointState.Unlocked &&
                        IsTargetMatch(step.completeTargetId, signal.UnlockPointId))
                    .Select(_ => Unit.Default);

            case TutorialCompleteConditionType.PrisonFull:
                return signals.PrisonFull
                    .Where(signal => IsTargetMatch(step.completeTargetId, signal.PrisonId))
                    .Select(_ => Unit.Default);

            case TutorialCompleteConditionType.CameraReturned:
                return signals.CameraReturned
                    .Where(signal => IsTargetMatch(step.completeTargetId, signal.TargetId))
                    .Select(_ => Unit.Default);
        }

        return Observable.Empty<Unit>();
    }

    private bool IsTargetMatch(string expectedTargetId, string actualTargetId)
    {
        // expectedTargetId가 비어 있으면 모든 대상에서 발생한 신호를 허용한다.
        if (string.IsNullOrEmpty(expectedTargetId))
            return true;

        return expectedTargetId == actualTargetId;
    }

    private bool IsResourceMatch(ResourceType? expectedResourceType, ResourceType actualResourceType)
    {
        // expectedResourceType이 비어 있으면 모든 자원 타입을 허용한다.
        if (!expectedResourceType.HasValue)
            return true;

        return expectedResourceType.Value == actualResourceType;
    }
}
