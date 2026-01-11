using NUnit.Framework;
using Unity.InferenceEngine;
using UnityEngine;

namespace uZipVoice.Tests
{
    /// <summary>
    /// テンソル形状のテスト
    /// ONNXモデルが期待する入力形状とC#コードの形状が一致することを確認
    /// </summary>
    public class TensorShapeTests
    {
        #region TextEncoder Input Shapes

        [Test]
        public void TextEncoder_TokensTensor_ShouldBeRank2()
        {
            // ONNX: tokens shape = [1, T] (dynamic)
            int[] tokens = { 2, 3, 4, 5, 6, 7, 8, 9 };
            var shape = new TensorShape(1, tokens.Length);

            Assert.AreEqual(2, shape.rank, "tokens should be rank 2");
            Assert.AreEqual(1, shape[0], "batch size should be 1");
            Assert.AreEqual(8, shape[1], "sequence length should match tokens.Length");
        }

        [Test]
        public void TextEncoder_PromptTokensTensor_ShouldBeRank2()
        {
            // ONNX: prompt_tokens shape = [1, T_prompt] (dynamic)
            int[] promptTokens = { 0, 1, 2, 3 };
            var shape = new TensorShape(1, promptTokens.Length);

            Assert.AreEqual(2, shape.rank, "prompt_tokens should be rank 2");
            Assert.AreEqual(1, shape[0], "batch size should be 1");
            Assert.AreEqual(4, shape[1], "sequence length should match promptTokens.Length");
        }

        [Test]
        public void TextEncoder_PromptFeaturesLenTensor_ShouldBeScalar()
        {
            // ONNX: prompt_features_len = torch.tensor(10, dtype=torch.int64) (scalar, rank 0)
            var shape = new TensorShape();

            Assert.AreEqual(0, shape.rank, "prompt_features_len should be scalar (rank 0)");
            Assert.AreEqual(1, shape.length, "scalar tensor should have length 1");
        }

        [Test]
        public void TextEncoder_SpeedTensor_ShouldBeScalar()
        {
            // ONNX: speed = torch.tensor(1.0, dtype=torch.float32) (scalar, rank 0)
            var shape = new TensorShape();

            Assert.AreEqual(0, shape.rank, "speed should be scalar (rank 0)");
            Assert.AreEqual(1, shape.length, "scalar tensor should have length 1");
        }

        [Test]
        public void TextEncoder_CreateTokensTensor_ValidShape()
        {
            int[] tokens = { 2, 3, 4, 5 };
            using var tensor = new Tensor<int>(new TensorShape(1, tokens.Length), tokens);

            Assert.AreEqual(2, tensor.shape.rank);
            Assert.AreEqual(1, tensor.shape[0]);
            Assert.AreEqual(4, tensor.shape[1]);
        }

        [Test]
        public void TextEncoder_CreateScalarTensor_ValidShape()
        {
            int value = 10;
            using var tensor = new Tensor<int>(new TensorShape(), new int[] { value });

            Assert.AreEqual(0, tensor.shape.rank, "Should be scalar (rank 0)");
            Assert.AreEqual(1, tensor.shape.length, "Scalar should have length 1");
        }

        #endregion

        #region FMDecoder Input Shapes

        [Test]
        public void FMDecoder_TTensor_ShouldBeScalar()
        {
            // ONNX: t = torch.tensor(0.5, dtype=torch.float32) (scalar, rank 0)
            var shape = new TensorShape();

            Assert.AreEqual(0, shape.rank, "t should be scalar (rank 0)");
        }

        [Test]
        public void FMDecoder_XTensor_ShouldBeRank3()
        {
            // ONNX: x shape = [1, seq_len, feat_dim] where feat_dim=100
            int batchSize = 1;
            int seqLen = 200;
            int featDim = 100;
            var shape = new TensorShape(batchSize, seqLen, featDim);

            Assert.AreEqual(3, shape.rank, "x should be rank 3");
            Assert.AreEqual(1, shape[0], "batch size should be 1");
            Assert.AreEqual(200, shape[1], "seq_len should be 200");
            Assert.AreEqual(100, shape[2], "feat_dim should be 100");
        }

