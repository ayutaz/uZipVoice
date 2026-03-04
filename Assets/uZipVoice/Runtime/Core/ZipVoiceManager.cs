using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Unity.InferenceEngine;
using UnityEngine;
using uZipVoice.Audio;
using uZipVoice.Inference;
using uZipVoice.Tokenizer;

namespace uZipVoice.Core
{
    /// <summary>
    /// ZipVoice メインAPI
    /// 音声合成の統合管理
    /// </summary>
    public class ZipVoiceManager : MonoBehaviour, IDisposable
    {
        [Header("Model Assets")]
        [Tooltip("text_encoder.onnx")]
        public ModelAsset TextEncoderModel;

        [Tooltip("fm_decoder.onnx")]
        public ModelAsset FMDecoderModel;

        [Tooltip("vocos_opset15.onnx")]
        public ModelAsset VocosModel;

        [Header("Resources")]
        [Tooltip("tokens.txt")]
        public TextAsset TokensAsset;

        [Header("Settings")]
        [Tooltip("設定（nullの場合はデフォルト値を使用）")]
        public ZipVoiceConfig Config;

        [Tooltip("推論バックエンド")]
        public BackendType Backend = BackendType.GPUCompute;

        // コンポーネント
        private TokenMap _tokenMap;
        private ITokenizer _tokenizer;
        private EspeakTokenizer _espeakTokenizer;
        private DotNetG2PTokenizer _g2pTokenizer;
        private TextEncoder _textEncoder;
        private FMDecoder _fmDecoder;
        private Vocos _vocos;
        private ISTFTProcessor _istftProcessor;
        private FeatureExtractor _featureExtractor;

        private bool _isInitialized;
        private bool _isProcessing;
        private bool _isDisposed;

        // メル特徴量のスケール係数（Pythonと同じ値）
        private const float FeatScale = 0.1f;

        /// <summary>
        /// 初期化済みかどうか
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 処理中かどうか
        /// </summary>
        public bool IsProcessing => _isProcessing;

        /// <summary>
        /// 言語を切り替え
        /// </summary>
        /// <param name="language">切り替え先言語</param>
        public void SetLanguage(Language language)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("ZipVoiceManager is not initialized. Call InitializeAsync() first.");

            if (language == Language.Japanese)
            {
                if (_g2pTokenizer == null)
                {
                    _g2pTokenizer = new DotNetG2PTokenizer(_tokenMap);
                    string dictPath = Path.Combine(Application.streamingAssetsPath, "naist-jdic");
                    _g2pTokenizer.Initialize(dictPath);
                }
                _tokenizer = _g2pTokenizer;
            }
            else
            {
                if (_espeakTokenizer == null)
                {
                    _espeakTokenizer = new EspeakTokenizer(_tokenMap);
                    string voice = Config != null ? Config.Voice : "en-us";
                    _espeakTokenizer.Voice = voice;
                    string espeakDataPath = Path.Combine(Application.streamingAssetsPath, "espeak-ng-data");
                    _espeakTokenizer.Initialize(espeakDataPath);
                }
                _tokenizer = _espeakTokenizer;
            }
            Debug.Log($"[ZipVoiceManager] Language set to {language}");
        }

