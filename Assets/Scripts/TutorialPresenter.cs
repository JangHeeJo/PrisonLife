using System.Collections.Generic;
using R3;
using UnityEngine;

/// <summary>
/// TutorialPresenter는 Model과 View 사이에서 튜토리얼 흐름을 제어합니다.
/// Step 시작 조건과 완료 조건은 ObjectiveTracker를 통해 R3로 구독하고,
/// 실제 화살표/카메라 연출은 View에 요청합니다.
/// </summary>
public sealed class TutorialPresenter
{
    private sealed class ActiveStepContext
    {
        public TutorialStepData Step;
        public TutorialDisposableGroup Disposables;
    }

    private readonly TutorialModel model;
    private readonly ITutorialView view;
    private readonly ObjectiveTracker objectiveTracker;

    private readonly Dictionary<string, ActiveStepContext> activeSteps = new();
    private readonly TutorialDisposableGroup presenterDisposables = new();

    private bool isStarted;

    public TutorialPresenter(
        TutorialModel model,
        ITutorialView view,
        ObjectiveTracker objectiveTracker)
    {
        this.model = model;
        this.view = view;
        this.objectiveTracker = objectiveTracker;
    }

    /// <summary>
    /// 튜토리얼 시스템 시작.
    /// Reactive Step의 시작 조건 구독을 먼저 걸고, Sequential 시작 Step을 실행합니다.
    /// </summary>
    public void StartTutorial(string startStepId)
    {
        if (isStarted)
            return;

        isStarted = true;

        RegisterReactiveStartConditions();
        StartStep(startStepId);

        // OnTutorialStart 조건을 쓰는 Reactive Step이 있다면 같이 반응할 수 있게 신호를 흘린다.
        objectiveTracker.NotifyTutorialStarted();
    }

    /// <summary>
    /// 튜토리얼을 강제로 종료하거나 씬 종료 시 모든 구독과 연출을 정리합니다.
    /// </summary>
    public void Dispose()
    {
        presenterDisposables.Dispose();

        foreach (ActiveStepContext context in activeSteps.Values)
            context.Disposables.Dispose();

        activeSteps.Clear();
        view.HideAllGuides();
    }

    private void RegisterReactiveStartConditions()
    {
        IReadOnlyList<TutorialStepData> steps = model.Steps;

        for (int i = 0; i < steps.Count; i++)
        {
            TutorialStepData step = steps[i];

            if (step == null || !step.enabled)
                continue;

            // Sequential Step은 nextStepId 흐름으로 직접 이어가기 때문에 별도 시작 구독을 걸지 않는다.
            if (step.runMode != TutorialRunMode.Reactive)
                continue;

            TutorialStepData capturedStep = step;

            presenterDisposables.Add(
                objectiveTracker.ObserveStartCondition(capturedStep)
                    .Subscribe(_ => StartStep(capturedStep.stepId))
            );
        }
    }

    private void StartStep(string stepId)
    {
        if (string.IsNullOrEmpty(stepId))
            return;

        if (!model.TryGetStep(stepId, out TutorialStepData step))
            return;

        if (step == null || !step.enabled)
            return;

        if (model.IsCompleted(step.stepId))
            return;

        if (activeSteps.ContainsKey(step.stepId))
            return;

        ActiveStepContext context = new()
        {
            Step = step,
            Disposables = new TutorialDisposableGroup()
        };

        activeSteps.Add(step.stepId, context);

        view.PlayGuide(step);
        SubscribeCompleteCondition(context);
    }

    private void SubscribeCompleteCondition(ActiveStepContext context)
    {
        TutorialStepData step = context.Step;

        context.Disposables.Add(
            objectiveTracker.ObserveCompleteCondition(step)
                .Subscribe(_ => CompleteStep(step.stepId))
        );
    }

    private void CompleteStep(string stepId)
    {
        if (!activeSteps.TryGetValue(stepId, out ActiveStepContext context))
            return;

        TutorialStepData step = context.Step;

        view.StopGuide(step);

        context.Disposables.Dispose();
        activeSteps.Remove(stepId);

        model.MarkCompleted(stepId);
        objectiveTracker.NotifyStepCompleted(stepId);

        // Sequential Step은 NextStepId를 통해 다음 Step으로 이어간다.
        if (step.runMode == TutorialRunMode.Sequential && !string.IsNullOrEmpty(step.nextStepId))
            StartStep(step.nextStepId);
    }
}
