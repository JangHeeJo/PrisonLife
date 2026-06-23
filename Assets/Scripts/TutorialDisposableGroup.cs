using System;
using System.Collections.Generic;

/// <summary>
/// R3 Subscribe에서 반환되는 IDisposable을 Step 단위로 묶어서 해제하기 위한 작은 유틸리티입니다.
/// R3의 DisposableBag을 써도 되지만, 여기서는 구조가 명확하게 보이도록 직접 관리합니다.
/// </summary>
public sealed class TutorialDisposableGroup : IDisposable
{
    private readonly List<IDisposable> disposables = new();
    private bool disposed;

    public void Add(IDisposable disposable)
    {
        if (disposable == null)
            return;

        if (disposed)
        {
            disposable.Dispose();
            return;
        }

        disposables.Add(disposable);
    }
    public void Clear()
    {
        for (int i = 0; i < disposables.Count; i++)
            disposables[i]?.Dispose();

        disposables.Clear();
    }


    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        Clear();
    }
}
