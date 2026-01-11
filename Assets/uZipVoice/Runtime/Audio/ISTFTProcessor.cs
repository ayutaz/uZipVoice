using System;
using NWaves.Transforms;
using UnityEngine;

namespace uZipVoice.Audio
{
    /// <summary>
    /// ISTFT (Inverse Short-Time Fourier Transform) プロセッサ
    /// Vocosの出力（magnitude, phase_cos, phase_sin）から波形を生成
    /// NWaves FFTを使用した手動overlap-add実装
    /// </summary>
    public class ISTFTProcessor
    {
        private readonly int _nFft;
        private readonly int _hopLength;
        private readonly float[] _window;
        private readonly Fft _fft;

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

            // Hann窓を作成
            _window = new float[nFft];
            for (int i = 0; i < nFft; i++)
            {
                _window[i] = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / nFft));
            }

            // NWaves Fftを作成（標準の複素FFT）
            _fft = new Fft(nFft);
        }

        /// <summary>
        /// STFT係数から波形を生成
        /// </summary>
        /// <param name="magnitude">振幅スペクトル [numBins, numFrames] (flatten)</param>
        /// <param name="phaseCos">位相のcos成分 [numBins, numFrames] (flatten)</param>
        /// <param name="phaseSin">位相のsin成分 [numBins, numFrames] (flatten)</param>
        /// <param name="numBins">周波数ビン数（通常 nFft/2 + 1 = 513）</param>
        /// <param name="numFrames">フレーム数</param>
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

            // 出力波形のサイズを計算
            int outputLength = (numFrames - 1) * _hopLength + _nFft;
            float[] output = new float[outputLength];
            float[] windowSum = new float[outputLength];

            // 各フレームを処理してoverlap-add
            float[] realSpectrum = new float[_nFft];
            float[] imagSpectrum = new float[_nFft];

            for (int frame = 0; frame < numFrames; frame++)
            {
                // スペクトルを構築（共役対称性を使って全スペクトルを復元）
                Array.Clear(realSpectrum, 0, _nFft);
                Array.Clear(imagSpectrum, 0, _nFft);

                for (int bin = 0; bin < numBins; bin++)
                {
                    // Vocos出力は [numBins, numFrames] の形式でフラット化されている
                    int idx = bin * numFrames + frame;

                    float mag = magnitude[idx];
                    float cos = phaseCos[idx];
                    float sin = phaseSin[idx];

                    // 複素数を構築: magnitude * (cos + i*sin)
                    float real = mag * cos;
                    float imag = mag * sin;

                    // 正の周波数を設定
                    realSpectrum[bin] = real;
                    imagSpectrum[bin] = imag;

                    // 共役対称性を利用してネガティブ周波数を設定（DC成分とナイキスト周波数を除く）
                    if (bin > 0 && bin < numBins - 1)
                    {
                        int mirrorBin = _nFft - bin;
                        realSpectrum[mirrorBin] = real;
                        imagSpectrum[mirrorBin] = -imag; // 共役
                    }
                }

                // IFFTを実行（in-place）
                _fft.Inverse(realSpectrum, imagSpectrum);

                // NWaves FftはIFFT結果を正規化しないため、nFftで割る
                float normFactor = 1.0f / _nFft;

                // 窓関数を適用してoverlap-add
                int frameStart = frame * _hopLength;
                for (int i = 0; i < _nFft; i++)
                {
                    int outIdx = frameStart + i;
                    if (outIdx < outputLength)
                    {
                        // IFFT結果の実部のみを使用（虚部は理論上ゼロ）
                        float sample = realSpectrum[i] * normFactor;
                        output[outIdx] += sample * _window[i];
                        windowSum[outIdx] += _window[i] * _window[i];
                    }
                }
            }

            // 窓関数の二乗和で正規化（COLA条件）
            // 境界部分では窓関数の和が小さいため、最小閾値を設定
            // Hann窓で4倍オーバーラップの場合、中央部分のwindowSumは約1.5
            float minWindowSum = 0.5f;
            for (int i = 0; i < outputLength; i++)
            {
                float ws = Math.Max(windowSum[i], minWindowSum);
                output[i] /= ws;
            }

            Debug.Log($"[ISTFTProcessor] ISTFT completed. Output length: {outputLength}, frames: {numFrames}");

            return output;
        }
    }
}
