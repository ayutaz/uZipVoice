using System;
using NUnit.Framework;
using uZipVoice.Tokenizer;
using UnityEngine;

namespace uZipVoice.Tests
{
    /// <summary>
    /// EspeakTokenizerクラスのテスト
    /// 注意: espeak-ng DLLが必要なテストは条件付きで実行
    /// </summary>
    [TestFixture]
    public class EspeakTokenizerTests
    {
        private EspeakTokenizer _tokenizer;
        private TokenMap _tokenMap;
        private bool _espeakAvailable;
        private string _espeakDataPath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // espeak-ng-dataのパスを確認
            _espeakDataPath = System.IO.Path.Combine(Application.streamingAssetsPath, "espeak-ng-data");
            _espeakAvailable = System.IO.Directory.Exists(_espeakDataPath);

            if (!_espeakAvailable)
            {
                Debug.LogWarning("[EspeakTokenizerTests] espeak-ng-data not found. Some tests will be skipped.");
            }
        }

        [SetUp]
        public void SetUp()
        {
            _tokenMap = new TokenMap();
            string tokensContent = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(tokensContent);
            _tokenizer = new EspeakTokenizer(_tokenMap);
        }

        [TearDown]
        public void TearDown()
        {
            _tokenizer?.Dispose();
        }

        #region ET-001: Initialize (espeak-ng依存)

        [Test]
        public void Initialize_ValidPath_Succeeds()
        {
            if (!_espeakAvailable)
            {
                Assert.Ignore("espeak-ng-data not available");
            }

            // Act
            _tokenizer.Initialize(_espeakDataPath);

            // Assert
            Assert.That(_tokenizer.IsInitialized, Is.True);
        }

        #endregion

        #region ET-002: Initialize_InvalidPath

