using UnityEngine;

/// <summary>
/// 튜토리얼/목표 추적에서 참조 가능한 씬 오브젝트입니다.
/// Excel의 TargetId와 이 값이 일치해야 합니다.
/// </summary>
public sealed class TutorialTarget : MonoBehaviour
{
    [SerializeField] private string targetId;

    public string TargetId => targetId;
    public Transform TargetTransform => transform;
}
