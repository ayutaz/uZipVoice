using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using uZipVoice.Core;
using uZipVoice.Samples;

namespace uZipVoice.Editor
{
    /// <summary>
    /// TTS サンプルシーン作成ユーティリティ
    /// </summary>
    public static class TTSSampleSceneCreator
    {
        [MenuItem("uZipVoice/Create TTS Sample Scene")]
        public static void CreateSampleScene()
        {
            // 新しいシーンを作成
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Main Camera設定
            var mainCamera = GameObject.Find("Main Camera");
            if (mainCamera != null)
            {
                mainCamera.transform.position = new Vector3(0, 1, -10);
            }

            // ZipVoiceManager オブジェクトを作成
            var managerGO = new GameObject("ZipVoiceManager");
            var manager = managerGO.AddComponent<ZipVoiceManager>();

            // AudioSource を追加
            var audioSource = managerGO.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            // UI Canvas を作成
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // EventSystem を作成
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<StandaloneInputModule>();

            // Panel (背景)
            var panelGO = CreateUIElement<Image>("Panel", canvasGO.transform);
            var panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(20, 20);
            panelRect.offsetMax = new Vector2(-20, -20);
            panelGO.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            // Title
            var titleGO = CreateTextElement("Title", panelGO.transform, "uZipVoice TTS Sample", 36);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -20);
            titleRect.sizeDelta = new Vector2(0, 50);

            // Text Input Label
            var textLabelGO = CreateTextElement("TextInputLabel", panelGO.transform, "Text to Synthesize:", 18);
            var textLabelRect = textLabelGO.GetComponent<RectTransform>();
            textLabelRect.anchorMin = new Vector2(0, 1);
            textLabelRect.anchorMax = new Vector2(1, 1);
            textLabelRect.pivot = new Vector2(0, 1);
            textLabelRect.anchoredPosition = new Vector2(20, -90);
            textLabelRect.sizeDelta = new Vector2(-40, 30);
            textLabelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Left;

            // Text Input Field
            var textInputGO = CreateInputField("TextInput", panelGO.transform, "Enter text here...");
            var textInputRect = textInputGO.GetComponent<RectTransform>();
            textInputRect.anchorMin = new Vector2(0, 1);
            textInputRect.anchorMax = new Vector2(1, 1);
            textInputRect.pivot = new Vector2(0.5f, 1);
            textInputRect.anchoredPosition = new Vector2(0, -130);
            textInputRect.sizeDelta = new Vector2(-40, 80);
            var textInput = textInputGO.GetComponent<TMP_InputField>();
            textInput.text = "Hello, this is a test of the text to speech system.";

            // Prompt Text Label
            var promptLabelGO = CreateTextElement("PromptTextLabel", panelGO.transform, "Prompt Text (for voice cloning):", 18);
            var promptLabelRect = promptLabelGO.GetComponent<RectTransform>();
            promptLabelRect.anchorMin = new Vector2(0, 1);
            promptLabelRect.anchorMax = new Vector2(1, 1);
            promptLabelRect.pivot = new Vector2(0, 1);
            promptLabelRect.anchoredPosition = new Vector2(20, -230);
            promptLabelRect.sizeDelta = new Vector2(-40, 30);
            promptLabelGO.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Left;

            // Prompt Text Input Field
            var promptInputGO = CreateInputField("PromptTextInput", panelGO.transform, "Enter prompt text...");
            var promptInputRect = promptInputGO.GetComponent<RectTransform>();
            promptInputRect.anchorMin = new Vector2(0, 1);
            promptInputRect.anchorMax = new Vector2(1, 1);
            promptInputRect.pivot = new Vector2(0.5f, 1);
            promptInputRect.anchoredPosition = new Vector2(0, -270);
            promptInputRect.sizeDelta = new Vector2(-40, 50);
            var promptInput = promptInputGO.GetComponent<TMP_InputField>();
            promptInput.text = "This is a sample prompt text.";

            // Options Panel
            var optionsPanelGO = CreateUIElement<Image>("OptionsPanel", panelGO.transform);
            var optionsPanelRect = optionsPanelGO.GetComponent<RectTransform>();
            optionsPanelRect.anchorMin = new Vector2(0, 1);
            optionsPanelRect.anchorMax = new Vector2(1, 1);
            optionsPanelRect.pivot = new Vector2(0.5f, 1);
            optionsPanelRect.anchoredPosition = new Vector2(0, -340);
            optionsPanelRect.sizeDelta = new Vector2(-40, 120);
            optionsPanelGO.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);

