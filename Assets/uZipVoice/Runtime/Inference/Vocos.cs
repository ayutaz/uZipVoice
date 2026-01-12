using System;
using Unity.InferenceEngine;
using UnityEngine;

namespace uZipVoice.Inference
{
    /// <summary>
    /// Vocosの出力結果
    /// </summary>
    public struct VocosOutput : IDisposable
    {
        /// <summary>
        /// 振幅スペクトル [1, 513, T]
        /// </summary>
        public Tensor<float> Magnitude;

        /// <summary>
        /// 位相のcos成分 [1, 513, T]
        /// </summary>
        public Tensor<float> PhaseCos;

        /// <summary>
        /// 位相のsin成分 [1, 513, T]
        /// </summary>
        public Tensor<float> PhaseSin;

        public void Dispose()
        {
            Magnitude?.Dispose();
            PhaseCos?.Dispose();
            PhaseSin?.Dispose();
        }
    }

    /// <summary>
    /// Vocos Vocoder - メル特徴量からSTFT係数に変換
    /// </summary>
    public class Vocos : IDisposable
    {
        private Model _model;
        private Worker _worker;
        private bool _isDisposed;

        /// <summary>
        /// モデルが読み込まれているかどうか
        /// </summary>
        public bool IsLoaded => _worker != null;

        /// <summary>
        /// FFTビン数（n_fft/2 + 1）
        /// </summary>
        public int NumBins { get; private set; } = 513;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public Vocos()
        {
        }

        /// <summary>
        /// ONNXモデルを読み込む
        /// </summary>
        /// <param name="modelAsset">vocos_opset15.onnxのModelAsset</param>
        /// <param name="backendType">推論バックエンド（デフォルト: GPUCompute）</param>
        public void LoadModel(ModelAsset modelAsset, BackendType backendType = BackendType.GPUCompute)
        {
            if (modelAsset == null)
            {
                throw new ArgumentNullException(nameof(modelAsset));
            }

            _model = ModelLoader.Load(modelAsset);
            _worker = new Worker(_model, backendType);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Vocos] Model loaded. Backend: {backendType}");
#endif
        }

        /// <summary>
        /// 推論を実行
        /// </summary>
        /// <param name="melSpectrogram">メルスペクトログラム [1, 100, T]</param>
        /// <returns>STFT係数（magnitude, phase_cos, phase_sin）</returns>
        public VocosOutput Execute(Tensor<float> melSpectrogram)
        {
            ThrowIfNotLoaded();

            if (melSpectrogram == null)
            {
                throw new ArgumentNullException(nameof(melSpectrogram));
            }

            // 入力を設定
            _worker.SetInput("mel_spectrogram", melSpectrogram);

            // 推論実行
            _worker.Schedule();

            // 出力を取得
            var magnitude = (_worker.PeekOutput("magnitude") as Tensor<float>).ReadbackAndClone();
            var phaseCos = (_worker.PeekOutput("phase_cos") as Tensor<float>).ReadbackAndClone();
            var phaseSin = (_worker.PeekOutput("phase_sin") as Tensor<float>).ReadbackAndClone();

            return new VocosOutput
            {
                Magnitude = magnitude,
                PhaseCos = phaseCos,
                PhaseSin = phaseSin
            };
        }

        /// <summary>
        /// メル特徴量の次元を変換 [1, T, 100] → [1, 100, T]
        /// </summary>
        /// <param name="melFeatures">メル特徴量 [1, T, 100]</param>
        /// <returns>転置されたメル特徴量 [1, 100, T]</returns>
        public static Tensor<float> TransposeMelFeatures(Tensor<float> melFeatures)
        {
            if (melFeatures == null)
            {
                throw new ArgumentNullException(nameof(melFeatures));
            }

            int batch = melFeatures.shape[0];
            int seqLen = melFeatures.shape[1];
            int featDim = melFeatures.shape[2];

            float[] inputData = melFeatures.DownloadToArray();
            float[] outputData = new float[inputData.Length];

            // [B, T, F] → [B, F, T]
            for (int b = 0; b < batch; b++)
            {
                for (int t = 0; t < seqLen; t++)
                {
                    for (int f = 0; f < featDim; f++)
                    {
                        int srcIdx = b * seqLen * featDim + t * featDim + f;
                        int dstIdx = b * featDim * seqLen + f * seqLen + t;
                        outputData[dstIdx] = inputData[srcIdx];
                    }
                }
            }

            return new Tensor<float>(
                new TensorShape(batch, featDim, seqLen),
                outputData
            );
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

            _worker?.Dispose();
            _worker = null;
            _model = null;
            _isDisposed = true;
        }

        private void ThrowIfNotLoaded()
        {
            if (!IsLoaded)
            {
                throw new InvalidOperationException("Model is not loaded. Call LoadModel() first.");
            }
        }
    }
}