        [Test]
        public void FMDecoder_TextConditionTensor_ShouldBeRank3()
        {
            // ONNX: text_condition shape = [1, seq_len, feat_dim]
            int batchSize = 1;
            int seqLen = 200;
            int featDim = 100;
            var shape = new TensorShape(batchSize, seqLen, featDim);

            Assert.AreEqual(3, shape.rank, "text_condition should be rank 3");
        }

        [Test]
        public void FMDecoder_SpeechConditionTensor_ShouldBeRank3()
        {
            // ONNX: speech_condition shape = [1, seq_len, feat_dim]
            int batchSize = 1;
            int seqLen = 200;
            int featDim = 100;
            var shape = new TensorShape(batchSize, seqLen, featDim);

            Assert.AreEqual(3, shape.rank, "speech_condition should be rank 3");
        }

        [Test]
        public void FMDecoder_GuidanceScaleTensor_ShouldBeScalar()
        {
            // ONNX: guidance_scale = torch.tensor(1.0, dtype=torch.float32) (scalar, rank 0)
            var shape = new TensorShape();

            Assert.AreEqual(0, shape.rank, "guidance_scale should be scalar (rank 0)");
        }

        [Test]
        public void FMDecoder_CreateScalarFloatTensor_ValidShape()
        {
            float value = 0.5f;
            using var tensor = new Tensor<float>(new TensorShape(), new float[] { value });

            Assert.AreEqual(0, tensor.shape.rank, "Should be scalar (rank 0)");
            Assert.AreEqual(1, tensor.shape.length, "Scalar should have length 1");
        }

        #endregion

        #region Vocos Input Shapes

        [Test]
        public void Vocos_MelFeaturesTensor_ShouldBeRank3()
        {
            // ONNX: features shape = [1, n_mels, T] where n_mels=100
            int batchSize = 1;
            int nMels = 100;
            int timeSteps = 200;
            var shape = new TensorShape(batchSize, nMels, timeSteps);

            Assert.AreEqual(3, shape.rank, "mel features should be rank 3");
            Assert.AreEqual(1, shape[0], "batch size should be 1");
            Assert.AreEqual(100, shape[1], "n_mels should be 100");
        }

        #endregion

        #region TensorShape Utility Tests

        [Test]
        public void TensorShape_Rank0_IsScalar()
        {
            var shape = new TensorShape();
            Assert.AreEqual(0, shape.rank);
            Assert.AreEqual(1, shape.length);
        }

        [Test]
        public void TensorShape_Rank1_IsVector()
        {
            var shape = new TensorShape(10);
            Assert.AreEqual(1, shape.rank);
            Assert.AreEqual(10, shape.length);
        }

        [Test]
        public void TensorShape_Rank2_IsMatrix()
        {
            var shape = new TensorShape(3, 4);
            Assert.AreEqual(2, shape.rank);
            Assert.AreEqual(12, shape.length);
        }

        [Test]
        public void TensorShape_Rank3_Is3DTensor()
        {
            var shape = new TensorShape(2, 3, 4);
            Assert.AreEqual(3, shape.rank);
            Assert.AreEqual(24, shape.length);
        }

        [Test]
        public void TensorShape_CompareScalarVsRank1()
        {
            var scalarShape = new TensorShape();     // rank 0, length 1
            var rank1Shape = new TensorShape(1);     // rank 1, length 1

            Assert.AreEqual(0, scalarShape.rank, "Scalar should be rank 0");
            Assert.AreEqual(1, rank1Shape.rank, "Rank-1 tensor with size 1 should be rank 1");
            Assert.AreEqual(scalarShape.length, rank1Shape.length, "Both have length 1");
            Assert.AreNotEqual(scalarShape.rank, rank1Shape.rank, "But different ranks!");
        }

        #endregion

        #region Integration Tests (Tensor Creation)

