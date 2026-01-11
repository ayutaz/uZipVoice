using System;
using System.Collections.Generic;
using NWaves.Transforms;
using NWaves.Windows;
using UnityEngine;

namespace uZipVoice.Audio
{
    /// <summary>
    /// ISTFT (Inverse Short-Time Fourier Transform) プロセッサ
    /// Vocosの出力（magnitude, phase_cos, phase_sin）から波形を生成
    /// NWavesライブラリを使用した実装
    /// </summary>
    public class ISTFTProcessor
    {
        private readonly int _nFft;
        private readonly int _hopLength;
        private readonly Stft _stft;

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

            // NWaves STFTインスタンスを作成
            _stft = new Stft(nFft, hopLength, WindowType.Hann);
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

            // NWavesのStft.Inverseは List<(float[] real, float[] imag)> 形式を期待
            // 各要素はフレームごとの(実部, 虚部)タプル
            var spectrogram = new List<(float[], float[])>(numFrames);

            for (int frame = 0; frame < numFrames; frame++)
            {
                var realFrame = new float[numBins];
                var imagFrame = new float[numBins];

                for (int bin = 0; bin < numBins; bin++)
                {
                    // Vocos出力は [numBins, numFrames] の形式でフラット化されている
                    int idx = bin * numFrames + frame;

                    float mag = magnitude[idx];
                    float cos = phaseCos[idx];
                    float sin = phaseSin[idx];

                    // 複素数を構築: magnitude * (cos + i*sin)
                    // = magnitude * cos + i * magnitude * sin
                    realFrame[bin] = mag * cos;
                    imagFrame[bin] = mag * sin;
                }

                spectrogram.Add((realFrame, imagFrame));
            }

            // NWavesのSTFT逆変換を実行
            float[] waveform = _stft.Inverse(spectrogram);

            Debug.Log($"[ISTFTProcessor] NWaves ISTFT completed. Output length: {waveform.Length}");

            return waveform;
        }
    }
}