            // Steps Slider
            var (stepsSliderGO, stepsSlider, stepsLabel) = CreateSliderWithLabel("StepsSlider", optionsPanelGO.transform, "Steps: 16", 4, 32, 16);
            var stepsRect = stepsSliderGO.GetComponent<RectTransform>();
            stepsRect.anchorMin = new Vector2(0, 1);
            stepsRect.anchorMax = new Vector2(0.33f, 1);
            stepsRect.pivot = new Vector2(0.5f, 1);
            stepsRect.anchoredPosition = new Vector2(0, -10);
            stepsRect.sizeDelta = new Vector2(-20, 100);

            // Speed Slider
            var (speedSliderGO, speedSlider, speedLabel) = CreateSliderWithLabel("SpeedSlider", optionsPanelGO.transform, "Speed: 1.00x", 0.5f, 2f, 1f);
            var speedRect = speedSliderGO.GetComponent<RectTransform>();
            speedRect.anchorMin = new Vector2(0.33f, 1);
            speedRect.anchorMax = new Vector2(0.66f, 1);
            speedRect.pivot = new Vector2(0.5f, 1);
            speedRect.anchoredPosition = new Vector2(0, -10);
            speedRect.sizeDelta = new Vector2(-20, 100);

            // Guidance Slider
            var (guidanceSliderGO, guidanceSlider, guidanceLabel) = CreateSliderWithLabel("GuidanceSlider", optionsPanelGO.transform, "Guidance: 1.00", 0f, 3f, 1f);
            var guidanceRect = guidanceSliderGO.GetComponent<RectTransform>();
            guidanceRect.anchorMin = new Vector2(0.66f, 1);
            guidanceRect.anchorMax = new Vector2(1f, 1);
            guidanceRect.pivot = new Vector2(0.5f, 1);
            guidanceRect.anchoredPosition = new Vector2(0, -10);
            guidanceRect.sizeDelta = new Vector2(-20, 100);

            // Buttons Panel
            var buttonsPanelGO = CreateUIElement<RectTransform>("ButtonsPanel", panelGO.transform);
            var buttonsPanelRect = buttonsPanelGO.GetComponent<RectTransform>();
            buttonsPanelRect.anchorMin = new Vector2(0, 1);
            buttonsPanelRect.anchorMax = new Vector2(1, 1);
            buttonsPanelRect.pivot = new Vector2(0.5f, 1);
            buttonsPanelRect.anchoredPosition = new Vector2(0, -480);
            buttonsPanelRect.sizeDelta = new Vector2(-40, 60);
            var buttonsLayout = buttonsPanelGO.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = 20;
            buttonsLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonsLayout.childControlWidth = true;
            buttonsLayout.childControlHeight = true;
            buttonsLayout.childForceExpandWidth = true;
            buttonsLayout.childForceExpandHeight = true;

