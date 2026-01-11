using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using uZipVoice.Core;

namespace uZipVoice.Samples
{
    /// <summary>
    /// TTS サンプルシーン用UIコントローラー
    /// </summary>
    public class TTSSampleController : MonoBehaviour
    {
        [Header("ZipVoice")]
        [SerializeField] private ZipVoiceManager _zipVoiceManager;

        [Header("UI - Input")]
        [SerializeField] private TMP_InputField _textInput;
        [SerializeField] private TMP_InputField _promptTextInput;
        [SerializeField] private Button _synthesizeButton;

        [Header("UI - Options")]
        [SerializeField] private Slider _stepsSlider;
        [SerializeField] private TMP_Text _stepsLabel;
        [SerializeField] private Slider _speedSlider;
        [SerializeField] private TMP_Text _speedLabel;
        [SerializeField] private Slider _guidanceSlider;
        [SerializeField] private TMP_Text _guidanceLabel;

        [Header("UI - Status")]
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _stopButton;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _promptAudioClip;

        private AudioClip _synthesizedClip;
        private bool _isInitialized;

        private void Start()
        {
            SetupUI();
            InitializeAsync().Forget();
        }

        private void SetupUI()
        {
            // ボタンイベント
            if (_synthesizeButton != null)
            {
                _synthesizeButton.onClick.AddListener(() => OnSynthesizeClickedAsync().Forget());
                _synthesizeButton.interactable = false;
            }

            if (_playButton != null)
            {
                _playButton.onClick.AddListener(OnPlayClicked);
                _playButton.interactable = false;
            }

            if (_stopButton != null)
            {
                _stopButton.onClick.AddListener(OnStopClicked);
                _stopButton.interactable = false;
            }

            // スライダーイベント
            if (_stepsSlider != null)
            {
                _stepsSlider.onValueChanged.AddListener(OnStepsChanged);
                _stepsSlider.minValue = 4;
                _stepsSlider.maxValue = 32;
                _stepsSlider.wholeNumbers = true;
                _stepsSlider.value = 16;
                OnStepsChanged(_stepsSlider.value);
            }

            if (_speedSlider != null)
            {
                _speedSlider.onValueChanged.AddListener(OnSpeedChanged);
                _speedSlider.minValue = 0.5f;
                _speedSlider.maxValue = 2.0f;
                _speedSlider.value = 1.0f;
                OnSpeedChanged(_speedSlider.value);
            }

            if (_guidanceSlider != null)
            {
                _guidanceSlider.onValueChanged.AddListener(OnGuidanceChanged);
                _guidanceSlider.minValue = 0f;
                _guidanceSlider.maxValue = 3.0f;
                _guidanceSlider.value = 1.0f;
                OnGuidanceChanged(_guidanceSlider.value);
            }

            // デフォルトテキスト
            if (_textInput != null && string.IsNullOrEmpty(_textInput.text))
            {
                _textInput.text = "Hello, this is a test of the text to speech system.";
            }

            if (_promptTextInput != null && string.IsNullOrEmpty(_promptTextInput.text))
            {
                _promptTextInput.text = "This is a sample prompt text.";
            }

            UpdateStatus("Initializing...");
        }

        private async UniTask InitializeAsync()
        {
            try
            {
                if (_zipVoiceManager == null)
                {
                    UpdateStatus("Error: ZipVoiceManager not assigned");
                    return;
                }

                await _zipVoiceManager.InitializeAsync();
                _isInitialized = true;

                if (_synthesizeButton != null)
                {
                    _synthesizeButton.interactable = true;
                }

                UpdateStatus("Ready. Enter text and click Synthesize.");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Initialization failed: {ex.Message}");
                Debug.LogError($"[TTSSampleController] Initialization failed: {ex}");
            }
        }

        private async UniTask OnSynthesizeClickedAsync()
        {
            if (!_isInitialized || _zipVoiceManager.IsProcessing)
            {
                return;
            }

            string text = _textInput?.text ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                UpdateStatus("Please enter text to synthesize.");
                return;
            }

            try
            {
                // UIを無効化
                SetUIInteractable(false);
                UpdateStatus("Synthesizing...");

                // オプションを設定
                var options = new SynthesisOptions
                {
                    NumSteps = _stepsSlider != null ? (int)_stepsSlider.value : 16,
                    Speed = _speedSlider != null ? _speedSlider.value : 1.0f,
                    GuidanceScale = _guidanceSlider != null ? _guidanceSlider.value : 1.0f
                };

                string promptText = _promptTextInput?.text ?? "";

                // 合成実行
                _synthesizedClip = await _zipVoiceManager.SynthesizeAsync(
                    text,
                    _promptAudioClip,
                    promptText,
                    options
                );

                if (_synthesizedClip != null)
                {
                    UpdateStatus($"Synthesis complete! Duration: {_synthesizedClip.length:F2}s");

                    if (_playButton != null)
                    {
                        _playButton.interactable = true;
                    }
                }
                else
                {
                    UpdateStatus("Synthesis failed: No audio generated.");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Synthesis error: {ex.Message}");
                Debug.LogError($"[TTSSampleController] Synthesis error: {ex}");
            }
            finally
            {
                SetUIInteractable(true);
            }
        }

