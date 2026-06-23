using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬에서 Tutorial MVP를 조립하는 진입점입니다.
/// TSV 로드, Model/Tracker/Presenter 생성, 시작 처리만 담당합니다.
/// </summary>
public sealed class TutorialManager : MonoBehaviour
{
    [Header("Table")]
    [SerializeField] private TextAsset tutorialTable;

    [Header("Start")]
    [SerializeField] private string startStepId = "T_ANCHOR_001";

    [Header("MVP")]
    [SerializeField] private TutorialView tutorialView;

    [Header("Signals")]
    [SerializeField] private GameStateSignals gameStateSignals;

    private TutorialPresenter presenter;

    private void Awake()
    {
        if (gameStateSignals == null)
            gameStateSignals = GameStateSignals.Instance;

        List<TutorialStepData> steps = TutorialTableLoader.LoadFromTextAsset(tutorialTable);

        TutorialModel model = new(steps);
        ObjectiveTracker objectiveTracker = new(gameStateSignals);

        presenter = new TutorialPresenter(
            model,
            tutorialView,
            objectiveTracker
        );
    }

    private void Start()
    {
        presenter?.StartTutorial(startStepId);
    }

    private void OnDestroy()
    {
        presenter?.Dispose();
    }
}
