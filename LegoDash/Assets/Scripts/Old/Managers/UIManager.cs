using System;
using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class UIManager : SingletonMonoBehaviour<UIManager>
{
    private int _score;
    public int Score => _score;

    [SerializeField] private TextMeshProUGUI scoreTxt;

    [SerializeField] private int scoreMultiplier=1;

    public event Action<int> ScoreChanged;

    protected override void Awake()
    {
        base.Awake();
        AwakeInitializer();
    }

    private void AwakeInitializer()
    {
        if (!PlayerPrefs.HasKey("score"))
            PlayerPrefs.SetInt("score", 0);

        _score = PlayerPrefs.GetInt("score");
        scoreTxt.text = _score.ToString();

        ScoreChanged?.Invoke(_score);
    }

    public void ScoreAdd(int a = 1)
    {
        if (_score > 0)
            _score += a * scoreMultiplier;
        else _score += a;

        PlayerPrefs.SetInt("score", _score);
        scoreTxt.text = _score.ToString();

        ScoreChanged?.Invoke(_score);
    }


}