        private void OnPlayClicked()
        {
            if (_synthesizedClip == null || _audioSource == null)
            {
                Debug.LogWarning($"[TTSSampleController] Cannot play: clip={_synthesizedClip}, audioSource={_audioSource}");
                return;
            }

            // AudioClipの状態をログ出力
            Debug.Log($"[TTSSampleController] AudioClip: samples={_synthesizedClip.samples}, channels={_synthesizedClip.channels}, frequency={_synthesizedClip.frequency}, length={_synthesizedClip.length}s");

            // 波形データの複数箇所を確認
            float[] allData = new float[_synthesizedClip.samples];
            _synthesizedClip.GetData(allData, 0);

            // 全体の統計
            float sum = 0, min = float.MaxValue, max = float.MinValue;
            int positiveCount = 0, negativeCount = 0, nearZeroCount = 0;
            for (int i = 0; i < allData.Length; i++)
            {
                sum += allData[i];
                min = Mathf.Min(min, allData[i]);
                max = Mathf.Max(max, allData[i]);
                if (allData[i] > 0.01f) positiveCount++;
                else if (allData[i] < -0.01f) negativeCount++;
                else nearZeroCount++;
            }
            float mean = sum / allData.Length;
            Debug.Log($"[TTSSampleController] Full waveform: min={min:F4}, max={max:F4}, mean={mean:F4}");
            Debug.Log($"[TTSSampleController] Sample distribution: positive={positiveCount}, negative={negativeCount}, nearZero={nearZeroCount}");

            // 異なる位置のサンプルを確認
            int[] checkPositions = { 0, allData.Length / 4, allData.Length / 2, allData.Length * 3 / 4, allData.Length - 1000 };
            foreach (int pos in checkPositions)
            {
                if (pos >= 0 && pos + 100 < allData.Length)
                {
                    float localMin = float.MaxValue, localMax = float.MinValue;
                    for (int i = pos; i < pos + 100; i++)
                    {
                        localMin = Mathf.Min(localMin, allData[i]);
                        localMax = Mathf.Max(localMax, allData[i]);
                    }
                    Debug.Log($"[TTSSampleController] Samples at {pos}: min={localMin:F4}, max={localMax:F4}");
                }
            }

            // AudioSourceの状態をログ出力
            Debug.Log($"[TTSSampleController] AudioSource: volume={_audioSource.volume}, mute={_audioSource.mute}, enabled={_audioSource.enabled}");

            _audioSource.clip = _synthesizedClip;
            _audioSource.Play();

            Debug.Log($"[TTSSampleController] AudioSource.isPlaying={_audioSource.isPlaying}");

            if (_stopButton != null)
            {
                _stopButton.interactable = true;
            }

            UpdateStatus("Playing...");
        }

        private void OnStopClicked()
        {
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
                UpdateStatus("Stopped.");
            }
        }

        private void OnStepsChanged(float value)
        {
            if (_stepsLabel != null)
            {
                _stepsLabel.text = $"Steps: {(int)value}";
            }
        }

        private void OnSpeedChanged(float value)
        {
            if (_speedLabel != null)
            {
                _speedLabel.text = $"Speed: {value:F2}x";
            }
        }

        private void OnGuidanceChanged(float value)
        {
            if (_guidanceLabel != null)
            {
                _guidanceLabel.text = $"Guidance: {value:F2}";
            }
        }

        private void SetUIInteractable(bool interactable)
        {
            if (_synthesizeButton != null)
            {
                _synthesizeButton.interactable = interactable && _isInitialized;
            }

            if (_textInput != null)
            {
                _textInput.interactable = interactable;
            }

            if (_promptTextInput != null)
            {
                _promptTextInput.interactable = interactable;
            }

            if (_stepsSlider != null)
            {
                _stepsSlider.interactable = interactable;
            }

            if (_speedSlider != null)
            {
                _speedSlider.interactable = interactable;
            }

            if (_guidanceSlider != null)
            {
                _guidanceSlider.interactable = interactable;
            }
        }

        private void UpdateStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }

            Debug.Log($"[TTSSampleController] {message}");
        }

        private void Update()
        {
            // 再生終了を検知
            if (_audioSource != null && !_audioSource.isPlaying && _stopButton != null && _stopButton.interactable)
            {
                _stopButton.interactable = false;
                if (_synthesizedClip != null)
                {
                    UpdateStatus("Ready. Enter text and click Synthesize.");
                }
            }
        }

        private void OnDestroy()
        {
            if (_synthesizedClip != null)
            {
                Destroy(_synthesizedClip);
                _synthesizedClip = null;
            }
        }
    }
}
