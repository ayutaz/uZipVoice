using System;
using Cysharp.Threading.Tasks;
using Unity.InferenceEngine;
using UnityEngine;

namespace uZipVoice.Inference
{
    /// <summary>
    /// FM Decoder - Flow Matchingデコーダ
    /// EulerSolverと組み合わせてメル特徴量を生成
    /// </summary>
    public class FMDecoder : IDisposable
    {
        private Model _model;
        private Worker _worker;
        private BackendType _backendType;
        private bool _isDisposed;

        // 再利用可能なバッファ（メモリアロケーション削減）
        private float[] _xBuffer;

        /// <summary>
        /// モデルが読み込まれているかどうか
        /// </summary>
        public bool IsLoaded => _worker != null;

        /// <summary>
        /// 特徴量の次元数
        /// </summary>
        public int FeatureDim { get; private set; } = 100;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public FMDecoder()
        {
        }

        /// <summary>
        /// ONNXモデルを読み込む
        /// </summary>
        /// <param name="modelAsset">fm_decoder.onnxのModelAsset</param>
        /// <param name="backendType">推論バックエンド（デフォルト: GPUCompute）</param>
        public void LoadModel(ModelAsset modelAsset, BackendType backendType = BackendType.GPUCompute)
        {
            if (modelAsset == null)
            {
                throw new ArgumentNullException(nameof(modelAsset));
            }

            _model = ModelLoader.Load(modelAsset);
            _worker = new Worker(_model, backendType);
            _backendType = backendType;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[FMDecoder] Model loaded. Backend: {backendType}");
#endif
        }


        /// <summary>
        /// 単一ステップの推論を実行（速度ベクトルを返す）
        /// </summary>
        /// <param name="t">現在の時刻 (0-1)</param>
        /// <param name="x">現在の状態 [1, T, 100]</param>
        /// <param name="textCondition">テキスト条件 [1, T, 100]</param>
        /// <param name="speechCondition">音声条件 [1, T, 100]</param>
        /// <param name="guidanceScale">CFGスケール</param>
        /// <returns>速度ベクトル [1, T, 100]</returns>
        public Tensor<float> ExecuteStep(
            float t,
            Tensor<float> x,
            Tensor<float> textCondition,
            Tensor<float> speechCondition,
            float guidanceScale = 1.0f)
        {
            ThrowIfNotLoaded();

            if (x == null)
            {
                throw new ArgumentNullException(nameof(x));
            }

            if (textCondition == null)
            {
                throw new ArgumentNullException(nameof(textCondition));
            }

            if (speechCondition == null)
            {
                throw new ArgumentNullException(nameof(speechCondition));
            }

            // スカラーテンソル（rank 0）として作成（ONNXモデルの期待形式）
            using var tTensor = new Tensor<float>(new TensorShape(), new float[] { t });
            using var guidanceScaleTensor = new Tensor<float>(new TensorShape(), new float[] { guidanceScale });

            // 入力を設定
            _worker.SetInput("t", tTensor);
            _worker.SetInput("x", x);
            _worker.SetInput("text_condition", textCondition);
            _worker.SetInput("speech_condition", speechCondition);
            _worker.SetInput("guidance_scale", guidanceScaleTensor);

            // 推論実行
            _worker.Schedule();

            // 出力を取得（速度ベクトル）
            var output = _worker.PeekOutput("v") as Tensor<float>;
            return output.ReadbackAndClone();
        }

        /// <summary>
        /// EulerSolverを使用して全ステップの積分を実行（最適化版）
        /// </summary>
        /// <param name="solver">EulerSolver</param>
        /// <param name="textCondition">テキスト条件 [1, T, 100]</param>
        /// <param name="speechCondition">音声条件 [1, T, 100]</param>
        /// <param name="guidanceScale">CFGスケール</param>
        /// <param name="onProgress">進捗コールバック (0.0-1.0)</param>
        /// <returns>最終的なメル特徴量 [1, T, 100]</returns>
        public async UniTask<Tensor<float>> GenerateAsync(
            EulerSolver solver,
            Tensor<float> textCondition,
            Tensor<float> speechCondition,
            float guidanceScale = 1.0f,
            Action<float> onProgress = null)
        {
            ThrowIfNotLoaded();

            if (solver == null)
            {
                throw new ArgumentNullException(nameof(solver));
            }

            // 初期状態（ランダムノイズ）
            int batchSize = textCondition.shape[0];
            int seqLen = textCondition.shape[1];
            int featDim = FeatureDim;
            int totalSize = batchSize * seqLen * featDim;

            // バッファを再利用（メモリアロケーション削減）
            if (_xBuffer == null || _xBuffer.Length != totalSize)
            {
                _xBuffer = new float[totalSize];
            }

            // Box-Muller変換で正規分布ノイズを生成
            var random = new System.Random();
            for (int i = 0; i < totalSize; i++)
            {
                double u1 = 1.0 - random.NextDouble();
                double u2 = 1.0 - random.NextDouble();
                _xBuffer[i] = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
            }

            var shape = new TensorShape(batchSize, seqLen, featDim);
            var x = new Tensor<float>(shape, _xBuffer);

            float[] timesteps = solver.GetTimesteps();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[FMDecoder] Starting Euler integration: {solver.NumSteps} steps, seqLen={seqLen}");
            var totalWatch = System.Diagnostics.Stopwatch.StartNew();
#endif

            // スカラーテンソルを事前に作成して再利用
            using var guidanceScaleTensor = new Tensor<float>(new TensorShape(), new float[] { guidanceScale });

            // Euler積分ループ（最適化版）
            for (int step = 0; step < solver.NumSteps; step++)
            {
                float t = timesteps[step];
                float dt = solver.GetDt(step);

                // スカラーテンソルを作成
                using var tTensor = new Tensor<float>(new TensorShape(), new float[] { t });

                // 入力を設定
                _worker.SetInput("t", tTensor);
                _worker.SetInput("x", x);
                _worker.SetInput("text_condition", textCondition);
                _worker.SetInput("speech_condition", speechCondition);
                _worker.SetInput("guidance_scale", guidanceScaleTensor);

                // 推論実行
                _worker.Schedule();

                // 速度を取得
                var velocity = _worker.PeekOutput("v") as Tensor<float>;
                var vData = velocity.DownloadToArray();

                // xデータを取得
                var xData = x.DownloadToArray();

                // CPU上でEulerステップを実行（SIMD最適化可能）
                for (int i = 0; i < totalSize; i++)
                {
                    _xBuffer[i] = xData[i] + dt * vData[i];
                }

                // 古いテンソルを解放して新しいテンソルを作成
                x.Dispose();
                x = new Tensor<float>(shape, _xBuffer);

                // 進捗を報告
                onProgress?.Invoke((float)(step + 1) / solver.NumSteps);

                // 毎ステップでUIに制御を戻す（UIフリーズ防止）
                await UniTask.Yield();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[FMDecoder] Euler integration completed in {totalWatch.ElapsedMilliseconds}ms");
#endif
            return x;
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
            _xBuffer = null;
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
