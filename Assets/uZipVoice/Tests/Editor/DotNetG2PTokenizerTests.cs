using System.Text;
using NUnit.Framework;
using uZipVoice.Tokenizer;
using UnityEngine;

namespace uZipVoice.Tests
{
    /// <summary>
    /// DotNetG2PTokenizerクラスのテスト
    /// TokenMapベースのテスト（dot-net-g2p辞書不要）
    /// </summary>
    [TestFixture]
    public class DotNetG2PTokenizerTests
    {
        private DotNetG2PTokenizer _tokenizer;
        private TokenMap _tokenMap;

        /// <summary>
        /// 日本語音素を含むテスト用トークン内容を生成
        /// </summary>
        private static string CreateJapaneseTestTokensContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine("_\t0");   // PAD
            sb.AppendLine("^\t1");   // BOS
            sb.AppendLine("$\t2");   // EOS
            sb.AppendLine(" \t3");   // SPACE
            sb.AppendLine("!\t4");
            sb.AppendLine(".\t10");
            sb.AppendLine(",\t11");
            sb.AppendLine("a\t14");
            sb.AppendLine("h\t20");
            sb.AppendLine("i\t21");
            sb.AppendLine("k\t23");
            sb.AppendLine("n\t26");
            sb.AppendLine("o\t27");
            sb.AppendLine("w\t35");
            sb.AppendLine("N\t363");
            sb.AppendLine("ch\t367");
            sb.AppendLine("pau\t378");
            sb.AppendLine("sil\t382");
            return sb.ToString();
        }

        [SetUp]
        public void SetUp()
        {
            _tokenMap = new TokenMap();
            _tokenMap.LoadFromString(CreateJapaneseTestTokensContent());
            _tokenizer = new DotNetG2PTokenizer(_tokenMap);
        }

        [TearDown]
        public void TearDown()
        {
            _tokenizer?.Dispose();
        }

        #region PhonemeStringToTokens

        [Test]
        public void PhonemeStringToTokens_KonnichiWa_ReturnsCorrectTokens()
        {
            // "k o N n i ch i w a" → [23, 27, 363, 26, 21, 367, 21, 35, 14]
            int[] tokens = _tokenizer.PhonemeStringToTokens("k o N n i ch i w a");

            Assert.That(tokens.Length, Is.EqualTo(9));
            Assert.That(tokens[0], Is.EqualTo(23));  // k
            Assert.That(tokens[1], Is.EqualTo(27));  // o
            Assert.That(tokens[2], Is.EqualTo(363)); // N
            Assert.That(tokens[3], Is.EqualTo(26));  // n
            Assert.That(tokens[4], Is.EqualTo(21));  // i
            Assert.That(tokens[5], Is.EqualTo(367)); // ch
            Assert.That(tokens[6], Is.EqualTo(21));  // i
            Assert.That(tokens[7], Is.EqualTo(35));  // w
            Assert.That(tokens[8], Is.EqualTo(14));  // a
        }

        [Test]
        public void PhonemeStringToTokens_Pau_ReturnsSingleToken()
        {
            int[] tokens = _tokenizer.PhonemeStringToTokens("pau");

            Assert.That(tokens.Length, Is.EqualTo(1));
            Assert.That(tokens[0], Is.EqualTo(378));
        }

        [Test]
        public void PhonemeStringToTokens_Sil_ReturnsSingleToken()
        {
            int[] tokens = _tokenizer.PhonemeStringToTokens("sil");

            Assert.That(tokens.Length, Is.EqualTo(1));
            Assert.That(tokens[0], Is.EqualTo(382));
        }

        [Test]
        public void PhonemeStringToTokens_MultiCharTokens_HandledCorrectly()
        {
            // "ch" は1トークン、"c" "h" ではない
            int[] tokens = _tokenizer.PhonemeStringToTokens("ch");

            Assert.That(tokens.Length, Is.EqualTo(1));
            Assert.That(tokens[0], Is.EqualTo(367));
        }

