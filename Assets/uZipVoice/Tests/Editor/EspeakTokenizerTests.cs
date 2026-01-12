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
            Assert.That(tokens.Length, Is.GreaterThan(0)); // At least some phoneme tokens
            // NOTE: Python互換のため、BOS/EOSトークンは追加されない
        }

        #endregion

        #region ET-009: Tokenize_EmptyText

        [Test]
        public void Tokenize_EmptyText_ReturnsEmptyArray()
        {
            if (!_espeakAvailable)
            {
                Assert.Ignore("espeak-ng-data not available");
            }

            // Arrange
            _tokenizer.Initialize(_espeakDataPath);

            // Act
            int[] tokens = _tokenizer.Tokenize("");

            // Assert - Python互換のため、空配列を返す
            Assert.That(tokens, Is.Empty);
        }

        [Test]
        public void Tokenize_NullText_ReturnsEmptyArray()
        {
            if (!_espeakAvailable)
            {
                Assert.Ignore("espeak-ng-data not available");
            }

            // Arrange
            _tokenizer.Initialize(_espeakDataPath);

            // Act
            int[] tokens = _tokenizer.Tokenize(null);

            // Assert - Python互換のため、空配列を返す
            Assert.That(tokens, Is.Empty);
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

            // Assert - Python互換のため、BOS/EOSなし
            Assert.That(tokens.Length, Is.EqualTo(2));
            Assert.That(tokens[0], Is.EqualTo(20)); // h
            Assert.That(tokens[1], Is.EqualTo(59)); // ə
        }

        [Test]
        public void PhonemeStringToTokens_EmptyString_ReturnsEmptyArray()
        {
            // Act
            int[] tokens = _tokenizer.PhonemeStringToTokens("");

            // Assert - Python互換のため、空配列
            Assert.That(tokens, Is.Empty);
        }

        [Test]
        public void PhonemeStringToTokens_NullString_ReturnsEmptyArray()
        {
            // Act
            int[] tokens = _tokenizer.PhonemeStringToTokens(null);

            // Assert - Python互換のため、空配列
            Assert.That(tokens, Is.Empty);
        }

        [Test]
        public void PhonemeStringToTokens_UnknownPhonemes_SkipsUnknown()
        {
            // Arrange - 'X'はTestUtilityで定義されていない、hは定義されている
            string phonemes = "hXh";

            // Act
            int[] tokens = _tokenizer.PhonemeStringToTokens(phonemes);

            // Assert - h, h (Xはスキップされる、BOS/EOSなし)
            Assert.That(tokens.Length, Is.EqualTo(2));
            Assert.That(tokens[0], Is.EqualTo(20)); // h
            Assert.That(tokens[1], Is.EqualTo(20)); // h
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

            // Assert - Python互換のため、BOS/EOSなし
            Assert.That(tokens.Length, Is.EqualTo(1));
            Assert.That(tokens[0], Is.EqualTo(20)); // h
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

        #region Integration Tests with Real tokens.txt

        [Test]
        public void Tokenize_HelloWithRealTokens_ProducesReasonableTokenCount()
        {
            if (!_espeakAvailable)
            {
                Assert.Ignore("espeak-ng-data not available");
            }

            // Use real tokens.txt
            string tokensPath = System.IO.Path.Combine(Application.dataPath, "uZipVoice/Resources/tokens.txt");
            if (!System.IO.File.Exists(tokensPath))
            {
                Assert.Ignore("Real tokens.txt not found");
            }

            var realTokenMap = new TokenMap();
            realTokenMap.LoadFromString(System.IO.File.ReadAllText(tokensPath));
            var realTokenizer = new EspeakTokenizer(realTokenMap);

            try
            {
                realTokenizer.Initialize(_espeakDataPath);

                // Act
                int[] tokens = realTokenizer.Tokenize("hello");

                // Assert - "hello" should produce ~3-6 phoneme tokens (h, ə, l, oʊ or similar)
                // Python互換のため、BOS/EOSトークンは追加されない
                Assert.That(tokens.Length, Is.GreaterThanOrEqualTo(3),
                    $"'hello' should produce at least 3 tokens but got {tokens.Length}");

                Debug.Log($"[Test] 'hello' produced {tokens.Length} tokens: [{string.Join(", ", tokens)}]");
            }
            finally
            {
                realTokenizer.Dispose();
            }
        }

        [Test]
        public void Tokenize_LongSentenceWithRealTokens_ProducesExpectedTokenCount()
        {
            if (!_espeakAvailable)
            {
                Assert.Ignore("espeak-ng-data not available");
            }

            // Use real tokens.txt
            string tokensPath = System.IO.Path.Combine(Application.dataPath, "uZipVoice/Resources/tokens.txt");
            if (!System.IO.File.Exists(tokensPath))
            {
                Assert.Ignore("Real tokens.txt not found");
            }

            var realTokenMap = new TokenMap();
            realTokenMap.LoadFromString(System.IO.File.ReadAllText(tokensPath));
            var realTokenizer = new EspeakTokenizer(realTokenMap);

            try
            {
                realTokenizer.Initialize(_espeakDataPath);

                // Act - This is the sentence that was producing only 8 tokens
                string testSentence = "Hello, this is a test of the text to speech system.";
                int[] tokens = realTokenizer.Tokenize(testSentence);

                // Assert - This sentence should produce ~40-60 phoneme tokens
                // If it produces less than 20, something is wrong
                Assert.That(tokens.Length, Is.GreaterThanOrEqualTo(20),
                    $"'{testSentence}' should produce at least 20 tokens but got {tokens.Length}");

                Debug.Log($"[Test] Long sentence produced {tokens.Length} tokens");
            }
            finally
            {
                realTokenizer.Dispose();
            }
        }

        [Test]
        public void TextToPhonemes_Hello_ReturnsValidPhonemes()
        {
            if (!_espeakAvailable)
            {
                Assert.Ignore("espeak-ng-data not available");
            }

            // Arrange
            _tokenizer.Initialize(_espeakDataPath);

            // Act
            string phonemes = _tokenizer.TextToPhonemes("hello");

            // Assert - should return something like "həˈloʊ" or similar
            Assert.That(phonemes, Is.Not.Null.And.Not.Empty, "Phonemes should not be empty");
            Assert.That(phonemes.Length, Is.GreaterThanOrEqualTo(3),
                $"'hello' phonemes should have at least 3 characters but got '{phonemes}' ({phonemes.Length} chars)");

            Debug.Log($"[Test] 'hello' phonemes: '{phonemes}' ({phonemes.Length} chars)");

            // Log Unicode code points for debugging
            var codePoints = new System.Collections.Generic.List<string>();
            foreach (char c in phonemes)
            {
                codePoints.Add($"'{c}'(U+{((int)c):X4})");
            }
            Debug.Log($"[Test] Phoneme code points: {string.Join(", ", codePoints)}");
        }

        #endregion
    }
}
