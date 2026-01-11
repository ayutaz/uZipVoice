using System;
using UnityEngine;

namespace uZipVoice.Inference
{
    /// <summary>
    /// Flow MatchingのためのEuler ODE積分ソルバー
    /// </summary>
    public class EulerSolver
    {
        private readonly int _numSteps;
        private readonly float _tShift;
        private readonly float _tStart;
        private readonly float _tEnd;
        private readonly float[] _timesteps;

        /// <summary>
        /// ステップ数
        /// </summary>
        public int NumSteps => _numSteps;

        /// <summary>
        /// タイムシフトパラメータ
        /// </summary>
        public float TShift => _tShift;

        /// <summary>
        /// 開始時刻
        /// </summary>
        public float TStart => _tStart;

        /// <summary>
        /// 終了時刻
        /// </summary>
        public float TEnd => _tEnd;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="numSteps">積分ステップ数 (1以上)</param>
        /// <param name="tShift">タイムシフトパラメータ (0〜1)</param>
        /// <param name="tStart">開始時刻</param>
        /// <param name="tEnd">終了時刻</param>
        public EulerSolver(int numSteps, float tShift = 0.5f, float tStart = 0f, float tEnd = 1f)
        {
            if (numSteps < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(numSteps), "Number of steps must be at least 1");
            }

            if (tShift <= 0f || tShift > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(tShift), "t_shift must be in range (0, 1]");
            }

            if (tStart >= tEnd)
            {
                throw new ArgumentException("t_start must be less than t_end");
            }

            _numSteps = numSteps;
            _tShift = tShift;
            _tStart = tStart;
            _tEnd = tEnd;
            _timesteps = ComputeTimesteps();
        }

        /// <summary>
        /// タイムステップを計算
        /// </summary>
        private float[] ComputeTimesteps()
        {
            float[] timesteps = new float[_numSteps + 1];

            for (int i = 0; i <= _numSteps; i++)
            {
                // 線形補間 t ∈ [0, 1]
                float t = (float)i / _numSteps;
                // t_shifted = t_shift * t / (1 + (t_shift - 1) * t)
                float tShifted = _tShift * t / (1f + (_tShift - 1f) * t);
                // スケーリング: [0,1] → [t_start, t_end]
                timesteps[i] = _tStart + tShifted * (_tEnd - _tStart);
            }

            return timesteps;
        }

        /// <summary>
        /// タイムステップ配列を取得
        /// </summary>
        /// <returns>タイムステップ配列 (長さ: numSteps + 1)</returns>
        public float[] GetTimesteps()
        {
            // コピーを返す
            float[] copy = new float[_timesteps.Length];
            Array.Copy(_timesteps, copy, _timesteps.Length);
            return copy;
        }

        /// <summary>
        /// 指定インデックスのタイムステップを取得
        /// </summary>
        /// <param name="index">ステップインデックス (0 〜 numSteps)</param>
        /// <returns>タイムステップ値</returns>
        public float GetTimestep(int index)
        {
            if (index < 0 || index > _numSteps)
            {
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Index must be in range [0, {_numSteps}]");
            }
            return _timesteps[index];
        }

        /// <summary>
        /// ステップの時間差分を取得
        /// </summary>
        /// <param name="stepIndex">ステップインデックス (0 〜 numSteps-1)</param>
        /// <returns>時間差分 dt</returns>
        public float GetDt(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= _numSteps)
            {
                throw new ArgumentOutOfRangeException(nameof(stepIndex),
                    $"Step index must be in range [0, {_numSteps - 1}]");
            }
            return _timesteps[stepIndex + 1] - _timesteps[stepIndex];
        }

        /// <summary>
        /// 単一のEulerステップを実行
        /// x_new = x + dt * velocity
        /// </summary>
        /// <param name="x">現在の状態 (長さ N)</param>
        /// <param name="velocity">速度ベクトル (長さ N)</param>
        /// <param name="stepIndex">ステップインデックス</param>
        /// <returns>更新された状態</returns>
        public float[] Step(float[] x, float[] velocity, int stepIndex)
        {
            if (x == null)
            {
                throw new ArgumentNullException(nameof(x));
            }

            if (velocity == null)
            {
                throw new ArgumentNullException(nameof(velocity));
            }

            if (x.Length != velocity.Length)
            {
                throw new ArgumentException("x and velocity must have the same length");
            }

            float dt = GetDt(stepIndex);
            float[] result = new float[x.Length];

            for (int i = 0; i < x.Length; i++)
            {
                result[i] = x[i] + dt * velocity[i];
            }

            return result;
        }

        /// <summary>
        /// 単一のEulerステップをインプレースで実行
        /// x = x + dt * velocity
        /// </summary>
        /// <param name="x">現在の状態 (更新される)</param>
        /// <param name="velocity">速度ベクトル</param>
        /// <param name="stepIndex">ステップインデックス</param>
        public void StepInPlace(float[] x, float[] velocity, int stepIndex)
        {
            if (x == null)
            {
                throw new ArgumentNullException(nameof(x));
            }

            if (velocity == null)
            {
                throw new ArgumentNullException(nameof(velocity));
            }

            if (x.Length != velocity.Length)
            {
                throw new ArgumentException("x and velocity must have the same length");
            }

            float dt = GetDt(stepIndex);

            for (int i = 0; i < x.Length; i++)
            {
                x[i] += dt * velocity[i];
            }
        }
    }
}