        [Test]
        public void PhonemeStringToTokens_EmptyString_ReturnsEmptyArray()
        {
            int[] tokens = _tokenizer.PhonemeStringToTokens("");
            Assert.That(tokens, Is.Empty);
        }

        [Test]
        public void PhonemeStringToTokens_NullString_ReturnsEmptyArray()
        {
            int[] tokens = _tokenizer.PhonemeStringToTokens(null);
            Assert.That(tokens, Is.Empty);
        }

        [Test]
        public void PhonemeStringToTokens_UnknownPhonemes_SkipsUnknown()
        {
            // "k UNKNOWN o" → [23, 27]（UNKNOWNがスキップ）
            int[] tokens = _tokenizer.PhonemeStringToTokens("k UNKNOWN o");

            Assert.That(tokens.Length, Is.EqualTo(2));
            Assert.That(tokens[0], Is.EqualTo(23)); // k
            Assert.That(tokens[1], Is.EqualTo(27)); // o
        }

        [Test]
        public void PhonemeStringToTokens_MultipleSpaces_HandledCorrectly()
        {
            int[] tokens = _tokenizer.PhonemeStringToTokens("k  o   n");

            Assert.That(tokens.Length, Is.EqualTo(3));
            Assert.That(tokens[0], Is.EqualTo(23)); // k
            Assert.That(tokens[1], Is.EqualTo(27)); // o
            Assert.That(tokens[2], Is.EqualTo(26)); // n
        }

        #endregion

        #region Initialize

        [Test]
        public void IsInitialized_BeforeInitialize_ReturnsFalse()
        {
            Assert.That(_tokenizer.IsInitialized, Is.False);
        }

        [Test]
        public void Initialize_NullPath_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => _tokenizer.Initialize(null));
        }

        [Test]
        public void Initialize_EmptyPath_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => _tokenizer.Initialize(""));
        }

        #endregion

        #region Tokenize (requires initialization)

        [Test]
        public void Tokenize_NotInitialized_ThrowsInvalidOperationException()
        {
            Assert.Throws<System.InvalidOperationException>(() => _tokenizer.Tokenize("こんにちは"));
        }

        [Test]
        public void Tokenize_NullText_NotInitialized_ThrowsInvalidOperationException()
        {
            // Even null/empty should throw if not initialized (consistent with EspeakTokenizer)
            Assert.Throws<System.InvalidOperationException>(() => _tokenizer.Tokenize(null));
        }

        #endregion

        #region TextToPhonemes

        [Test]
        public void TextToPhonemes_NotInitialized_ThrowsInvalidOperationException()
        {
            Assert.Throws<System.InvalidOperationException>(() => _tokenizer.TextToPhonemes("test"));
        }

        #endregion

        #region Dispose

        [Test]
        public void Dispose_BeforeInitialize_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _tokenizer.Dispose());
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _tokenizer.Dispose();
                _tokenizer.Dispose();
            });
        }

        #endregion

        #region Constructor

        [Test]
        public void Constructor_NullTokenMap_CreatesInternalTokenMap()
        {
            var tokenizer = new DotNetG2PTokenizer(null);
            Assert.That(tokenizer.IsInitialized, Is.False);
            Assert.DoesNotThrow(() => tokenizer.Dispose());
        }

        [Test]
        public void Constructor_WithTokenMap_UsesProvidedTokenMap()
        {
            var tokenMap = new TokenMap();
            tokenMap.LoadFromString(CreateJapaneseTestTokensContent());

            var tokenizer = new DotNetG2PTokenizer(tokenMap);
            int[] tokens = tokenizer.PhonemeStringToTokens("k o");

            Assert.That(tokens.Length, Is.EqualTo(2));
            Assert.That(tokens[0], Is.EqualTo(23)); // k
            Assert.That(tokens[1], Is.EqualTo(27)); // o
            tokenizer.Dispose();
        }

        #endregion
    }
}
