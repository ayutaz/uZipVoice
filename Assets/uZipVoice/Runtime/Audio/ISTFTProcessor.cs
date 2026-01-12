using System;
using NWaves.Transforms;
using UnityEngine;

namespace uZipVoice.Audio
{
    /// <summary>
    /// パディングタイプ
    /// </summary>
    public enum ISTFTPadding
    {
        /// <summary>
        /// center padding - torch.istft(center=True)と同じ。出力をn_fft//2でトリム
        /// </summary>
        Center,

        /// <summary>
        /// same padding - カスタム実装。トリムなし
        /// </summary>
        Same
    }

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
        private readonly ISTFTPadding _padding;

        /// <summary>
        /// FFTサイズ
        /// </summary>
        public int NFft => _nFft;

        /// <summary>
        /// ホップ長
        /// </summary>
        public int HopLength => _hopLength;

        /// <summary>
        /// パディングタイプ
        /// </summary>
        public ISTFTPadding Padding => _padding;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="nFft">FFTサイズ（デフォルト: 1024）</param>
        /// <param name="hopLength">ホップ長（デフォルト: 256）</param>
        /// <param name="padding">パディングタイプ（デフォルト: Center - Vocosで使用）</param>
        public ISTFTProcessor(int nFft = 1024, int hopLength = 256, ISTFTPadding padding = ISTFTPadding.Center)
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
            _padding = padding;

            // Hann窓を作成（periodic=True相当: 分母にnFftを使用）
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

            // 出力波形のサイズを計算（center paddingの場合、後でトリムする）
            int fullLength = (numFrames - 1) * _hopLength + _nFft;
            float[] output = new float[fullLength];
            float[] windowSum = new float[fullLength];

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

                // NWaves FFTのIFFTは正規化しない（torch.fft.irfft(norm="backward")と同じ）
                // PyTorch istftの内部実装に合わせて、ここで1/nFftの正規化を適用
                float normFactor = 1.0f / _nFft;

                // 窓関数を適用してoverlap-add
                // PyTorch istftと同様: IFFT結果に窓関数を掛けてoverlap-add
                int frameStart = frame * _hopLength;
                for (int i = 0; i < _nFft; i++)
                {
                    int outIdx = frameStart + i;
                    if (outIdx < fullLength)
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
            // Hann窓で4倍オーバーラップ（hopLength=nFft/4）の場合、中央部分のwindowSumは約1.5
            float minWindowSum = 1e-8f;
            for (int i = 0; i < fullLength; i++)
            {
                if (windowSum[i] > minWindowSum)
                {
                    output[i] /= windowSum[i];
                }
            }

            // Center paddingの場合、出力をトリム
            // STFTでcenter=Trueの場合、入力の両端にn_fft//2のパディングが追加される
            // ISTFTではそのパディング分を削除する必要がある
            float[] result;
            if (_padding == ISTFTPadding.Center)
            {
                int pad = _nFft / 2;
                int trimmedLength = fullLength - 2 * pad;
                if (trimmedLength > 0)
                {
                    result = new float[trimmedLength];
                    Array.Copy(output, pad, result, 0, trimmedLength);
                    Debug.Log($"[ISTFTProcessor] ISTFT completed (center padding). Full length: {fullLength}, Trimmed length: {trimmedLength}, frames: {numFrames}");
                }
                else
                {
                    // トリム後の長さが0以下の場合は全出力を返す
                    result = output;
                    Debug.LogWarning($"[ISTFTProcessor] Trimmed length would be non-positive ({trimmedLength}). Returning full output.");
                }
            }
            else
            {
                result = output;
                Debug.Log($"[ISTFTProcessor] ISTFT completed (same padding). Output length: {fullLength}, frames: {numFrames}");
            }

            return result;
        }
    }
}
