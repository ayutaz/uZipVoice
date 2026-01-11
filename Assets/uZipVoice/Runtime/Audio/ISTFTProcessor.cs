using System;
using UnityEngine;

namespace uZipVoice.Audio
{
    /// <summary>
    /// ISTFT (Inverse Short-Time Fourier Transform) プロセッサ
    /// Vocosの出力（magnitude, phase_cos, phase_sin）から波形を生成
    /// </summary>
    public class ISTFTProcessor
    {
        private readonly int _nFft;
        private readonly int _hopLength;
        private readonly float[] _window;

        /// <summary>
        /// FFTサイズ
        /// </summary>
        public int NFft => _nFft;

        /// <summary>
        /// ホップ長
        /// </summary>
        public int HopLength => _hopLength;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="nFft">FFTサイズ（デフォルト: 1024）</param>
        /// <param name="hopLength">ホップ長（デフォルト: 256）</param>
        public ISTFTProcessor(int nFft = 1024, int hopLength = 256)
        {
            if (nFft <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nFft), "nFft must be positive");
            }

            if (hopLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hopLength), "hopLength must be positive");
            }

            if (hopLength > nFft)
            {
                throw new ArgumentException("hopLength must be less than or equal to nFft");
            }

            _nFft = nFft;
            _hopLength = hopLength;
            _window = CreateHannWindow(nFft);
        }

        /// <summary>
        /// STFT係数から波形を生成
        /// </summary>
        /// <param name="magnitude">振幅スペクトル [numBins, numFrames]</param>
        /// <param name="phaseCos">位相のcos成分 [numBins, numFrames]</param>
        /// <param name="phaseSin">位相のsin成分 [numBins, numFrames]</param>
        /// <returns>波形データ</returns>
        public float[] Process(float[] magnitude, float[] phaseCos, float[] phaseSin, int numBins, int numFrames)
        {
            if (magnitude == null)
            {
                throw new ArgumentNullException(nameof(magnitude));
            }

            if (phaseCos == null)
            {
                throw new ArgumentNullException(nameof(phaseCos));
            }

            if (phaseSin == null)
            {
                throw new ArgumentNullException(nameof(phaseSin));
            }

            // 出力波形の長さを計算
            int outputLength = (numFrames - 1) * _hopLength + _nFft;
            float[] output = new float[outputLength];
            float[] windowSum = new float[outputLength];

            // 複素スペクトルを構築
            float[] real = new float[_nFft];
            float[] imag = new float[_nFft];

            for (int frame = 0; frame < numFrames; frame++)
            {
                // 複素スペクトルを構築 (mag * cos, mag * sin)
                for (int bin = 0; bin < numBins; bin++)
                {
                    int idx = bin * numFrames + frame;
                    float mag = magnitude[idx];
                    float cos = phaseCos[idx];
                    float sin = phaseSin[idx];

                    real[bin] = mag * cos;
                    imag[bin] = mag * sin;

                    // 対称性を利用して残りのビンを埋める
                    if (bin > 0 && bin < numBins - 1)
                    {
                        int mirrorBin = _nFft - bin;
                        real[mirrorBin] = real[bin];
                        imag[mirrorBin] = -imag[bin]; // 共役
                    }
                }

                // IFFTを実行
                float[] frameData = IFFT(real, imag);

                // ウィンドウを適用してオーバーラップ加算
                int startSample = frame * _hopLength;
                for (int i = 0; i < _nFft; i++)
                {
                    int outputIdx = startSample + i;
                    if (outputIdx < outputLength)
                    {
                        output[outputIdx] += frameData[i] * _window[i];
                        windowSum[outputIdx] += _window[i] * _window[i];
                    }
                }
            }

            // ウィンドウ正規化
            for (int i = 0; i < outputLength; i++)
            {
                if (windowSum[i] > 1e-8f)
                {
                    output[i] /= windowSum[i];
                }
            }

            return output;
        }

        /// <summary>
        /// Hannウィンドウを作成
        /// </summary>
        private static float[] CreateHannWindow(int length)
        {
            float[] window = new float[length];
            for (int i = 0; i < length; i++)
            {
                window[i] = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * i / length));
            }
            return window;
        }

        /// <summary>
        /// 逆FFTを実行（Cooley-Tukey）
        /// </summary>
        private float[] IFFT(float[] real, float[] imag)
        {
            int n = real.Length;
            float[] result = new float[n];

            // 簡易的なDFT実装（最適化されていない）
            // 本番環境ではNWavesなどの最適化されたライブラリを使用すべき
            for (int k = 0; k < n; k++)
            {
                float sumReal = 0;
                float sumImag = 0;

                for (int t = 0; t < n; t++)
                {
                    float angle = 2f * Mathf.PI * t * k / n;
                    sumReal += real[t] * Mathf.Cos(angle) - imag[t] * Mathf.Sin(angle);
                    sumImag += real[t] * Mathf.Sin(angle) + imag[t] * Mathf.Cos(angle);
                }

                result[k] = sumReal / n;
            }

            return result;
        }
    }
}
