using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DebugLevelControls : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private Vector2 panelAnchorOffset = new Vector2(20f, -20f);
    [SerializeField] private Vector2 panelSize = new Vector2(260f, 200f);
    [SerializeField] private float buttonHeight = 48f;
    [SerializeField] private float toggleHeight = 48f;

    private Toggle _adsToggle;
    private Text _adsToggleLabel;

    private void Start()
    {
        BuildUi();
        SyncToggleState();
    }

    private void BuildUi()
    {
        var canvas = ResolveCanvas();
        if (canvas == null)
        {
            return;
        }

        var panel = new GameObject("Debug Controls Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = panelAnchorOffset;
        panelRect.sizeDelta = panelSize;

        var panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.6f);

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 8f;
        layout.padding = new RectOffset(10, 10, 10, 10);

        var fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        _adsToggle = CreateToggle(panel.transform, "Ads: On", toggleHeight, OnAdsToggleChanged, out _adsToggleLabel);
        CreateButton(panel.transform, "Previous Level", buttonHeight, GoToPreviousLevel);
        CreateButton(panel.transform, "Next Level", buttonHeight, GoToNextLevel);
    }

    private Canvas ResolveCanvas()
    {
        var mainCanvasObject = GameObject.Find("Main UI Canvas");
        if (mainCanvasObject != null)
        {
            var mainCanvas = mainCanvasObject.GetComponent<Canvas>();
            if (mainCanvas != null)
            {
                return mainCanvas;
            }
        }

        return FindAnyObjectByType<Canvas>();
    }

    private Toggle CreateToggle(Transform parent, string label, float height, UnityEngine.Events.UnityAction<bool> onValueChanged, out Text labelText)
    {
        var toggleObject = new GameObject("Debug Ads Toggle", typeof(RectTransform), typeof(Toggle));
        toggleObject.transform.SetParent(parent, false);

        var toggleRect = toggleObject.GetComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(0f, height);

        var toggle = toggleObject.GetComponent<Toggle>();
        toggle.isOn = true;
        toggle.transition = Selectable.Transition.ColorTint;
        toggle.targetGraphic = CreateImageChild(toggleObject.transform, "Background", new Color(0.2f, 0.2f, 0.2f, 0.9f));
        toggle.graphic = CreateImageChild(toggle.targetGraphic.transform, "Checkmark", new Color(0.1f, 0.8f, 0.2f, 0.9f));

        var backgroundRect = toggle.targetGraphic.rectTransform;
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.pivot = new Vector2(0f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(32f, 32f);
        backgroundRect.anchoredPosition = new Vector2(0f, 0f);

        var checkmarkRect = toggle.graphic.rectTransform;
        checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
        checkmarkRect.sizeDelta = new Vector2(18f, 18f);
        checkmarkRect.anchoredPosition = Vector2.zero;

        var labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(toggleObject.transform, false);
        labelText = labelObject.AddComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        labelText.fontSize = 18;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.text = label;

        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(40f, 0f);
        labelRect.offsetMax = new Vector2(0f, 0f);

        toggle.onValueChanged.AddListener(onValueChanged);

        var layoutElement = toggleObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = height;

        return toggle;
    }

    private Button CreateButton(Transform parent, string label, float height, UnityEngine.Events.UnityAction onClick)
    {
        var buttonObject = new GameObject($"Debug {label} Button", typeof(RectTransform), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        var buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(0f, height);

        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.25f, 0.25f, 0.25f, 0.9f);

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        var labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);
        var labelText = labelObject.AddComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        labelText.fontSize = 18;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.text = label;

        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = height;

        return button;
    }

    private Image CreateImageChild(Transform parent, string name, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private void OnAdsToggleChanged(bool isOn)
    {
        SetAdsEnabled(isOn);
        UpdateAdsLabel(isOn);
    }

    private void SyncToggleState()
    {
        bool adsEnabled = true;
        var services = FindAdServices();
        if (services.Count > 0)
        {
            foreach (var service in services)
            {
                if (!service.isActiveAndEnabled)
                {
                    adsEnabled = false;
                    break;
                }
            }
        }

        if (_adsToggle != null)
        {
            _adsToggle.SetIsOnWithoutNotify(adsEnabled);
            UpdateAdsLabel(adsEnabled);
        }
    }

    private void SetAdsEnabled(bool enabled)
    {
        foreach (var service in FindAdServices())
        {
            service.enabled = enabled;
        }
    }

    private List<MonoBehaviour> FindAdServices()
    {
        var services = new List<MonoBehaviour>();
        var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var behaviour in behaviours)
        {
            if (behaviour is IAdService)
            {
                services.Add(behaviour);
            }
        }

        return services;
    }

    private void UpdateAdsLabel(bool adsEnabled)
    {
        if (_adsToggleLabel != null)
        {
            _adsToggleLabel.text = adsEnabled ? "Ads: On" : "Ads: Off";
        }
    }

    private void GoToNextLevel()
    {
        if (LevelMissionManager.Instance != null)
        {
            LevelMissionManager.Instance.AdvanceToNextLevel();
            return;
        }

        LevelManager.Instance?.NextLevel();
    }

    private void GoToPreviousLevel()
    {
        if (LevelMissionManager.Instance != null)
        {
            LevelMissionManager.Instance.ReturnToPreviousLevel();
        }
    }
}