        [Test]
        public void Initialize_NullPath_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _tokenizer.Initialize(null));
        }

        [Test]
        public void Initialize_EmptyPath_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _tokenizer.Initialize(""));
        }

        #endregion

        #region ET-008: Tokenize (espeak-ng依存)

        [Test]
        public void Tokenize_ValidText_ReturnsTokens()
        {
            if (!_espeakAvailable)
            {
                Assert.Ignore("espeak-ng-data not available");
            }

            // Arrange
            _tokenizer.Initialize(_espeakDataPath);

            // Act
            int[] tokens = _tokenizer.Tokenize("hello");

            // Assert
            Assert.That(tokens, Is.Not.Null);
            Assert.That(tokens.Length, Is.GreaterThan(2)); // At least BOS + EOS + some phonemes
            Assert.That(tokens[0], Is.EqualTo(_tokenMap.BosId)); // Starts with BOS
            Assert.That(tokens[^1], Is.EqualTo(_tokenMap.EosId)); // Ends with EOS
        }

        #endregion

        #region ET-009: Tokenize_EmptyText

        [Test]
        public void Tokenize_EmptyText_ReturnsBosEos()
        {
            if (!_espeakAvailable)
            {
                Assert.Ignore("espeak-ng-data not available");
            }

            // Arrange
            _tokenizer.Initialize(_espeakDataPath);

            // Act
            int[] tokens = _tokenizer.Tokenize("");

            // Assert
            Assert.That(tokens, Is.EqualTo(new[] { _tokenMap.BosId, _tokenMap.EosId }));
        }

        [Test]
        public void Tokenize_NullText_ReturnsBosEos()
        {
            if (!_espeakAvailable)
            {
                Assert.Ignore("espeak-ng-data not available");
            }

            // Arrange
            _tokenizer.Initialize(_espeakDataPath);

            // Act
            int[] tokens = _tokenizer.Tokenize(null);

            // Assert
            Assert.That(tokens, Is.EqualTo(new[] { _tokenMap.BosId, _tokenMap.EosId }));
        }

        #endregion

        #region PhonemeStringToTokens (espeak-ng非依存)

        [Test]
        public void PhonemeStringToTokens_ValidPhonemes_ReturnsTokens()
        {
            // Arrange - hとəはTestUtilityで定義されている
            string phonemes = "hə";

            // Act
            int[] tokens = _tokenizer.PhonemeStringToTokens(phonemes);

            // Assert
            Assert.That(tokens[0], Is.EqualTo(_tokenMap.BosId)); // BOS
            Assert.That(tokens[1], Is.EqualTo(20)); // h
            Assert.That(tokens[2], Is.EqualTo(59)); // ə
            Assert.That(tokens[^1], Is.EqualTo(_tokenMap.EosId)); // EOS
        }

        [Test]
        public void PhonemeStringToTokens_EmptyString_ReturnsBosEos()
        {
            // Act
            int[] tokens = _tokenizer.PhonemeStringToTokens("");

            // Assert
            Assert.That(tokens, Is.EqualTo(new[] { _tokenMap.BosId, _tokenMap.EosId }));
        }

        [Test]
        public void PhonemeStringToTokens_NullString_ReturnsBosEos()
        {
            // Act
            int[] tokens = _tokenizer.PhonemeStringToTokens(null);

            // Assert
            Assert.That(tokens, Is.EqualTo(new[] { _tokenMap.BosId, _tokenMap.EosId }));
        }

        [Test]
        public void PhonemeStringToTokens_UnknownPhonemes_SkipsUnknown()
        {
            // Arrange - 'x'はTestUtilityで定義されていない、hは定義されている
            string phonemes = "hXh";

            // Act
            int[] tokens = _tokenizer.PhonemeStringToTokens(phonemes);

            // Assert
            // BOS, h, h, EOS (Xはスキップされる)
            Assert.That(tokens.Length, Is.EqualTo(4));
            Assert.That(tokens[0], Is.EqualTo(_tokenMap.BosId));
            Assert.That(tokens[1], Is.EqualTo(20)); // h
            Assert.That(tokens[2], Is.EqualTo(20)); // h
            Assert.That(tokens[3], Is.EqualTo(_tokenMap.EosId));
        }

        #endregion

        #region ET-011: SetVoice (espeak-ng依存)

        [Test]
        public void Voice_SetBeforeInitialize_AppliesAfterInitialize()
        {
            if (!_espeakAvailable)
            {
                Assert.Ignore("espeak-ng-data not available");
            }

            // Arrange
            _tokenizer.Voice = "en-gb";

            // Act
            _tokenizer.Initialize(_espeakDataPath);

            // Assert
            Assert.That(_tokenizer.Voice, Is.EqualTo("en-gb"));
        }

        #endregion

        #region ET-012: Dispose

        [Test]
        public void Dispose_AfterInitialize_CleansUp()
        {
            if (!_espeakAvailable)
            {
                Assert.Ignore("espeak-ng-data not available");
            }

            // Arrange
            _tokenizer.Initialize(_espeakDataPath);

            // Act
            _tokenizer.Dispose();

            // Assert
            Assert.That(_tokenizer.IsInitialized, Is.False);
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            if (!_espeakAvailable)
            {
                Assert.Ignore("espeak-ng-data not available");
            }

            // Arrange
            _tokenizer.Initialize(_espeakDataPath);

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _tokenizer.Dispose();
                _tokenizer.Dispose();
            });
        }

        [Test]
        public void Dispose_BeforeInitialize_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _tokenizer.Dispose());
        }

        #endregion

        #region Not Initialized

        [Test]
        public void TextToPhonemes_NotInitialized_ThrowsInvalidOperationException()
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _tokenizer.TextToPhonemes("hello"));
        }

        [Test]
        public void Tokenize_NotInitialized_ThrowsInvalidOperationException()
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _tokenizer.Tokenize("hello"));
        }

        #endregion

        #region Constructor

        [Test]
        public void Constructor_NullTokenMap_CreatesInternalTokenMap()
        {
            // Act
            var tokenizer = new EspeakTokenizer(null);

            // Assert
            Assert.That(tokenizer.IsInitialized, Is.False);
            Assert.DoesNotThrow(() => tokenizer.Dispose());
        }

        [Test]
        public void Constructor_WithTokenMap_UsesProvidedTokenMap()
        {
            // Arrange
            var tokenMap = new TokenMap();
            tokenMap.LoadFromString(TestUtility.CreateTestTokensContent());

            // Act
            var tokenizer = new EspeakTokenizer(tokenMap);
            int[] tokens = tokenizer.PhonemeStringToTokens("h");

            // Assert
            Assert.That(tokens[1], Is.EqualTo(20)); // h
            tokenizer.Dispose();
        }

        #endregion

        #region IsInitialized

        [Test]
        public void IsInitialized_BeforeInitialize_ReturnsFalse()
        {
            // Assert
            Assert.That(_tokenizer.IsInitialized, Is.False);
        }

        #endregion
    }
}
