using System;
using System.Threading.Tasks;

namespace uZipVoice.Tokenizer
{
    /// <summary>
    /// トークナイザーインターフェース
    /// テキストを音素に変換し、トークンIDに変換する
    /// </summary>
    public interface ITokenizer : IDisposable
    {
        /// <summary>
        /// 初期化済みかどうか
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// トークナイザーを初期化
        /// </summary>
        /// <param name="dataPath">データディレクトリのパス</param>
        void Initialize(string dataPath);

        /// <summary>
        /// トークナイザーを非同期で初期化
        /// </summary>
        /// <param name="dataPath">データディレクトリのパス</param>
        Task InitializeAsync(string dataPath);

        /// <summary>
        /// テキストを音素列に変換
        /// </summary>
        /// <param name="text">入力テキスト</param>
        /// <returns>IPA音素文字列</returns>
        string TextToPhonemes(string text);

        /// <summary>
        /// テキストをトークンID列に変換
        /// </summary>
        /// <param name="text">入力テキスト</param>
        /// <returns>トークンID配列（BOS, 音素ID..., EOS）</returns>
        int[] Tokenize(string text);

        /// <summary>
        /// 音素文字列をトークンID列に変換
        /// </summary>
        /// <param name="phonemes">IPA音素文字列</param>
        /// <returns>トークンID配列</returns>
        int[] PhonemeStringToTokens(string phonemes);
    }
}
