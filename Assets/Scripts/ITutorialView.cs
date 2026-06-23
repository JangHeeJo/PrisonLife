/// <summary>
/// Presenter가 View에 요청할 수 있는 기능입니다.
/// Presenter는 실제 Unity 오브젝트 구조를 모르고, Step 데이터만 넘깁니다.
/// </summary>
public interface ITutorialView
{
    void PlayGuide(TutorialStepData step);
    void StopGuide(TutorialStepData step);
    void HideAllGuides();
}
