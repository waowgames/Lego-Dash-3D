using System;
using System.Collections;
using UnityEngine;

public class MockAdService : MonoBehaviour, IAdService
{
    [SerializeField] private Vector2 waitTimeRange = new Vector2(2f, 3f);
    [Range(0f, 1f)][SerializeField] private float successChance = 0.9f;

    public bool IsAdReady(string placement)
    {
        return true;
    }

    public void ShowRewarded(string placement, Action onSuccess, Action onFail)
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning("MockAdService: Component disabled, cannot show ad.");
            onFail?.Invoke();
            return;
        }

        StartCoroutine(SimulateRewardedAd(placement, onSuccess, onFail));
    }

    private IEnumerator SimulateRewardedAd(string placement, Action onSuccess, Action onFail)
    {
        float waitTime = UnityEngine.Random.Range(waitTimeRange.x, waitTimeRange.y);
        Debug.Log($"MockAdService: Showing rewarded ad for placement '{placement}'.");
        yield return new WaitForSeconds(waitTime);

        bool success = UnityEngine.Random.value <= successChance;
        Debug.Log(success
            ? $"MockAdService: Rewarded ad for '{placement}' finished successfully."
            : $"MockAdService: Rewarded ad for '{placement}' failed or was skipped.");

        if (success)
        {
            onSuccess?.Invoke();
        }
        else
        {
            onFail?.Invoke();
        }
    }
}