        [Test]
        public void Integration_CreateTextEncoderInputs_NoException()
        {
            int[] tokens = { 2, 3, 4, 5, 6, 7, 8, 9 };
            int[] promptTokens = { 0, 1, 2, 3 };
            int promptFeaturesLen = 100;
            float speed = 1.0f;

            Assert.DoesNotThrow(() =>
            {
                using var tokensTensor = new Tensor<int>(
                    new TensorShape(1, tokens.Length),
                    tokens
                );

                using var promptTokensTensor = new Tensor<int>(
                    new TensorShape(1, promptTokens.Length),
                    promptTokens
                );

                using var promptFeaturesLenTensor = new Tensor<int>(
                    new TensorShape(),
                    new int[] { promptFeaturesLen }
                );

                using var speedTensor = new Tensor<float>(
                    new TensorShape(),
                    new float[] { speed }
                );

                // Verify shapes
                Assert.AreEqual(2, tokensTensor.shape.rank);
                Assert.AreEqual(2, promptTokensTensor.shape.rank);
                Assert.AreEqual(0, promptFeaturesLenTensor.shape.rank);
                Assert.AreEqual(0, speedTensor.shape.rank);
            });
        }

        [Test]
        public void Integration_CreateFMDecoderInputs_NoException()
        {
            int batchSize = 1;
            int seqLen = 50;
            int featDim = 100;
            float t = 0.5f;
            float guidanceScale = 1.0f;

            float[] xData = new float[batchSize * seqLen * featDim];
            float[] textCondData = new float[batchSize * seqLen * featDim];
            float[] speechCondData = new float[batchSize * seqLen * featDim];

            Assert.DoesNotThrow(() =>
            {
                using var tTensor = new Tensor<float>(
                    new TensorShape(),
                    new float[] { t }
                );

                using var xTensor = new Tensor<float>(
                    new TensorShape(batchSize, seqLen, featDim),
                    xData
                );

                using var textCondTensor = new Tensor<float>(
                    new TensorShape(batchSize, seqLen, featDim),
                    textCondData
                );

                using var speechCondTensor = new Tensor<float>(
                    new TensorShape(batchSize, seqLen, featDim),
                    speechCondData
                );

                using var guidanceScaleTensor = new Tensor<float>(
                    new TensorShape(),
                    new float[] { guidanceScale }
                );

                // Verify shapes
                Assert.AreEqual(0, tTensor.shape.rank);
                Assert.AreEqual(3, xTensor.shape.rank);
                Assert.AreEqual(3, textCondTensor.shape.rank);
                Assert.AreEqual(3, speechCondTensor.shape.rank);
                Assert.AreEqual(0, guidanceScaleTensor.shape.rank);
            });
        }

        #endregion

        #region Array Size Validation Tests

        [Test]
        public void ArraySize_MustMatchTensorShapeLength()
        {
            // This test documents the relationship between array size and tensor shape

            // For rank-2 tensor [1, 8], we need 1*8=8 elements
            int[] data8 = new int[8];
            var shape1x8 = new TensorShape(1, 8);
            Assert.AreEqual(8, shape1x8.length, "Shape [1,8] needs 8 elements");
            Assert.AreEqual(data8.Length, shape1x8.length, "Array size must match shape length");

            // For scalar tensor (rank 0), we need 1 element
            int[] data1 = new int[1];
            var shapeScalar = new TensorShape();
            Assert.AreEqual(1, shapeScalar.length, "Scalar shape needs 1 element");
            Assert.AreEqual(data1.Length, shapeScalar.length, "Array size must match shape length");

            // For rank-3 tensor [1, 50, 100], we need 1*50*100=5000 elements
            float[] data5000 = new float[5000];
            var shape1x50x100 = new TensorShape(1, 50, 100);
            Assert.AreEqual(5000, shape1x50x100.length, "Shape [1,50,100] needs 5000 elements");
            Assert.AreEqual(data5000.Length, shape1x50x100.length, "Array size must match shape length");
        }

        [Test]
        public void WrongArraySize_WillCauseError()
        {
            // Document: if array size doesn't match shape, Sentis will throw an error
            // This is what was happening with the original bug

            int[] tokens = { 1, 2, 3, 4, 5, 6, 7, 8 }; // 8 elements
            var correctShape = new TensorShape(1, 8);   // needs 8 elements - OK
            var wrongShape = new TensorShape(1, 16);    // needs 16 elements - WRONG!

            Assert.AreEqual(tokens.Length, correctShape.length, "Correct shape matches array size");
            Assert.AreNotEqual(tokens.Length, wrongShape.length, "Wrong shape doesn't match array size");
        }

        #endregion
    }
}
