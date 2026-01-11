using System;
using NUnit.Framework;
using UnityEngine;
using uZipVoice.Audio;

namespace uZipVoice.Tests
{
    /// <summary>
    /// FeatureExtractor のユニットテスト
    /// </summary>
    public class FeatureExtractorTests
    {
        private const int DefaultSampleRate = 24000;
        private const int DefaultNFft = 1024;
        private const int DefaultHopLength = 256;
        private const int DefaultNMels = 100;

        #region Constructor Tests

        [Test]
        public void Constructor_DefaultParams_CreatesExtractor()
        {
            var extractor = new FeatureExtractor();

            Assert.AreEqual(DefaultSampleRate, extractor.SampleRate);
            Assert.AreEqual(DefaultNFft, extractor.NFft);
            Assert.AreEqual(DefaultHopLength, extractor.HopLength);
            Assert.AreEqual(DefaultNMels, extractor.NMels);
        }

        [Test]
        public void Constructor_CustomParams_CreatesExtractor()
        {
            var extractor = new FeatureExtractor(22050, 512, 128, 80);

            Assert.AreEqual(22050, extractor.SampleRate);
            Assert.AreEqual(512, extractor.NFft);
            Assert.AreEqual(128, extractor.HopLength);
            Assert.AreEqual(80, extractor.NMels);
        }

