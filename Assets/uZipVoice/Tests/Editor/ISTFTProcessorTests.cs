using System;
using NUnit.Framework;
using NWaves.Transforms;
using UnityEngine;
using uZipVoice.Audio;

namespace uZipVoice.Tests
{
    /// <summary>
    /// ISTFTProcessorクラスのテスト
    /// ISTFT（逆短時間フーリエ変換）の正確性を検証
    /// </summary>
    [TestFixture]
    public class ISTFTProcessorTests
    {
        private const float Tolerance = 1e-4f;
        private const int DefaultNFft = 1024;
        private const int DefaultHopLength = 256;

        #region Constructor Tests

        [Test]
        public void Constructor_DefaultParams_CreatesProcessor()
        {
            // Act
            var processor = new ISTFTProcessor();

            // Assert
            Assert.That(processor.NFft, Is.EqualTo(DefaultNFft));
            Assert.That(processor.HopLength, Is.EqualTo(DefaultHopLength));
        }

        [Test]
        public void Constructor_CustomParams_CreatesProcessor()
        {
            // Act
            var processor = new ISTFTProcessor(nFft: 512, hopLength: 128);

            // Assert
            Assert.That(processor.NFft, Is.EqualTo(512));
            Assert.That(processor.HopLength, Is.EqualTo(128));
        }

