using System;
using UnityEngine;

namespace uZipVoice.Audio
{
    /// <summary>
    /// 音声特徴抽出器
    /// 音声波形からメルスペクトログラムを抽出
    /// </summary>
    public class FeatureExtractor
    {
        private readonly int _sampleRate;
        private readonly int _nFft;
        private readonly int _hopLength;
        private readonly int _nMels;
        private readonly float[] _window;
        private readonly float[,] _melFilterbank;

        /// <summary>
        /// サンプルレート
        /// </summary>
        public int SampleRate => _sampleRate;

        /// <summary>
        /// FFTサイズ
        /// </summary>
        public int NFft => _nFft;

        /// <summary>
        /// ホップ長
        /// </summary>
        public int HopLength => _hopLength;

        /// <summary>
        /// メルバンド数
        /// </summary>
        public int NMels => _nMels;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="sampleRate">サンプルレート（デフォルト: 24000）</param>
        /// <param name="nFft">FFTサイズ（デフォルト: 1024）</param>
        /// <param name="hopLength">ホップ長（デフォルト: 256）</param>
        /// <param name="nMels">メルバンド数（デフォルト: 100）</param>
        public FeatureExtractor(
            int sampleRate = 24000,
            int nFft = 1024,
            int hopLength = 256,
            int nMels = 100)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            if (nFft <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nFft));
            }

            if (hopLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hopLength));
            }

            if (nMels <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nMels));
            }

            _sampleRate = sampleRate;
            _nFft = nFft;
            _hopLength = hopLength;
            _nMels = nMels;
            _window = CreateHannWindow(nFft);
            _melFilterbank = CreateMelFilterbank(sampleRate, nFft, nMels);
        }

        /// <summary>
        /// 音声波形からメルスペクトログラムを抽出
        /// </summary>
        /// <param name="audio">音声波形</param>
        /// <returns>メルスペクトログラム [numFrames, nMels]</returns>
        public float[,] ExtractMelSpectrogram(float[] audio)
        {
            if (audio == null || audio.Length == 0)
            {
                throw new ArgumentException("Audio cannot be null or empty", nameof(audio));
            }

            // フレーム数を計算
            int numFrames = (audio.Length - _nFft) / _hopLength + 1;
            if (numFrames <= 0)
            {
                numFrames = 1;
            }

            float[,] melSpec = new float[numFrames, _nMels];
            int numBins = _nFft / 2 + 1;

            float[] frame = new float[_nFft];
            float[] real = new float[_nFft];
            float[] imag = new float[_nFft];

            for (int f = 0; f < numFrames; f++)
            {
                int startSample = f * _hopLength;

                // フレームを切り出してウィンドウを適用
                for (int i = 0; i < _nFft; i++)
                {
                    int sampleIdx = startSample + i;
                    frame[i] = sampleIdx < audio.Length ? audio[sampleIdx] * _window[i] : 0f;
                }

                // FFTを実行
                FFT(frame, real, imag);

                // パワースペクトルを計算
                float[] powerSpec = new float[numBins];
                for (int bin = 0; bin < numBins; bin++)
                {
                    powerSpec[bin] = real[bin] * real[bin] + imag[bin] * imag[bin];
                }

                // メルフィルターバンクを適用
                for (int mel = 0; mel < _nMels; mel++)
                {
                    float sum = 0;
                    for (int bin = 0; bin < numBins; bin++)
                    {
                        sum += powerSpec[bin] * _melFilterbank[mel, bin];
                    }
                    // 対数スケールに変換
                    melSpec[f, mel] = Mathf.Log(Mathf.Max(sum, 1e-10f));
                }
            }

            return melSpec;
        }

        /// <summary>
        /// AudioClipからメルスペクトログラムを抽出
        /// </summary>
        /// <param name="clip">AudioClip</param>
        /// <returns>メルスペクトログラム [numFrames, nMels]</returns>
        public float[,] ExtractMelSpectrogram(AudioClip clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            // モノラルに変換
            if (clip.channels > 1)
            {
                samples = ConvertToMono(samples, clip.channels);
            }

            // リサンプリング（必要な場合）
            if (clip.frequency != _sampleRate)
            {
                samples = Resample(samples, clip.frequency, _sampleRate);
            }

            return ExtractMelSpectrogram(samples);
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
        /// メルフィルターバンクを作成
        /// </summary>
        private static float[,] CreateMelFilterbank(int sampleRate, int nFft, int nMels)
        {
            int numBins = nFft / 2 + 1;
            float[,] filterbank = new float[nMels, numBins];

            float fMin = 0f;
            float fMax = sampleRate / 2f;

            // メル周波数に変換
            float melMin = HzToMel(fMin);
            float melMax = HzToMel(fMax);

            // メルスケールで等間隔な点を作成
            float[] melPoints = new float[nMels + 2];
            for (int i = 0; i < nMels + 2; i++)
            {
                melPoints[i] = melMin + i * (melMax - melMin) / (nMels + 1);
            }

            // Hz周波数に戻す
            float[] hzPoints = new float[nMels + 2];
            for (int i = 0; i < nMels + 2; i++)
            {
                hzPoints[i] = MelToHz(melPoints[i]);
            }

            // FFTビンに変換
            int[] binPoints = new int[nMels + 2];
            for (int i = 0; i < nMels + 2; i++)
            {
                binPoints[i] = (int)Mathf.Floor((nFft + 1) * hzPoints[i] / sampleRate);
            }

            // 三角フィルターを作成
            for (int mel = 0; mel < nMels; mel++)
            {
                int startBin = binPoints[mel];
                int centerBin = binPoints[mel + 1];
                int endBin = binPoints[mel + 2];

                // 上り坂
                for (int bin = startBin; bin < centerBin; bin++)
                {
                    if (bin >= 0 && bin < numBins)
                    {
                        filterbank[mel, bin] = (float)(bin - startBin) / (centerBin - startBin);
                    }
                }

                // 下り坂
                for (int bin = centerBin; bin < endBin; bin++)
                {
                    if (bin >= 0 && bin < numBins)
                    {
                        filterbank[mel, bin] = (float)(endBin - bin) / (endBin - centerBin);
                    }
                }
            }

            return filterbank;
        }

        /// <summary>
        /// Hz→メル変換
        /// </summary>
        private static float HzToMel(float hz)
        {
            return 2595f * Mathf.Log10(1f + hz / 700f);
        }

        /// <summary>
        /// メル→Hz変換
        /// </summary>
        private static float MelToHz(float mel)
        {
            return 700f * (Mathf.Pow(10f, mel / 2595f) - 1f);
        }

        /// <summary>
        /// 簡易FFT
        /// </summary>
        private static void FFT(float[] input, float[] real, float[] imag)
        {
            int n = input.Length;

            // 簡易的なDFT実装
            for (int k = 0; k < n; k++)
            {
                real[k] = 0;
                imag[k] = 0;

                for (int t = 0; t < n; t++)
                {
                    float angle = -2f * Mathf.PI * t * k / n;
                    real[k] += input[t] * Mathf.Cos(angle);
                    imag[k] += input[t] * Mathf.Sin(angle);
                }
            }
        }

        /// <summary>
        /// ステレオ→モノラル変換
        /// </summary>
        private static float[] ConvertToMono(float[] stereo, int channels)
        {
            int length = stereo.Length / channels;
            float[] mono = new float[length];

            for (int i = 0; i < length; i++)
            {
                float sum = 0;
                for (int ch = 0; ch < channels; ch++)
                {
                    sum += stereo[i * channels + ch];
                }
                mono[i] = sum / channels;
            }

            return mono;
        }

        /// <summary>
        /// 線形補間によるリサンプリング
        /// </summary>
        private static float[] Resample(float[] input, int srcRate, int dstRate)
        {
            float ratio = (float)srcRate / dstRate;
            int outputLength = (int)(input.Length / ratio);
            float[] output = new float[outputLength];

            for (int i = 0; i < outputLength; i++)
            {
                float srcIndex = i * ratio;
                int index0 = (int)srcIndex;
                int index1 = Mathf.Min(index0 + 1, input.Length - 1);
                float frac = srcIndex - index0;

                output[i] = input[index0] * (1f - frac) + input[index1] * frac;
            }

            return output;
        }
    }
}