        [Test]
        public void Constructor_ZeroSampleRate_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FeatureExtractor(0));
        }

        [Test]
        public void Constructor_NegativeNFft_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FeatureExtractor(24000, -1));
        }

        [Test]
        public void Constructor_ZeroHopLength_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FeatureExtractor(24000, 1024, 0));
        }

        [Test]
        public void Constructor_ZeroNMels_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FeatureExtractor(24000, 1024, 256, 0));
        }

        #endregion

        #region ExtractMelSpectrogram Tests

        [Test]
        public void ExtractMelSpectrogram_NullAudio_ThrowsArgumentException()
        {
            var extractor = new FeatureExtractor();

            Assert.Throws<ArgumentException>(() => extractor.ExtractMelSpectrogram((float[])null));
        }

        [Test]
        public void ExtractMelSpectrogram_EmptyAudio_ThrowsArgumentException()
        {
            var extractor = new FeatureExtractor();

            Assert.Throws<ArgumentException>(() => extractor.ExtractMelSpectrogram(new float[0]));
        }

        [Test]
        public void ExtractMelSpectrogram_ShortAudio_ReturnsAtLeastOneFrame()
        {
            var extractor = new FeatureExtractor();
            float[] audio = new float[100]; // Very short audio

            var melSpec = extractor.ExtractMelSpectrogram(audio);

            Assert.IsNotNull(melSpec);
            Assert.GreaterOrEqual(melSpec.GetLength(0), 1); // At least one frame
            Assert.AreEqual(DefaultNMels, melSpec.GetLength(1));
        }

        [Test]
        public void ExtractMelSpectrogram_OneSecondAudio_ReturnsCorrectFrameCount()
        {
            var extractor = new FeatureExtractor();
            int oneSecondSamples = DefaultSampleRate;
            float[] audio = new float[oneSecondSamples];

            // Fill with random noise
            var random = new System.Random(42);
            for (int i = 0; i < audio.Length; i++)
            {
                audio[i] = (float)(random.NextDouble() * 2 - 1);
            }

            var melSpec = extractor.ExtractMelSpectrogram(audio);

            // Expected frames: (samples - nFft) / hopLength + 1
            int expectedFrames = (oneSecondSamples - DefaultNFft) / DefaultHopLength + 1;
            Assert.AreEqual(expectedFrames, melSpec.GetLength(0));
            Assert.AreEqual(DefaultNMels, melSpec.GetLength(1));
        }

        [Test]
        public void ExtractMelSpectrogram_SineWave_ReturnsValidMelValues()
        {
            var extractor = new FeatureExtractor();
            int samples = DefaultSampleRate; // 1 second
            float[] audio = new float[samples];

            // Generate 440Hz sine wave
            float frequency = 440f;
            for (int i = 0; i < samples; i++)
            {
                audio[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / DefaultSampleRate);
            }

            var melSpec = extractor.ExtractMelSpectrogram(audio);

            Assert.IsNotNull(melSpec);
            Assert.Greater(melSpec.GetLength(0), 0);

            // Check that values are finite (not NaN or Inf)
            for (int f = 0; f < melSpec.GetLength(0); f++)
            {
                for (int m = 0; m < melSpec.GetLength(1); m++)
                {
                    Assert.IsFalse(float.IsNaN(melSpec[f, m]), $"NaN at frame {f}, mel {m}");
                    Assert.IsFalse(float.IsInfinity(melSpec[f, m]), $"Inf at frame {f}, mel {m}");
                }
            }
        }

        [Test]
        public void ExtractMelSpectrogram_SilentAudio_ReturnsLowMelValues()
        {
            var extractor = new FeatureExtractor();
            float[] audio = new float[DefaultSampleRate]; // 1 second of silence

            var melSpec = extractor.ExtractMelSpectrogram(audio);

            // For silent audio, mel values should be very low (log of near-zero)
            float maxMel = float.MinValue;
            for (int f = 0; f < melSpec.GetLength(0); f++)
            {
                for (int m = 0; m < melSpec.GetLength(1); m++)
                {
                    maxMel = Mathf.Max(maxMel, melSpec[f, m]);
                }
            }

            // Log(1e-10) ≈ -23, so silent audio should have very negative values
            Assert.Less(maxMel, -10f, "Silent audio should have low mel values");
        }

        [Test]
        public void ExtractMelSpectrogram_NWavesFFT_MatchesExpectedOutput()
        {
            // This test verifies that NWaves FFT produces reasonable output
            var extractor = new FeatureExtractor();
            int samples = DefaultSampleRate; // 1 second
            float[] audio = new float[samples];

            // Generate a simple test signal: 1kHz sine wave
            float frequency = 1000f;
            for (int i = 0; i < samples; i++)
            {
                audio[i] = 0.5f * Mathf.Sin(2f * Mathf.PI * frequency * i / DefaultSampleRate);
            }

            var melSpec = extractor.ExtractMelSpectrogram(audio);

            // The mel bin corresponding to 1kHz should have higher energy
            // than very low or very high frequencies
            int midFrame = melSpec.GetLength(0) / 2;

            // Find the mel bin with max energy in the middle frame
            float maxEnergy = float.MinValue;
            int maxMelBin = 0;
            for (int m = 0; m < melSpec.GetLength(1); m++)
            {
                if (melSpec[midFrame, m] > maxEnergy)
                {
                    maxEnergy = melSpec[midFrame, m];
                    maxMelBin = m;
                }
            }

            // 1kHz should be in the lower-mid mel bins (roughly 20-50 range for 100 mel bins)
            Assert.Greater(maxMelBin, 10, "1kHz peak should not be in very low mel bins");
            Assert.Less(maxMelBin, 70, "1kHz peak should not be in very high mel bins");
        }

        [Test]
        public void ExtractMelSpectrogram_DifferentFrequencies_ProduceDifferentPeaks()
        {
            var extractor = new FeatureExtractor();
            int samples = DefaultSampleRate;

            // Generate 500Hz audio
            float[] audio500Hz = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                audio500Hz[i] = 0.5f * Mathf.Sin(2f * Mathf.PI * 500f * i / DefaultSampleRate);
            }

            // Generate 2000Hz audio
            float[] audio2000Hz = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                audio2000Hz[i] = 0.5f * Mathf.Sin(2f * Mathf.PI * 2000f * i / DefaultSampleRate);
            }

            var melSpec500 = extractor.ExtractMelSpectrogram(audio500Hz);
            var melSpec2000 = extractor.ExtractMelSpectrogram(audio2000Hz);

            // Find peak mel bin for each frequency
            int midFrame = melSpec500.GetLength(0) / 2;

            int peak500 = FindPeakMelBin(melSpec500, midFrame);
            int peak2000 = FindPeakMelBin(melSpec2000, midFrame);

            // 2000Hz should peak at a higher mel bin than 500Hz
            Assert.Greater(peak2000, peak500, "Higher frequency should peak at higher mel bin");
        }

        private int FindPeakMelBin(float[,] melSpec, int frame)
        {
            float maxEnergy = float.MinValue;
            int maxBin = 0;
            for (int m = 0; m < melSpec.GetLength(1); m++)
            {
                if (melSpec[frame, m] > maxEnergy)
                {
                    maxEnergy = melSpec[frame, m];
                    maxBin = m;
                }
            }
            return maxBin;
        }

        #endregion

        #region AudioClip Tests

        [Test]
        public void ExtractMelSpectrogram_NullClip_ThrowsArgumentNullException()
        {
            var extractor = new FeatureExtractor();

            Assert.Throws<ArgumentNullException>(() => extractor.ExtractMelSpectrogram((AudioClip)null));
        }

        #endregion

        #region Performance Tests

        [Test]
        public void ExtractMelSpectrogram_LongAudio_CompletesInReasonableTime()
        {
            var extractor = new FeatureExtractor();

            // 10 seconds of audio at 24kHz = 240,000 samples
            int tenSecondsSamples = DefaultSampleRate * 10;
            float[] audio = new float[tenSecondsSamples];

            // Fill with random noise
            var random = new System.Random(42);
            for (int i = 0; i < audio.Length; i++)
            {
                audio[i] = (float)(random.NextDouble() * 2 - 1);
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var melSpec = extractor.ExtractMelSpectrogram(audio);
            stopwatch.Stop();

            // Should complete in less than 5 seconds (with NWaves FFT)
            Assert.Less(stopwatch.ElapsedMilliseconds, 5000,
                $"Extraction took too long: {stopwatch.ElapsedMilliseconds}ms");

            // Verify output is valid
            Assert.IsNotNull(melSpec);
            Assert.Greater(melSpec.GetLength(0), 0);
        }

        #endregion
    }
}