        /// <summary>
        /// 初期化
        /// </summary>
        public async UniTask InitializeAsync()
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[ZipVoiceManager] Already initialized");
                return;
            }

            try
            {
                Debug.Log("[ZipVoiceManager] Initializing...");

                // 設定を取得（なければデフォルト）
                int sampleRate = Config != null ? Config.SampleRate : 24000;
                int nFft = Config != null ? Config.NFft : 1024;
                int hopLength = Config != null ? Config.HopLength : 256;
                int nMels = Config != null ? Config.NMels : 100;
                string voice = Config != null ? Config.Voice : "en-us";

                // TokenMapを初期化
                _tokenMap = new TokenMap();
                if (TokensAsset != null)
                {
                    _tokenMap.LoadFromTextAsset(TokensAsset);
                    Debug.Log($"[ZipVoiceManager] TokenMap loaded: {_tokenMap.Count} tokens");
                }
                else
                {
                    throw new InvalidOperationException("TokensAsset is not assigned");
                }

                // Tokenizerを初期化
                Language language = Config != null ? Config.Language : Language.English;

                if (language == Language.Japanese)
                {
                    _g2pTokenizer = new DotNetG2PTokenizer(_tokenMap);
                    string dictPath = Path.Combine(Application.streamingAssetsPath, "naist-jdic");
                    _g2pTokenizer.Initialize(dictPath);
                    _tokenizer = _g2pTokenizer;
                    Debug.Log("[ZipVoiceManager] DotNetG2PTokenizer initialized");
                }
                else
                {
                    _espeakTokenizer = new EspeakTokenizer(_tokenMap);
                    _espeakTokenizer.Voice = voice;
                    string espeakDataPath = Path.Combine(Application.streamingAssetsPath, "espeak-ng-data");
                    if (Directory.Exists(espeakDataPath))
                    {
                        _espeakTokenizer.Initialize(espeakDataPath);
                        Debug.Log("[ZipVoiceManager] EspeakTokenizer initialized");
                    }
                    else
                    {
                        Debug.LogWarning($"[ZipVoiceManager] espeak-ng-data not found at {espeakDataPath}");
                    }
                    _tokenizer = _espeakTokenizer;
                }

                // TextEncoderを初期化
                if (TextEncoderModel != null)
                {
                    _textEncoder = new TextEncoder();
                    _textEncoder.LoadModel(TextEncoderModel, Backend);
                    Debug.Log("[ZipVoiceManager] TextEncoder loaded");
                }
                else
                {
                    Debug.LogWarning("[ZipVoiceManager] TextEncoderModel is not assigned");
                }

                // FMDecoderを初期化
                if (FMDecoderModel != null)
                {
                    _fmDecoder = new FMDecoder();
                    _fmDecoder.LoadModel(FMDecoderModel, Backend);
                    Debug.Log("[ZipVoiceManager] FMDecoder loaded");
                }
                else
                {
                    Debug.LogWarning("[ZipVoiceManager] FMDecoderModel is not assigned");
                }

                // Vocosを初期化
                if (VocosModel != null)
                {
                    _vocos = new Vocos();
                    _vocos.LoadModel(VocosModel, Backend);
                    Debug.Log("[ZipVoiceManager] Vocos loaded");
                }
                else
                {
                    Debug.LogWarning("[ZipVoiceManager] VocosModel is not assigned");
                }

                // オーディオプロセッサを初期化
                _istftProcessor = new ISTFTProcessor(nFft, hopLength);
                _featureExtractor = new FeatureExtractor(sampleRate, nFft, hopLength, nMels);

                _isInitialized = true;
                Debug.Log("[ZipVoiceManager] Initialization complete");

                await UniTask.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ZipVoiceManager] Initialization failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 音声合成
        /// </summary>
        /// <param name="text">合成するテキスト</param>
        /// <param name="promptAudio">プロンプト音声（声質の参照）</param>
        /// <param name="promptText">プロンプトテキスト</param>
        /// <param name="options">合成オプション（nullの場合はデフォルト）</param>
        /// <returns>生成されたAudioClip</returns>
        public async UniTask<AudioClip> SynthesizeAsync(
            string text,
            AudioClip promptAudio,
            string promptText,
            SynthesisOptions options = null)
        {
            ThrowIfNotInitialized();

            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Text cannot be null or empty", nameof(text));
            }

            if (promptAudio == null)
            {
                throw new ArgumentNullException(nameof(promptAudio),
                    "Prompt audio is required for voice cloning. Please assign a prompt audio clip in the Inspector.");
            }

            if (_isProcessing)
            {
                throw new InvalidOperationException("Already processing");
            }

            _isProcessing = true;

            try
            {
                // オプションを取得
                int numSteps = options?.NumSteps ?? (Config != null ? Config.NumSteps : 16);
                float guidanceScale = options?.GuidanceScale ?? (Config != null ? Config.GuidanceScale : 1.0f);
                float speed = options?.Speed ?? (Config != null ? Config.Speed : 1.0f);
                float tShift = Config != null ? Config.TShift : 0.5f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ZipVoiceManager] Synthesizing: \"{text}\" (steps={numSteps}, guidance={guidanceScale}, speed={speed})");
