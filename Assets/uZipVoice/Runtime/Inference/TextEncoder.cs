using System;
using Unity.InferenceEngine;
using UnityEngine;

namespace uZipVoice.Inference
{
    /// <summary>
    /// Text Encoder - テキストトークンを条件ベクトルに変換
    /// </summary>
    public class TextEncoder : IDisposable
    {
        private Model _model;
        private Worker _worker;
        private bool _isDisposed;

        /// <summary>
        /// モデルが読み込まれているかどうか
        /// </summary>
        public bool IsLoaded => _worker != null;

        /// <summary>
        /// 出力の次元数
        /// </summary>
        public int OutputDim { get; private set; } = 512;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TextEncoder()
        {
        }

        /// <summary>
        /// ONNXモデルを読み込む
        /// </summary>
        /// <param name="modelAsset">text_encoder.onnxのModelAsset</param>
        /// <param name="backendType">推論バックエンド（デフォルト: GPUCompute）</param>
        public void LoadModel(ModelAsset modelAsset, BackendType backendType = BackendType.GPUCompute)
        {
            if (modelAsset == null)
            {
                throw new ArgumentNullException(nameof(modelAsset));
            }

            _model = ModelLoader.Load(modelAsset);
            _worker = new Worker(_model, backendType);

            Debug.Log($"[TextEncoder] Model loaded. Backend: {backendType}");
        }

        /// <summary>
        /// 推論を実行
        /// </summary>
        /// <param name="tokens">テキストトークン [1, T]</param>
        /// <param name="promptTokens">プロンプトトークン [1, T_prompt]</param>
        /// <param name="promptFeaturesLen">プロンプト特徴量の長さ</param>
        /// <param name="speed">速度係数（1.0 = 通常）</param>
        /// <returns>テキスト条件ベクトル [1, T_out, 512]</returns>
        public Tensor<float> Execute(
            int[] tokens,
            int[] promptTokens,
            int promptFeaturesLen,
            float speed = 1.0f)
        {
            ThrowIfNotLoaded();

            if (tokens == null || tokens.Length == 0)
            {
                throw new ArgumentException("Tokens cannot be null or empty", nameof(tokens));
            }

            if (promptTokens == null || promptTokens.Length == 0)
            {
                throw new ArgumentException("Prompt tokens cannot be null or empty", nameof(promptTokens));
            }

            Debug.Log($"[TextEncoder] tokens.Length={tokens.Length}, promptTokens.Length={promptTokens.Length}, promptFeaturesLen={promptFeaturesLen}");

            // INT32テンソルを作成（Sentisの推奨形式）
            // Note: ONNXはint64を期待するが、SentisはTensor<int>をint64として扱う
            using var tokensTensor = new Tensor<int>(
                new TensorShape(1, tokens.Length),
                tokens
            );

            using var promptTokensTensor = new Tensor<int>(
                new TensorShape(1, promptTokens.Length),
                promptTokens
            );

            // スカラーテンソル（rank 0）として作成
            using var promptFeaturesLenTensor = new Tensor<int>(
                new TensorShape(),
                new int[] { promptFeaturesLen }
            );

            using var speedTensor = new Tensor<float>(
                new TensorShape(),
                new float[] { speed }
            );

            // 入力を設定
            _worker.SetInput("tokens", tokensTensor);
            _worker.SetInput("prompt_tokens", promptTokensTensor);
            _worker.SetInput("prompt_features_len", promptFeaturesLenTensor);
            _worker.SetInput("speed", speedTensor);

            // 推論実行
            _worker.Schedule();

            // 出力を取得（コピーを作成して返す）
            var output = _worker.PeekOutput("text_condition") as Tensor<float>;
            return output.ReadbackAndClone();
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
