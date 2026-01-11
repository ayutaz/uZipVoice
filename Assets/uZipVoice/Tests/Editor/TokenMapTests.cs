using System;
using System.IO;
using NUnit.Framework;
using uZipVoice.Tokenizer;
using UnityEngine;

namespace uZipVoice.Tests
{
    /// <summary>
    /// TokenMapクラスのテスト
    /// </summary>
    [TestFixture]
    public class TokenMapTests
    {
        private TokenMap _tokenMap;
        private string _tempFilePath;

        [SetUp]
        public void SetUp()
        {
            _tokenMap = new TokenMap();
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_tempFilePath))
            {
                TestUtility.DeleteTempFile(_tempFilePath);
                _tempFilePath = null;
            }
        }

        #region TM-001: LoadTokensFromFile

        [Test]
        public void LoadFromFile_ValidFile_LoadsSuccessfully()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tempFilePath = TestUtility.CreateTempFile(content);

            // Act
            _tokenMap.LoadFromFile(_tempFilePath);

            // Assert
            Assert.That(_tokenMap.Count, Is.GreaterThan(0));
        }

        [Test]
        public void LoadFromFile_FileNotFound_ThrowsFileNotFoundException()
        {
            // Arrange
            string invalidPath = Path.Combine(Application.temporaryCachePath, "nonexistent.txt");

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => _tokenMap.LoadFromFile(invalidPath));
        }

        [Test]
        public void LoadFromFile_NullPath_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _tokenMap.LoadFromFile(null));
        }

        [Test]
        public void LoadFromFile_EmptyPath_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _tokenMap.LoadFromFile(""));
        }

        #endregion

        #region TM-002: LoadTokensFromTextAsset (simplified test)

        [Test]
        public void LoadFromTextAsset_NullAsset_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _tokenMap.LoadFromTextAsset(null));
        }

        #endregion

        #region TM-003: GetTokenId_ValidPhoneme

        [Test]
        public void GetTokenId_ValidPhoneme_ReturnsCorrectId()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act & Assert
            Assert.That(_tokenMap.GetTokenId("h"), Is.EqualTo(20));
            Assert.That(_tokenMap.GetTokenId("ə"), Is.EqualTo(59));
            Assert.That(_tokenMap.GetTokenId("l"), Is.EqualTo(24));
        }

        #endregion

        #region TM-004: GetTokenId_InvalidPhoneme

        [Test]
        public void GetTokenId_InvalidPhoneme_ThrowsKeyNotFoundException()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act & Assert
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => _tokenMap.GetTokenId("invalid_phoneme")
            );
        }

        [Test]
        public void GetTokenIdOrDefault_InvalidPhoneme_ReturnsDefaultValue()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act
            int result = _tokenMap.GetTokenIdOrDefault("invalid_phoneme", -1);

            // Assert
            Assert.That(result, Is.EqualTo(-1));
        }

        #endregion

        #region TM-005: GetTokenId_SpecialTokens

        [Test]
        public void GetTokenId_PadToken_ReturnsZero()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act & Assert
            Assert.That(_tokenMap.PadId, Is.EqualTo(0));
        }

        [Test]
        public void GetTokenId_BosToken_ReturnsOne()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act & Assert
            Assert.That(_tokenMap.BosId, Is.EqualTo(1));
        }

        [Test]
        public void GetTokenId_EosToken_ReturnsTwo()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act & Assert
            Assert.That(_tokenMap.EosId, Is.EqualTo(2));
        }

        [Test]
        public void GetTokenId_SpaceToken_ReturnsThree()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act & Assert
            Assert.That(_tokenMap.SpaceId, Is.EqualTo(3));
        }

        #endregion

        #region TM-006: TokenCount

        [Test]
        public void Count_AfterLoading_ReturnsCorrectCount()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act & Assert
            Assert.That(_tokenMap.Count, Is.EqualTo(15)); // TestUtilityで15個のトークンを定義
        }

        [Test]
        public void Count_BeforeLoading_ReturnsZero()
        {
            // Assert
            Assert.That(_tokenMap.Count, Is.EqualTo(0));
        }

        #endregion

        #region TM-007: ContainsPhoneme

        [Test]
        public void ContainsPhoneme_ExistingPhoneme_ReturnsTrue()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act & Assert
            Assert.That(_tokenMap.ContainsPhoneme("h"), Is.True);
            Assert.That(_tokenMap.ContainsPhoneme("ə"), Is.True);
            Assert.That(_tokenMap.ContainsPhoneme(TokenMap.PAD), Is.True);
        }

        [Test]
        public void ContainsPhoneme_NonExistingPhoneme_ReturnsFalse()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act & Assert
            Assert.That(_tokenMap.ContainsPhoneme("xyz"), Is.False);
        }

        #endregion

        #region TM-008: LoadTokens_EmptyFile

        [Test]
        public void LoadFromString_EmptyContent_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _tokenMap.LoadFromString(""));
        }

        [Test]
        public void LoadFromString_NullContent_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _tokenMap.LoadFromString(null));
        }

        [Test]
        public void LoadFromString_OnlyWhitespace_ThrowsFormatException()
        {
            // Act & Assert
            Assert.Throws<FormatException>(() => _tokenMap.LoadFromString("   \n   \r\n   "));
        }

        #endregion

        #region TM-009: LoadTokens_MalformedFile

        [Test]
        public void LoadFromString_NoTabSeparator_ThrowsFormatException()
        {
            // Arrange
            string content = "invalid_line_without_tab";

            // Act & Assert
            Assert.Throws<FormatException>(() => _tokenMap.LoadFromString(content));
        }

        [Test]
        public void LoadFromString_InvalidId_ThrowsFormatException()
        {
            // Arrange
            string content = "phoneme\tnot_a_number";

            // Act & Assert
            Assert.Throws<FormatException>(() => _tokenMap.LoadFromString(content));
        }

        [Test]
        public void LoadFromString_DuplicatePhoneme_ThrowsFormatException()
        {
            // Arrange
            string content = "_\t0\n_\t1";

            // Act & Assert
            Assert.Throws<FormatException>(() => _tokenMap.LoadFromString(content));
        }

        #endregion

        #region Additional Tests

        [Test]
        public void GetPhoneme_ValidId_ReturnsCorrectPhoneme()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act & Assert
            Assert.That(_tokenMap.GetPhoneme(20), Is.EqualTo("h"));
            Assert.That(_tokenMap.GetPhoneme(59), Is.EqualTo("ə"));
        }

        [Test]
        public void GetPhoneme_InvalidId_ThrowsKeyNotFoundException()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act & Assert
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => _tokenMap.GetPhoneme(9999)
            );
        }

        [Test]
        public void PhonemeToIds_ValidPhonemes_ReturnsCorrectIds()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);
            string[] phonemes = new[] { "h", "ə", "l" };

            // Act
            int[] ids = _tokenMap.PhonemeToIds(phonemes);

            // Assert
            Assert.That(ids, Is.EqualTo(new[] { 20, 59, 24 }));
        }

        [Test]
        public void PhonemeToIds_NullInput_ThrowsArgumentNullException()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _tokenMap.PhonemeToIds(null));
        }

        [Test]
        public void ContainsId_ExistingId_ReturnsTrue()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act & Assert
            Assert.That(_tokenMap.ContainsId(0), Is.True);
            Assert.That(_tokenMap.ContainsId(20), Is.True);
        }

        [Test]
        public void ContainsId_NonExistingId_ReturnsFalse()
        {
            // Arrange
            string content = TestUtility.CreateTestTokensContent();
            _tokenMap.LoadFromString(content);

            // Act & Assert
            Assert.That(_tokenMap.ContainsId(9999), Is.False);
        }

        #endregion
    }
}