            // Synthesize Button
            var synthesizeButtonGO = CreateButton("SynthesizeButton", buttonsPanelGO.transform, "Synthesize");
            var synthesizeButton = synthesizeButtonGO.GetComponent<Button>();
            synthesizeButtonGO.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f, 1f);

            // Play Button
            var playButtonGO = CreateButton("PlayButton", buttonsPanelGO.transform, "Play");
            var playButton = playButtonGO.GetComponent<Button>();
            playButtonGO.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.8f, 1f);

            // Stop Button
            var stopButtonGO = CreateButton("StopButton", buttonsPanelGO.transform, "Stop");
            var stopButton = stopButtonGO.GetComponent<Button>();
            stopButtonGO.GetComponent<Image>().color = new Color(0.8f, 0.3f, 0.2f, 1f);

            // Status Text
            var statusGO = CreateTextElement("StatusText", panelGO.transform, "Initializing...", 20);
            var statusRect = statusGO.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 0);
            statusRect.anchorMax = new Vector2(1, 0);
            statusRect.pivot = new Vector2(0.5f, 0);
            statusRect.anchoredPosition = new Vector2(0, 20);
            statusRect.sizeDelta = new Vector2(-40, 40);
            var statusText = statusGO.GetComponent<TMP_Text>();

            // TTSSampleController を作成
            var controllerGO = new GameObject("TTSSampleController");
            var controller = controllerGO.AddComponent<TTSSampleController>();

            // SerializedObjectで参照を設定
            var so = new SerializedObject(controller);
            so.FindProperty("_zipVoiceManager").objectReferenceValue = manager;
            so.FindProperty("_audioSource").objectReferenceValue = audioSource;
            so.FindProperty("_textInput").objectReferenceValue = textInput;
            so.FindProperty("_promptTextInput").objectReferenceValue = promptInput;
            so.FindProperty("_synthesizeButton").objectReferenceValue = synthesizeButton;
            so.FindProperty("_playButton").objectReferenceValue = playButton;
            so.FindProperty("_stopButton").objectReferenceValue = stopButton;
            so.FindProperty("_stepsSlider").objectReferenceValue = stepsSlider;
            so.FindProperty("_stepsLabel").objectReferenceValue = stepsLabel;
            so.FindProperty("_speedSlider").objectReferenceValue = speedSlider;
            so.FindProperty("_speedLabel").objectReferenceValue = speedLabel;
            so.FindProperty("_guidanceSlider").objectReferenceValue = guidanceSlider;
            so.FindProperty("_guidanceLabel").objectReferenceValue = guidanceLabel;
            so.FindProperty("_statusText").objectReferenceValue = statusText;
            so.ApplyModifiedProperties();

            // シーンを保存
            string scenePath = "Assets/uZipVoice/Samples/TTSSample.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);

            Debug.Log($"[TTSSampleSceneCreator] Scene created and saved to: {scenePath}");
            EditorUtility.DisplayDialog("Success", $"Sample scene created at:\n{scenePath}", "OK");
        }

        private static GameObject CreateUIElement<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<T>();
            return go;
        }

        private static GameObject CreateTextElement(string name, Transform parent, string text, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return go;
        }

        private static GameObject CreateInputField(string name, Transform parent, string placeholder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 1f);

            var inputField = go.AddComponent<TMP_InputField>();

            // Text Area
            var textAreaGO = new GameObject("Text Area");
            textAreaGO.transform.SetParent(go.transform, false);
            var textAreaRect = textAreaGO.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 5);
            textAreaRect.offsetMax = new Vector2(-10, -5);
            textAreaGO.AddComponent<RectMask2D>();

            // Placeholder
            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(textAreaGO.transform, false);
            var placeholderText = placeholderGO.AddComponent<TextMeshProUGUI>();
            placeholderText.text = placeholder;
            placeholderText.fontSize = 16;
            placeholderText.fontStyle = FontStyles.Italic;
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            placeholderText.alignment = TextAlignmentOptions.TopLeft;
            var placeholderRect = placeholderGO.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;

            // Text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(textAreaGO.transform, false);
            var textText = textGO.AddComponent<TextMeshProUGUI>();
            textText.fontSize = 16;
            textText.color = Color.white;
            textText.alignment = TextAlignmentOptions.TopLeft;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            inputField.textViewport = textAreaRect;
            inputField.textComponent = textText;
            inputField.placeholder = placeholderText;

            return go;
        }

        private static GameObject CreateButton(string name, Transform parent, string text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 20;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return go;
        }

        private static (GameObject, Slider, TMP_Text) CreateSliderWithLabel(string name, Transform parent, string labelText, float min, float max, float value)
        {
            var containerGO = new GameObject(name);
            containerGO.transform.SetParent(parent, false);
            containerGO.AddComponent<RectTransform>();

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(containerGO.transform, false);
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.fontSize = 16;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 1);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.pivot = new Vector2(0.5f, 1);
            labelRect.anchoredPosition = new Vector2(0, 0);
            labelRect.sizeDelta = new Vector2(0, 30);

            // Slider
            var sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(containerGO.transform, false);

            var sliderRect = sliderGO.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0, 0);
            sliderRect.anchorMax = new Vector2(1, 1);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.offsetMin = new Vector2(10, 10);
            sliderRect.offsetMax = new Vector2(-10, -40);

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(sliderGO.transform, false);
            var bgImage = bgGO.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Fill Area
            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-5, 0);

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillImage = fillGO.AddComponent<Image>();
            fillImage.color = new Color(0.4f, 0.7f, 1f, 1f);
            var fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // Handle
            var handleAreaGO = new GameObject("Handle Slide Area");
            handleAreaGO.transform.SetParent(sliderGO.transform, false);
            var handleAreaRect = handleAreaGO.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var handleImage = handleGO.AddComponent<Image>();
            handleImage.color = Color.white;
            var handleRect = handleGO.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0, 0);
            handleRect.anchorMax = new Vector2(0, 1);
            handleRect.sizeDelta = new Vector2(20, 0);
            handleRect.anchoredPosition = Vector2.zero;

            var slider = sliderGO.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            return (containerGO, slider, label);
        }
    }
}