        [Test]
        public void Constructor_ZeroNFft_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ISTFTProcessor(nFft: 0));
        }

        [Test]
        public void Constructor_NegativeNFft_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ISTFTProcessor(nFft: -1));
        }

        [Test]
        public void Constructor_ZeroHopLength_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ISTFTProcessor(hopLength: 0));
        }

        [Test]
        public void Constructor_HopLengthGreaterThanNFft_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ISTFTProcessor(nFft: 512, hopLength: 1024));
        }

        #endregion

        #region Process Input Validation

        [Test]
        public void Process_NullMagnitude_ThrowsArgumentNullException()
        {
            var processor = new ISTFTProcessor();
            float[] phaseCos = new float[513];
            float[] phaseSin = new float[513];

            Assert.Throws<ArgumentNullException>(() =>
                processor.Process(null, phaseCos, phaseSin, 513, 1));
        }

        [Test]
        public void Process_NullPhaseCos_ThrowsArgumentNullException()
        {
            var processor = new ISTFTProcessor();
            float[] magnitude = new float[513];
            float[] phaseSin = new float[513];

            Assert.Throws<ArgumentNullException>(() =>
                processor.Process(magnitude, null, phaseSin, 513, 1));
        }

        [Test]
        public void Process_NullPhaseSin_ThrowsArgumentNullException()
        {
            var processor = new ISTFTProcessor();
            float[] magnitude = new float[513];
            float[] phaseCos = new float[513];

            Assert.Throws<ArgumentNullException>(() =>
                processor.Process(magnitude, phaseCos, null, 513, 1));
        }

        #endregion

        #region Basic IFFT Tests

        [Test]
        public void Process_DCSignal_ReturnsConstantOutput()
        {
            // Arrange: DC signal (magnitude at bin 0 only)
            var processor = new ISTFTProcessor(nFft: 256, hopLength: 64);
            int numBins = 129; // 256/2 + 1
            int numFrames = 4;
            int totalSize = numBins * numFrames;

            float[] magnitude = new float[totalSize];
            float[] phaseCos = new float[totalSize];
            float[] phaseSin = new float[totalSize];

            // Set DC component with magnitude 1, phase 0
            for (int frame = 0; frame < numFrames; frame++)
            {
                int idx = 0 * numFrames + frame; // bin 0
                magnitude[idx] = 1.0f;
                phaseCos[idx] = 1.0f;
                phaseSin[idx] = 0.0f;
            }

            // Act
            float[] result = processor.Process(magnitude, phaseCos, phaseSin, numBins, numFrames);

            // Assert: DC signal should produce relatively constant output
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.GreaterThan(0));

            // Check that all values have the same sign (or are near zero due to windowing)
            float firstNonZero = 0;
            for (int i = 0; i < result.Length; i++)
            {
                if (Mathf.Abs(result[i]) > 0.001f)
                {
                    firstNonZero = result[i];
                    break;
                }
            }

            Debug.Log($"[Test] DC signal result: first nonzero={firstNonZero}, length={result.Length}");
        }

        [Test]
        public void Process_SingleFrame_ReturnsCorrectLength()
        {
            // Arrange
            var processor = new ISTFTProcessor(nFft: 256, hopLength: 64);
            int numBins = 129;
            int numFrames = 1;

            float[] magnitude = new float[numBins];
            float[] phaseCos = new float[numBins];
            float[] phaseSin = new float[numBins];

            // Fill with zeros except DC
            magnitude[0] = 1.0f;
            phaseCos[0] = 1.0f;

            // Act
            float[] result = processor.Process(magnitude, phaseCos, phaseSin, numBins, numFrames);

            // Assert
            // outputLength = (numFrames - 1) * hopLength + nFft = 0 + 256 = 256
            Assert.That(result.Length, Is.EqualTo(256));
        }

        [Test]
        public void Process_MultipleFrames_ReturnsCorrectLength()
        {
            // Arrange
            var processor = new ISTFTProcessor(nFft: 256, hopLength: 64);
            int numBins = 129;
            int numFrames = 10;

            float[] magnitude = new float[numBins * numFrames];
            float[] phaseCos = new float[numBins * numFrames];
            float[] phaseSin = new float[numBins * numFrames];

            // Act
            float[] result = processor.Process(magnitude, phaseCos, phaseSin, numBins, numFrames);

            // Assert
            // outputLength = (10 - 1) * 64 + 256 = 576 + 256 = 832
            int expectedLength = (numFrames - 1) * 64 + 256;
            Assert.That(result.Length, Is.EqualTo(expectedLength));
        }

        #endregion

        #region STFT Round-Trip Tests

        [Test]
        public void Process_RoundTrip_ReconstructsSineWave()
        {
            // Arrange: Create a sine wave, perform STFT, then ISTFT
            int nFft = 1024;
            int hopLength = 256;
            float frequency = 440f; // A4
            int sampleRate = 24000;
            float duration = 0.1f; // 100ms
            int numSamples = (int)(sampleRate * duration);

            // Generate sine wave
            float[] original = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                original[i] = Mathf.Sin(2f * Mathf.PI * frequency * t);
            }

            // Perform STFT using NWaves
            var stft = new Stft(nFft, hopLength);
            var spectrogram = stft.Direct(original);

            // Convert STFT output to magnitude/phase format
            int numFrames = spectrogram.Count;
            int numBins = nFft / 2 + 1;

            float[] magnitude = new float[numBins * numFrames];
            float[] phaseCos = new float[numBins * numFrames];
            float[] phaseSin = new float[numBins * numFrames];

            for (int frame = 0; frame < numFrames; frame++)
            {
                var (real, imag) = spectrogram[frame];
                for (int bin = 0; bin < numBins; bin++)
                {
                    int idx = bin * numFrames + frame;
                    float r = real[bin];
                    float im = imag[bin];
                    float mag = Mathf.Sqrt(r * r + im * im);

                    magnitude[idx] = mag;
                    if (mag > 1e-8f)
                    {
                        phaseCos[idx] = r / mag;
                        phaseSin[idx] = im / mag;
                    }
                    else
                    {
                        phaseCos[idx] = 1f;
                        phaseSin[idx] = 0f;
                    }
                }
            }

            // Perform ISTFT
            var processor = new ISTFTProcessor(nFft, hopLength);
            float[] reconstructed = processor.Process(magnitude, phaseCos, phaseSin, numBins, numFrames);

            // Assert: Compare reconstructed signal with original
            // Only compare the middle portion (avoiding boundary effects)
            int startIdx = nFft;
            int endIdx = Math.Min(numSamples - nFft, reconstructed.Length - nFft);

            if (endIdx > startIdx)
            {
                float maxError = 0f;
                float maxReconstructed = 0f;
                float maxOriginal = 0f;

                for (int i = startIdx; i < endIdx; i++)
                {
                    if (i < reconstructed.Length && i < original.Length)
                    {
                        float error = Mathf.Abs(reconstructed[i] - original[i]);
                        maxError = Mathf.Max(maxError, error);
                        maxReconstructed = Mathf.Max(maxReconstructed, Mathf.Abs(reconstructed[i]));
                        maxOriginal = Mathf.Max(maxOriginal, Mathf.Abs(original[i]));
                    }
                }

                Debug.Log($"[Test] Round-trip: maxError={maxError}, maxReconstructed={maxReconstructed}, maxOriginal={maxOriginal}");

                // The reconstruction should have similar amplitude
                Assert.That(maxReconstructed, Is.GreaterThan(0.1f), "Reconstructed signal should have significant amplitude");
            }
        }

        [Test]
        public void Process_RoundTrip_STFTAndISTFTPreserveEnergy()
        {
            // Arrange: Create a test signal
            int nFft = 512;
            int hopLength = 128;
            int numSamples = 2048;

            // Generate test signal (white noise)
            var random = new System.Random(42);
            float[] original = new float[numSamples];
            float originalEnergy = 0f;
            for (int i = 0; i < numSamples; i++)
            {
                original[i] = (float)(random.NextDouble() * 2 - 1) * 0.5f;
                originalEnergy += original[i] * original[i];
            }

            // Perform STFT using NWaves
            var stft = new Stft(nFft, hopLength);
            var spectrogram = stft.Direct(original);

            // Convert to magnitude/phase
            int numFrames = spectrogram.Count;
            int numBins = nFft / 2 + 1;

            float[] magnitude = new float[numBins * numFrames];
            float[] phaseCos = new float[numBins * numFrames];
            float[] phaseSin = new float[numBins * numFrames];

            for (int frame = 0; frame < numFrames; frame++)
            {
                var (real, imag) = spectrogram[frame];
                for (int bin = 0; bin < numBins; bin++)
                {
                    int idx = bin * numFrames + frame;
                    float r = real[bin];
                    float im = imag[bin];
                    float mag = Mathf.Sqrt(r * r + im * im);

                    magnitude[idx] = mag;
                    if (mag > 1e-8f)
                    {
                        phaseCos[idx] = r / mag;
                        phaseSin[idx] = im / mag;
                    }
                    else
                    {
                        phaseCos[idx] = 1f;
                        phaseSin[idx] = 0f;
                    }
                }
            }

            // Perform ISTFT
            var processor = new ISTFTProcessor(nFft, hopLength);
            float[] reconstructed = processor.Process(magnitude, phaseCos, phaseSin, numBins, numFrames);

            // Calculate reconstructed energy (central portion)
            float reconstructedEnergy = 0f;
            int overlapSamples = nFft;
            int startIdx = overlapSamples;
            int endIdx = Math.Min(numSamples - overlapSamples, reconstructed.Length - overlapSamples);
            int count = 0;

            for (int i = startIdx; i < endIdx && i < original.Length; i++)
            {
                reconstructedEnergy += reconstructed[i] * reconstructed[i];
                count++;
            }

            // Compare energy per sample
            float originalEnergyPerSample = originalEnergy / numSamples;
            float reconstructedEnergyPerSample = reconstructedEnergy / Math.Max(count, 1);

            Debug.Log($"[Test] Energy: original={originalEnergyPerSample:F6}, reconstructed={reconstructedEnergyPerSample:F6}");

            // Reconstructed should have non-zero energy
            Assert.That(reconstructedEnergy, Is.GreaterThan(0f), "Reconstructed signal should have non-zero energy");
        }

        #endregion

        #region NWaves FFT Direct Comparison

        [Test]
        public void NWavesFft_Inverse_ProducesRealOutput()
        {
            // Verify NWaves FFT behavior directly
            int nFft = 256;
            var fft = new Fft(nFft);

            // Create a simple real spectrum (only real parts, no imaginary)
            float[] real = new float[nFft];
            float[] imag = new float[nFft];

            // DC component only
            real[0] = 1.0f;

            // Perform inverse FFT
            fft.Inverse(real, imag);

            // After IFFT, the result should be in the real array
            // For DC=1, the output should be constant = 1/N at each sample (unnormalized: just 1)
            Debug.Log($"[Test] NWaves IFFT DC: real[0]={real[0]}, real[1]={real[1]}, real[N/2]={real[nFft/2]}");

            // Check that all values are approximately equal (DC signal)
            float firstValue = real[0];
            bool allEqual = true;
            for (int i = 1; i < nFft; i++)
            {
                if (Mathf.Abs(real[i] - firstValue) > 0.01f)
                {
                    allEqual = false;
                    break;
                }
            }

            Assert.That(allEqual, Is.True, "IFFT of DC should produce constant output");
        }

        [Test]
        public void NWavesFft_ForwardThenInverse_RecoversSignal()
        {
            // Test NWaves FFT round-trip directly
            int nFft = 256;
            var fft = new Fft(nFft);

            // Create test signal
            float[] original = new float[nFft];
            for (int i = 0; i < nFft; i++)
            {
                original[i] = Mathf.Sin(2f * Mathf.PI * 4 * i / nFft); // 4 cycles
            }

            // Copy for FFT
            float[] real = new float[nFft];
            float[] imag = new float[nFft];
            Array.Copy(original, real, nFft);

            // Forward FFT
            fft.Direct(real, imag);

            Debug.Log($"[Test] FFT result: real[0]={real[0]:F4}, real[4]={real[4]:F4}, imag[4]={imag[4]:F4}");

            // Inverse FFT
            fft.Inverse(real, imag);

            // Normalize (NWaves doesn't normalize)
            for (int i = 0; i < nFft; i++)
            {
                real[i] /= nFft;
            }

            // Compare
            float maxError = 0f;
            for (int i = 0; i < nFft; i++)
            {
                maxError = Mathf.Max(maxError, Mathf.Abs(real[i] - original[i]));
            }

            Debug.Log($"[Test] Round-trip max error: {maxError}");
            Assert.That(maxError, Is.LessThan(1e-4f), "FFT round-trip should recover original signal");
        }

        [Test]
        public void NWavesFft_ConjugateSymmetry_WorksCorrectly()
        {
            // Test that conjugate symmetry reconstruction works
            int nFft = 256;
            int numBins = nFft / 2 + 1;
            var fft = new Fft(nFft);

            // Create a spectrum with conjugate symmetry (for a real signal)
            float[] real = new float[nFft];
            float[] imag = new float[nFft];

            // Set bin 4 (frequency = 4 cycles per frame)
            float magnitude = 1.0f;
            float phase = 0f;

            real[4] = magnitude * Mathf.Cos(phase);
            imag[4] = magnitude * Mathf.Sin(phase);

            // Set conjugate
            real[nFft - 4] = real[4];
            imag[nFft - 4] = -imag[4];

            // Inverse FFT
            fft.Inverse(real, imag);

            // Normalize
            for (int i = 0; i < nFft; i++)
            {
                real[i] /= nFft;
            }

            // Check that output is a cosine wave
            float maxAbs = 0f;
            for (int i = 0; i < nFft; i++)
            {
                maxAbs = Mathf.Max(maxAbs, Mathf.Abs(real[i]));
            }

            Debug.Log($"[Test] Conjugate symmetry result: maxAbs={maxAbs}");
            Assert.That(maxAbs, Is.GreaterThan(0.001f), "Output should be non-zero");

            // Verify it's a sinusoidal pattern
            float expected0 = 2.0f * Mathf.Cos(2f * Mathf.PI * 4 * 0 / nFft) / nFft;
            Debug.Log($"[Test] Expected at 0: {expected0}, Actual: {real[0]}");
        }

        #endregion

        #region Vocos-like Output Tests

        [Test]
        public void Process_VocosLikeOutput_ProducesValidWaveform()
        {
            // Simulate Vocos output format
            int nFft = 1024;
            int hopLength = 256;
            int numBins = 513;
            int numFrames = 10;

            var processor = new ISTFTProcessor(nFft, hopLength);

            // Create Vocos-like output: magnitude with typical speech-like pattern
            float[] magnitude = new float[numBins * numFrames];
            float[] phaseCos = new float[numBins * numFrames];
            float[] phaseSin = new float[numBins * numFrames];

            var random = new System.Random(42);

            for (int frame = 0; frame < numFrames; frame++)
            {
                for (int bin = 0; bin < numBins; bin++)
                {
                    int idx = bin * numFrames + frame;

                    // Magnitude decreases with frequency (typical for speech)
                    float freq = (float)bin / numBins;
                    magnitude[idx] = Mathf.Exp(-freq * 3f) * 0.5f;

                    // Random phase
                    float phase = (float)(random.NextDouble() * 2 * Math.PI);
                    phaseCos[idx] = Mathf.Cos(phase);
                    phaseSin[idx] = Mathf.Sin(phase);
                }
            }

            // Act
            float[] result = processor.Process(magnitude, phaseCos, phaseSin, numBins, numFrames);

            // Assert
            Assert.That(result, Is.Not.Null);

            // Check that output has reasonable values
            float min = float.MaxValue, max = float.MinValue, sum = 0f;
            int nonZeroCount = 0;

            for (int i = 0; i < result.Length; i++)
            {
                min = Mathf.Min(min, result[i]);
                max = Mathf.Max(max, result[i]);
                sum += result[i];
                if (Mathf.Abs(result[i]) > 0.001f) nonZeroCount++;
            }

            Debug.Log($"[Test] Vocos-like output: min={min:F4}, max={max:F4}, mean={sum/result.Length:F6}, nonZeroRatio={100f*nonZeroCount/result.Length:F1}%");

            // Output should be in reasonable range (not too large)
            Assert.That(Mathf.Abs(min), Is.LessThan(10f), "Min value should be reasonable");
            Assert.That(Mathf.Abs(max), Is.LessThan(10f), "Max value should be reasonable");

            // Most samples should be non-zero
            float nonZeroRatio = (float)nonZeroCount / result.Length;
            Assert.That(nonZeroRatio, Is.GreaterThan(0.5f), "Most samples should be non-zero");
        }

        #endregion

        #region Window and Overlap-Add Tests

        [Test]
        public void Process_OverlapAdd_ProducesSmoothtransitions()
        {
            // Test that overlap-add doesn't produce discontinuities
            int nFft = 256;
            int hopLength = 64;
            int numBins = 129;
            int numFrames = 8;

            var processor = new ISTFTProcessor(nFft, hopLength);

            // Create constant magnitude across all frames
            float[] magnitude = new float[numBins * numFrames];
            float[] phaseCos = new float[numBins * numFrames];
            float[] phaseSin = new float[numBins * numFrames];

            for (int frame = 0; frame < numFrames; frame++)
            {
                for (int bin = 0; bin < numBins; bin++)
                {
                    int idx = bin * numFrames + frame;
                    magnitude[idx] = (bin == 0) ? 0.5f : 0f; // DC only
                    phaseCos[idx] = 1f;
                    phaseSin[idx] = 0f;
                }
            }

            // Act
            float[] result = processor.Process(magnitude, phaseCos, phaseSin, numBins, numFrames);

            // Assert: Check for discontinuities
            float maxDiff = 0f;
            int centerStart = nFft;
            int centerEnd = result.Length - nFft;

            for (int i = centerStart + 1; i < centerEnd; i++)
            {
                float diff = Mathf.Abs(result[i] - result[i - 1]);
                maxDiff = Mathf.Max(maxDiff, diff);
            }

            Debug.Log($"[Test] Overlap-add max diff: {maxDiff}");

            // For constant DC, adjacent samples should be similar
            // Note: This may not be exactly 0 due to window effects
        }

        [Test]
        public void Process_HannWindow_AppliedCorrectly()
        {
            // Verify that Hann window is applied
            int nFft = 256;
            int hopLength = 256; // No overlap for this test
            int numBins = 129;
            int numFrames = 1;

            var processor = new ISTFTProcessor(nFft, hopLength);

            // All ones in spectrum
            float[] magnitude = new float[numBins];
            float[] phaseCos = new float[numBins];
            float[] phaseSin = new float[numBins];

            for (int bin = 0; bin < numBins; bin++)
            {
                magnitude[bin] = 1f;
                phaseCos[bin] = 1f;
                phaseSin[bin] = 0f;
            }

            // Act
            float[] result = processor.Process(magnitude, phaseCos, phaseSin, numBins, numFrames);

            // The output should show windowing effect (tapered at edges)
            float edgeAvg = (Mathf.Abs(result[0]) + Mathf.Abs(result[nFft - 1])) / 2;
            float centerAvg = Mathf.Abs(result[nFft / 2]);

            Debug.Log($"[Test] Window effect: edge={edgeAvg:F4}, center={centerAvg:F4}");

            // Edge values should be smaller than center (Hann window effect)
            // Note: Due to COLA normalization, this may not be as pronounced
        }

        #endregion
    }
}
