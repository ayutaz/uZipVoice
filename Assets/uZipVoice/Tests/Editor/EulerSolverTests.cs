using System;
using NUnit.Framework;
using uZipVoice.Inference;

namespace uZipVoice.Tests
{
    /// <summary>
    /// EulerSolverクラスのテスト
    /// </summary>
    [TestFixture]
    public class EulerSolverTests
    {
        private const float Tolerance = 1e-5f;

        #region ES-001: GetTimesteps_DefaultParams

        [Test]
        public void GetTimesteps_DefaultParams_ReturnsCorrectLength()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 10);

            // Act
            float[] timesteps = solver.GetTimesteps();

            // Assert
            Assert.That(timesteps.Length, Is.EqualTo(11)); // numSteps + 1
        }

        [Test]
        public void GetTimesteps_DefaultParams_StartsAtZero()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 10);

            // Act
            float[] timesteps = solver.GetTimesteps();

            // Assert
            Assert.That(timesteps[0], Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void GetTimesteps_DefaultParams_EndsAtOne()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 10);

            // Act
            float[] timesteps = solver.GetTimesteps();

            // Assert
            Assert.That(timesteps[^1], Is.EqualTo(1f).Within(Tolerance));
        }

        #endregion

        #region ES-002: GetTimesteps_NumSteps8

        [Test]
        public void GetTimesteps_NumSteps8_Returns9Elements()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 8);

            // Act
            float[] timesteps = solver.GetTimesteps();

            // Assert
            Assert.That(timesteps.Length, Is.EqualTo(9));
        }

        #endregion

        #region ES-003: GetTimesteps_NumSteps16

        [Test]
        public void GetTimesteps_NumSteps16_Returns17Elements()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 16);

            // Act
            float[] timesteps = solver.GetTimesteps();

            // Assert
            Assert.That(timesteps.Length, Is.EqualTo(17));
        }

        #endregion

        #region ES-004: GetTimesteps_TShift0_5

        [Test]
        public void GetTimesteps_TShift0_5_ReturnsNonLinearTimesteps()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 4, tShift: 0.5f);

            // Act
            float[] timesteps = solver.GetTimesteps();

            // Assert
            // t_shifted = 0.5 * t / (1 + (0.5 - 1) * t) = 0.5 * t / (1 - 0.5 * t)
            // t=0.0: 0.5*0/(1-0) = 0
            // t=0.25: 0.5*0.25/(1-0.125) = 0.125/0.875 ≈ 0.1429
            // t=0.5: 0.5*0.5/(1-0.25) = 0.25/0.75 ≈ 0.333
            // t=0.75: 0.5*0.75/(1-0.375) = 0.375/0.625 = 0.6
            // t=1.0: 0.5*1/(1-0.5) = 0.5/0.5 = 1.0

            Assert.That(timesteps[0], Is.EqualTo(0f).Within(Tolerance));
            Assert.That(timesteps[1], Is.EqualTo(0.1428571f).Within(0.001f));
            Assert.That(timesteps[2], Is.EqualTo(0.3333333f).Within(0.001f));
            Assert.That(timesteps[3], Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(timesteps[4], Is.EqualTo(1f).Within(Tolerance));
        }

        #endregion

        #region ES-005: GetTimesteps_TShift1_0

        [Test]
        public void GetTimesteps_TShift1_0_ReturnsLinearTimesteps()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 4, tShift: 1.0f);

            // Act
            float[] timesteps = solver.GetTimesteps();

            // Assert
            // t_shift = 1.0 means linear: t_shifted = t
            Assert.That(timesteps[0], Is.EqualTo(0f).Within(Tolerance));
            Assert.That(timesteps[1], Is.EqualTo(0.25f).Within(Tolerance));
            Assert.That(timesteps[2], Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(timesteps[3], Is.EqualTo(0.75f).Within(Tolerance));
            Assert.That(timesteps[4], Is.EqualTo(1f).Within(Tolerance));
        }

        #endregion

        #region ES-006: GetTimesteps_StartEnd

        [Test]
        public void GetTimesteps_StartEnd_FirstIsZero()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 8);

            // Act
            float first = solver.GetTimestep(0);

            // Assert
            Assert.That(first, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(solver.TStart, Is.EqualTo(0f));
        }

        [Test]
        public void GetTimesteps_StartEnd_LastIsOne()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 8);

            // Act
            float last = solver.GetTimestep(8);

            // Assert
            Assert.That(last, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(solver.TEnd, Is.EqualTo(1f));
        }

        #endregion

        #region ES-007: GetTimesteps_Monotonic

        [Test]
        public void GetTimesteps_Monotonic_IsStrictlyIncreasing()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 16, tShift: 0.5f);

            // Act
            float[] timesteps = solver.GetTimesteps();

            // Assert
            Assert.That(TestUtility.IsMonotonicallyIncreasing(timesteps), Is.True);
        }

        [Test]
        public void GetTimesteps_Monotonic_AllDtPositive()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 16, tShift: 0.5f);

            // Act & Assert
            for (int i = 0; i < solver.NumSteps; i++)
            {
                float dt = solver.GetDt(i);
                Assert.That(dt, Is.GreaterThan(0f), $"dt at step {i} should be positive");
            }
        }

        #endregion

        #region ES-008: Step_SingleStep

        [Test]
        public void Step_SingleStep_UpdatesCorrectly()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 4, tShift: 1.0f); // Linear for simplicity
            float[] x = { 0f, 0f, 0f };
            float[] velocity = { 1f, 2f, 3f };
            float expectedDt = 0.25f; // Linear with 4 steps

            // Act
            float[] result = solver.Step(x, velocity, stepIndex: 0);

            // Assert
            Assert.That(result[0], Is.EqualTo(expectedDt * 1f).Within(Tolerance));
            Assert.That(result[1], Is.EqualTo(expectedDt * 2f).Within(Tolerance));
            Assert.That(result[2], Is.EqualTo(expectedDt * 3f).Within(Tolerance));
        }

        [Test]
        public void Step_SingleStep_DoesNotModifyInput()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 4);
            float[] x = { 1f, 2f, 3f };
            float[] originalX = { 1f, 2f, 3f };
            float[] velocity = { 1f, 1f, 1f };

            // Act
            solver.Step(x, velocity, stepIndex: 0);

            // Assert
            Assert.That(x, Is.EqualTo(originalX));
        }

        #endregion

        #region ES-009: Step_FullIntegration

        [Test]
        public void Step_FullIntegration_ConvergesToExpectedValue()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 100, tShift: 1.0f);
            float[] x = { 0f };
            // Constant velocity = 1 means x should go from 0 to 1
            float[] velocity = { 1f };

            // Act
            for (int step = 0; step < solver.NumSteps; step++)
            {
                x = solver.Step(x, velocity, step);
            }

            // Assert
            // With 100 steps and constant velocity 1, x should be close to 1
            Assert.That(x[0], Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void StepInPlace_FullIntegration_ConvergesToExpectedValue()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 100, tShift: 1.0f);
            float[] x = { 0f };
            float[] velocity = { 1f };

            // Act
            for (int step = 0; step < solver.NumSteps; step++)
            {
                solver.StepInPlace(x, velocity, step);
            }

            // Assert
            Assert.That(x[0], Is.EqualTo(1f).Within(0.01f));
        }

        #endregion

        #region ES-010: Constructor_InvalidNumSteps

        [Test]
        public void Constructor_ZeroSteps_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new EulerSolver(numSteps: 0));
        }

        [Test]
        public void Constructor_NegativeSteps_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new EulerSolver(numSteps: -1));
        }

        [Test]
        public void Constructor_InvalidTShift_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new EulerSolver(numSteps: 10, tShift: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EulerSolver(numSteps: 10, tShift: -0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EulerSolver(numSteps: 10, tShift: 1.5f));
        }

        [Test]
        public void Constructor_InvalidTimeRange_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new EulerSolver(numSteps: 10, tShift: 0.5f, tStart: 1f, tEnd: 0f));
            Assert.Throws<ArgumentException>(() => new EulerSolver(numSteps: 10, tShift: 0.5f, tStart: 0.5f, tEnd: 0.5f));
        }

        #endregion

        #region Additional Tests

        [Test]
        public void GetTimestep_ValidIndex_ReturnsValue()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 4);

            // Act & Assert
            for (int i = 0; i <= 4; i++)
            {
                Assert.DoesNotThrow(() => solver.GetTimestep(i));
            }
        }

        [Test]
        public void GetTimestep_InvalidIndex_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 4);

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => solver.GetTimestep(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => solver.GetTimestep(5));
        }

        [Test]
        public void GetDt_InvalidStepIndex_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 4);

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => solver.GetDt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => solver.GetDt(4));
        }

        [Test]
        public void Step_NullX_ThrowsArgumentNullException()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 4);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => solver.Step(null, new float[] { 1f }, 0));
        }

        [Test]
        public void Step_NullVelocity_ThrowsArgumentNullException()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 4);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => solver.Step(new float[] { 1f }, null, 0));
        }

        [Test]
        public void Step_MismatchedLengths_ThrowsArgumentException()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 4);
            float[] x = { 1f, 2f, 3f };
            float[] velocity = { 1f, 2f };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => solver.Step(x, velocity, 0));
        }

        [Test]
        public void NumSteps_ReturnsConstructorValue()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 16);

            // Assert
            Assert.That(solver.NumSteps, Is.EqualTo(16));
        }

        [Test]
        public void TShift_ReturnsConstructorValue()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 8, tShift: 0.7f);

            // Assert
            Assert.That(solver.TShift, Is.EqualTo(0.7f));
        }

        [Test]
        public void GetTimesteps_ReturnsCopy_NotReference()
        {
            // Arrange
            var solver = new EulerSolver(numSteps: 4);
            float[] timesteps1 = solver.GetTimesteps();
            float originalValue = timesteps1[0];

            // Act
            timesteps1[0] = 999f;
            float[] timesteps2 = solver.GetTimesteps();

            // Assert
            Assert.That(timesteps2[0], Is.EqualTo(originalValue).Within(Tolerance));
        }

        #endregion
    }
}