#endif

                // 1. テキストをトークン化
                int[] tokens = _tokenizer.Tokenize(text);
                int[] promptTokens = _tokenizer.Tokenize(promptText ?? "");

                // 2. プロンプト音声からメル特徴量を抽出
                float[,] promptMel = null;
                int promptFeaturesLen = 0;

                if (promptAudio != null)
                {
                    promptMel = _featureExtractor.ExtractMelSpectrogram(promptAudio);
                    promptFeaturesLen = promptMel.GetLength(0);
                }
                else
                {
                    // プロンプト音声がない場合、デフォルトの出力長を計算
                    int defaultFramesPerToken = 10;
                    promptFeaturesLen = promptTokens.Length * defaultFramesPerToken;
                }

                // UIに制御を戻す
                await UniTask.Yield();

                // 3. TextEncoderで条件ベクトルを生成
                using var textCondition = _textEncoder.Execute(tokens, promptTokens, promptFeaturesLen, speed);

                // UIに制御を戻す
                await UniTask.Yield();

                // 4. 音声条件を作成（プロンプトメル特徴量から）
                int seqLen = textCondition.shape[1];
                int featDim = textCondition.shape[2];
                float[] speechCondData = new float[1 * seqLen * featDim];

                if (promptMel != null)
                {
                    // プロンプトメル特徴量をコピー（feat_scaleを適用）
                    int copyLen = Math.Min(promptFeaturesLen, seqLen);
                    int melDim = promptMel.GetLength(1); // 実際のメル次元数（100）
                    for (int t = 0; t < copyLen; t++)
                    {
                        for (int f = 0; f < Math.Min(featDim, melDim); f++)
                        {
                            speechCondData[t * featDim + f] = promptMel[t, f] * FeatScale;
                        }
                    }
                }

                using var speechCondition = new Tensor<float>(
                    new TensorShape(1, seqLen, featDim),
                    speechCondData
                );

                // 5. EulerSolverでFMDecoderを積分
                var solver = new EulerSolver(numSteps, tShift);

                using var melFeatures = await _fmDecoder.GenerateAsync(
                    solver, textCondition, speechCondition, guidanceScale,
                    null  // 進捗コールバックを無効化（パフォーマンス向上）
                );

                // 6. プロンプト部分をトリムして生成部分のみを取得
                float[] melData = melFeatures.DownloadToArray();
                int totalFrames = melFeatures.shape[1];
                int featDimMel = melFeatures.shape[2];
                int generatedFrames = totalFrames - promptFeaturesLen;

                if (generatedFrames <= 0)
                {
                    throw new InvalidOperationException($"Generated frames ({generatedFrames}) must be positive. Total frames: {totalFrames}, Prompt frames: {promptFeaturesLen}");
                }

                // トリムされたメル特徴量を作成
                float[] trimmedMelData = new float[1 * generatedFrames * featDimMel];
                for (int t = 0; t < generatedFrames; t++)
                {
                    int srcFrame = promptFeaturesLen + t;
                    for (int f = 0; f < featDimMel; f++)
                    {
                        int srcIdx = srcFrame * featDimMel + f;
                        int dstIdx = t * featDimMel + f;
                        // feat_scaleを元に戻す（Pythonと同じ処理）
                        trimmedMelData[dstIdx] = melData[srcIdx] / FeatScale;
                    }
                }

                // スケール復元後のテンソルを作成（トリム済み）
                using var melScaled = new Tensor<float>(
                    new TensorShape(1, generatedFrames, featDimMel),
                    trimmedMelData
                );

                // 7. メル特徴量を転置 [1, T, 100] → [1, 100, T]
                using var melTransposed = Vocos.TransposeMelFeatures(melScaled);

                // UIに制御を戻す
                await UniTask.Yield();

                // 8. Vocosでメル→STFT係数
                using var vocosOutput = _vocos.Execute(melTransposed);

                // UIに制御を戻す
                await UniTask.Yield();

                // 9. ISTFTで波形に変換
                int numBins = vocosOutput.Magnitude.shape[1];
                int numFrames = vocosOutput.Magnitude.shape[2];

                float[] magnitude = vocosOutput.Magnitude.DownloadToArray();
                float[] phaseCos = vocosOutput.PhaseCos.DownloadToArray();
                float[] phaseSin = vocosOutput.PhaseSin.DownloadToArray();

                float[] waveform = _istftProcessor.Process(magnitude, phaseCos, phaseSin, numBins, numFrames);

                // UIに制御を戻す
                await UniTask.Yield();

                // 波形を正規化 (-1.0 〜 1.0)
                float waveMin = float.MaxValue, waveMax = float.MinValue;
                for (int i = 0; i < waveform.Length; i++)
                {
                    if (!float.IsNaN(waveform[i]) && !float.IsInfinity(waveform[i]))
                    {
                        waveMin = Math.Min(waveMin, waveform[i]);
                        waveMax = Math.Max(waveMax, waveform[i]);
                    }
                }

                float absMax = Math.Max(Math.Abs(waveMin), Math.Abs(waveMax));
                if (absMax > 1e-6f)
                {
                    float scale = 0.95f / absMax;
                    for (int i = 0; i < waveform.Length; i++)
                    {
                        waveform[i] *= scale;
                    }
                }

                // 10. AudioClipを作成
                int sampleRate = Config != null ? Config.SampleRate : 24000;
                AudioClip clip = AudioClip.Create("Synthesized", waveform.Length, 1, sampleRate, false);
                clip.SetData(waveform, 0);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ZipVoiceManager] Synthesis complete. Duration: {clip.length:F2}s");
#endif

                return clip;
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// リソースを解放
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _espeakTokenizer?.Dispose();
            _g2pTokenizer?.Dispose();
            _textEncoder?.Dispose();
            _fmDecoder?.Dispose();
            _vocos?.Dispose();

            _tokenizer = null;
            _espeakTokenizer = null;
            _g2pTokenizer = null;
            _textEncoder = null;
            _fmDecoder = null;
            _vocos = null;
            _tokenMap = null;
            _istftProcessor = null;
            _featureExtractor = null;

            _isInitialized = false;
            _isDisposed = true;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void ThrowIfNotInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("ZipVoiceManager is not initialized. Call InitializeAsync() first.");
            }
        }
    }
}
