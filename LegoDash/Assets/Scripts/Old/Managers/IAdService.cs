using System;

public interface IAdService
{
    bool IsAdReady(string placement);
    void ShowRewarded(string placement, Action onSuccess, Action onFail);
}
