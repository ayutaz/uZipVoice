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
        private EspeakTokenizer _tokenizer;
        private TextEncoder _textEncoder;
        private FMDecoder _fmDecoder;
        private Vocos _vocos;
        private ISTFTProcessor _istftProcessor;
        private FeatureExtractor _featureExtractor;

        private bool _isInitialized;
        private bool _isProcessing;
        private bool _isDisposed;

        /// <summary>
        /// 初期化済みかどうか
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 処理中かどうか
        /// </summary>
        public bool IsProcessing => _isProcessing;

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
                _tokenizer = new EspeakTokenizer(_tokenMap);
                _tokenizer.Voice = voice;

                string espeakDataPath = Path.Combine(Application.streamingAssetsPath, "espeak-ng-data");
                if (Directory.Exists(espeakDataPath))
                {
                    _tokenizer.Initialize(espeakDataPath);
                    Debug.Log("[ZipVoiceManager] EspeakTokenizer initialized");
                }
                else
                {
                    Debug.LogWarning($"[ZipVoiceManager] espeak-ng-data not found at {espeakDataPath}");
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

                Debug.Log($"[ZipVoiceManager] Synthesizing: \"{text}\" (steps={numSteps}, guidance={guidanceScale}, speed={speed})");

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
                    // TextEncoderの計算式: features_len = (prompt_features_len / prompt_tokens_len * tokens_len / speed)
                    // デフォルトでは1トークンあたり約10フレーム（約100ms）と仮定
                    int defaultFramesPerToken = 10;
                    promptFeaturesLen = promptTokens.Length * defaultFramesPerToken;
                    Debug.Log($"[ZipVoiceManager] No prompt audio. Using estimated promptFeaturesLen={promptFeaturesLen}");
                }

                // 3. TextEncoderで条件ベクトルを生成
                Debug.Log("[ZipVoiceManager] Running TextEncoder...");
                using var textCondition = _textEncoder.Execute(tokens, promptTokens, promptFeaturesLen, speed);
                Debug.Log($"[ZipVoiceManager] textCondition shape: [{textCondition.shape[0]}, {textCondition.shape[1]}, {textCondition.shape[2]}]");
                await UniTask.Yield(); // UIスレッドに制御を戻す

                // 4. 音声条件を作成（プロンプトメル特徴量から）
                int seqLen = textCondition.shape[1];
                int featDim = textCondition.shape[2]; // TextEncoderの出力次元に合わせる
                Debug.Log($"[ZipVoiceManager] Using seqLen={seqLen}, featDim={featDim}");
                float[] speechCondData = new float[1 * seqLen * featDim];

                if (promptMel != null)
                {
                    // プロンプトメル特徴量をコピー
                    int copyLen = Math.Min(promptFeaturesLen, seqLen);
                    for (int t = 0; t < copyLen; t++)
                    {
                        for (int f = 0; f < featDim; f++)
                        {
                            speechCondData[t * featDim + f] = promptMel[t, f];
                        }
                    }
                }

                using var speechCondition = new Tensor<float>(
                    new TensorShape(1, seqLen, featDim),
                    speechCondData
                );

                // 5. EulerSolverでFMDecoderを積分
                var solver = new EulerSolver(numSteps, tShift);

                // textConditionを [1, T, 512] → [1, T, 100] に変換する必要がある
                // 実際のモデルの仕様に合わせて調整が必要
                using var melFeatures = await _fmDecoder.GenerateAsync(
                    solver, textCondition, speechCondition, guidanceScale,
                    progress => Debug.Log($"[ZipVoiceManager] FM Decoder progress: {progress * 100:F0}%")
                );

                // デバッグ: FMDecoder出力の統計
                float[] melData = melFeatures.DownloadToArray();
                float melMin = float.MaxValue, melMax = float.MinValue, melSum = 0;
                for (int i = 0; i < melData.Length; i++)
                {
                    melMin = Math.Min(melMin, melData[i]);
                    melMax = Math.Max(melMax, melData[i]);
                    melSum += melData[i];
                }
                Debug.Log($"[ZipVoiceManager] Mel features stats: min={melMin:F4}, max={melMax:F4}, mean={melSum / melData.Length:F4}, shape=[{melFeatures.shape[0]}, {melFeatures.shape[1]}, {melFeatures.shape[2]}]");

                // 6. メル特徴量を転置 [1, T, 100] → [1, 100, T]
                using var melTransposed = Vocos.TransposeMelFeatures(melFeatures);

                // 7. Vocosでメル→STFT係数
                Debug.Log("[ZipVoiceManager] Running Vocos...");
                using var vocosOutput = _vocos.Execute(melTransposed);
                await UniTask.Yield(); // UIスレッドに制御を戻す

                // 8. ISTFTで波形に変換
                Debug.Log("[ZipVoiceManager] Running ISTFT...");
                int numBins = vocosOutput.Magnitude.shape[1];
                int numFrames = vocosOutput.Magnitude.shape[2];
                Debug.Log($"[ZipVoiceManager] Vocos output: numBins={numBins}, numFrames={numFrames}");

                float[] magnitude = vocosOutput.Magnitude.DownloadToArray();
                float[] phaseCos = vocosOutput.PhaseCos.DownloadToArray();
                float[] phaseSin = vocosOutput.PhaseSin.DownloadToArray();

                // デバッグ: Vocos出力の統計
                float magMin = float.MaxValue, magMax = float.MinValue, magSum = 0;
                for (int i = 0; i < magnitude.Length; i++)
                {
                    magMin = Math.Min(magMin, magnitude[i]);
                    magMax = Math.Max(magMax, magnitude[i]);
                    magSum += magnitude[i];
                }
                Debug.Log($"[ZipVoiceManager] Magnitude stats: min={magMin:F4}, max={magMax:F4}, mean={magSum / magnitude.Length:F4}");

                float[] waveform = _istftProcessor.Process(magnitude, phaseCos, phaseSin, numBins, numFrames);

                // デバッグ: 波形の統計
                float waveMin = float.MaxValue, waveMax = float.MinValue;
                int nanCount = 0, infCount = 0;
                for (int i = 0; i < waveform.Length; i++)
                {
                    if (float.IsNaN(waveform[i])) nanCount++;
                    else if (float.IsInfinity(waveform[i])) infCount++;
                    else
                    {
                        waveMin = Math.Min(waveMin, waveform[i]);
                        waveMax = Math.Max(waveMax, waveform[i]);
                    }
                }
                Debug.Log($"[ZipVoiceManager] Waveform stats: min={waveMin:F4}, max={waveMax:F4}, length={waveform.Length}, NaN={nanCount}, Inf={infCount}");

                // 波形を正規化 (-1.0 〜 1.0)
                float absMax = Math.Max(Math.Abs(waveMin), Math.Abs(waveMax));
                if (absMax > 1e-6f)
                {
                    float scale = 0.95f / absMax; // 少しマージンを持たせる
                    for (int i = 0; i < waveform.Length; i++)
                    {
                        waveform[i] *= scale;
                    }
                    Debug.Log($"[ZipVoiceManager] Waveform normalized with scale={scale:F4}");
                }

                // 9. AudioClipを作成
                int sampleRate = Config != null ? Config.SampleRate : 24000;
                AudioClip clip = AudioClip.Create("Synthesized", waveform.Length, 1, sampleRate, false);
                clip.SetData(waveform, 0);

                Debug.Log($"[ZipVoiceManager] Synthesis complete. Duration: {clip.length:F2}s");

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

            _tokenizer?.Dispose();
            _textEncoder?.Dispose();
            _fmDecoder?.Dispose();
            _vocos?.Dispose();

            _tokenizer = null;
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
