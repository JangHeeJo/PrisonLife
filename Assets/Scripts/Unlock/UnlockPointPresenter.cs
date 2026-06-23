using System;
using UnityEngine;

/// <summary>
/// UnlockPoint, UnlockPointView, UnlockPointData를 연결하는 Presenter입니다.
///
/// 역할:
/// - UnlockPoint에 데이터 바인딩
/// - UnlockPointView에 아이콘/비용/fill 표시
/// - UnlockPoint 진행도 변경 이벤트를 받아 View 갱신
/// - UnlockPoint 완료 이벤트를 받아 View 숨김
///
/// 주의:
/// - 실제 해금 결과 실행은 아직 여기서 하지 않습니다.
///   다음 단계에서 UnlockResultExecutor를 붙일 예정입니다.
/// </summary>
public sealed class UnlockPointPresenter : IDisposable
{
    private readonly UnlockPointData data;
    private readonly UnlockPointSlot slot;
    private readonly UnlockPoint unlockPoint;
    private readonly UnlockPointView view;
    private readonly GameIconDatabase iconDatabase;
    private readonly Action<UnlockPointData> onUnlocked;

    private bool isDisposed;

    public UnlockPointData Data => data;
    public UnlockPointSlot Slot => slot;
    public UnlockPoint UnlockPoint => unlockPoint;
    public UnlockPointView View => view;

    public UnlockPointPresenter(
        UnlockPointData data,
        UnlockPointSlot slot,
        UnlockPointView view,
        GameIconDatabase iconDatabase,
        Action<UnlockPointData> onUnlocked = null)
    {
        this.data = data;
        this.slot = slot;
        this.view = view;
        this.iconDatabase = iconDatabase;
        this.onUnlocked = onUnlocked;

        unlockPoint = slot != null ? slot.UnlockPoint : null;
    }

    /// <summary>
    /// UnlockPoint와 View를 실제로 연결하고 화면에 표시합니다.
    /// </summary>
    public void Reveal()
    {
        if (isDisposed)
            return;

        if (data == null)
        {
            Debug.LogError("[UnlockPointPresenter] Data is null.");
            return;
        }

        if (slot == null)
        {
            Debug.LogError($"[UnlockPointPresenter] Slot is null. UnlockId: {data.unlockId}");
            return;
        }

        if (unlockPoint == null)
        {
            Debug.LogError($"[UnlockPointPresenter] UnlockPoint is null. SlotId: {slot.SlotId}");
            return;
        }

        if (view == null)
        {
            Debug.LogError($"[UnlockPointPresenter] View is null. UnlockId: {data.unlockId}");
            return;
        }

        // UnlockPoint 오브젝트가 꺼져 있을 수 있으므로 먼저 켭니다.
        slot.SetPointActive(true);

        // UnlockPoint 런타임 상태 초기화
        unlockPoint.Bind(data);

        // 이벤트 중복 방지 후 구독
        unlockPoint.ProgressChanged -= OnProgressChanged;
        unlockPoint.Unlocked -= OnUnlocked;

        unlockPoint.ProgressChanged += OnProgressChanged;
        unlockPoint.Unlocked += OnUnlocked;

        // IconId를 실제 Sprite로 변환
        Sprite iconSprite = iconDatabase != null
            ? iconDatabase.GetIcon(data.iconId)
            : null;

        // View에 데이터 반영
        view.Bind(data, iconSprite);

        // View를 Slot 위치에 표시
        view.Show(
            slot.UIAnchor,
            data.popScale,
            data.popDuration
        );

        // UnlockPoint 상호작용 가능 상태로 전환
        unlockPoint.Reveal();
    }

    /// <summary>
    /// UnlockPoint가 돈을 받을 때마다 View의 fill/text를 갱신합니다.
    /// </summary>
    private void OnProgressChanged(int current, int max)
    {
        if (view == null)
            return;

        view.SetProgress(current, max);
    }

    /// <summary>
    /// 비용을 모두 채워 해금이 완료되었을 때 호출됩니다.
    /// </summary>
    private void OnUnlocked(UnlockPoint completedPoint)
    {
        if (view != null)
            view.Hide();

        // 현재 단계에서는 결과 실행은 하지 않고,
        // Manager나 Executor가 처리할 수 있도록 callback만 전달합니다.
        onUnlocked?.Invoke(data);
    }

    /// <summary>
    /// Presenter 정리.
    /// View를 숨기고 이벤트 구독을 해제합니다.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;

        if (unlockPoint != null)
        {
            unlockPoint.ProgressChanged -= OnProgressChanged;
            unlockPoint.Unlocked -= OnUnlocked;
            unlockPoint.HidePoint();
        }

        if (view != null)
            view.Hide();
    }
}