using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNetG2P.Core;
using DotNetG2P.MeCab;
using UnityEngine;

namespace uZipVoice.Tokenizer
{
    /// <summary>
    /// dot-net-g2pを使用した日本語トークナイザー
    /// 日本語テキストを音素に変換し、トークンIDに変換する
    /// </summary>
    public class DotNetG2PTokenizer : ITokenizer
    {
        private TokenMap _tokenMap;
        private G2PEngine _g2pEngine;
        private bool _isInitialized;
        private bool _isDisposed;

        /// <summary>
        /// 初期化済みかどうか
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="tokenMap">トークンマップ（nullの場合は内部で作成）</param>
        public DotNetG2PTokenizer(TokenMap tokenMap = null)
        {
            _tokenMap = tokenMap ?? new TokenMap();
        }

        /// <summary>
        /// トークナイザーを初期化
        /// </summary>
        /// <param name="dataPath">naist-jdic辞書ディレクトリのパス</param>
        public void Initialize(string dataPath)
        {
            if (_isInitialized)
            {
                return;
            }

            if (string.IsNullOrEmpty(dataPath))
            {
                throw new ArgumentException("Data path cannot be null or empty", nameof(dataPath));
            }

            var mecabTokenizer = new MeCabTokenizer(dataPath);
            _g2pEngine = new G2PEngine(mecabTokenizer);

            _isInitialized = true;
            Debug.Log("[DotNetG2PTokenizer] Initialized with dictionary: " + dataPath);
        }

        /// <summary>
        /// トークナイザーを非同期で初期化
        /// </summary>
        /// <param name="dataPath">naist-jdic辞書ディレクトリのパス</param>
        public Task InitializeAsync(string dataPath)
        {
            Initialize(dataPath);
            return Task.CompletedTask;
        }

        /// <summary>
        /// テキストを音素列に変換
        /// </summary>
        /// <param name="text">入力テキスト（日本語）</param>
        /// <returns>スペース区切りの音素文字列</returns>
        public string TextToPhonemes(string text)
        {
            ThrowIfNotInitialized();

            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // 句読点を除去してG2P変換用テキストを作成
            string cleanText = RemoveJapanesePunctuation(text);
            if (string.IsNullOrEmpty(cleanText))
            {
                return string.Empty;
            }

            var pronunciations = _g2pEngine.Convert(cleanText);
            var phonemeList = new List<string>();

            foreach (var pronunciation in pronunciations)
            {
                string phonemeStr = pronunciation.ToString();
                if (!string.IsNullOrEmpty(phonemeStr))
                {
                    // スペース区切りの音素を追加
                    string[] parts = phonemeStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    phonemeList.AddRange(parts);
                }
            }

            string result = string.Join(" ", phonemeList);
            Debug.Log($"[DotNetG2PTokenizer] TextToPhonemes: '{text}' -> '{result}'");
            return result;
        }

        /// <summary>
        /// テキストをトークンID列に変換
        /// NOTE: Python側と同じく、BOS/EOSトークンは追加しない
        /// </summary>
        /// <param name="text">入力テキスト</param>
        /// <returns>トークンID配列</returns>
        public int[] Tokenize(string text)
        {
            ThrowIfNotInitialized();

            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<int>();
            }

            string phonemes = TextToPhonemes(text);
            var tokens = new List<int>(PhonemeStringToTokens(phonemes));

            // 日本語句読点をトークンに追加
            if (text.Length > 0)
            {
                char lastChar = text[text.Length - 1];
                string mappedPunct = MapJapanesePunctuation(lastChar);
                if (mappedPunct != null)
                {
                    int punctToken = _tokenMap.GetTokenIdOrDefault(mappedPunct, -1);
                    if (punctToken >= 0)
                    {
                        tokens.Add(punctToken);
                        Debug.Log($"[DotNetG2PTokenizer] Added trailing punctuation '{lastChar}' -> '{mappedPunct}' as token {punctToken}");
                    }
                }
                else if (IsAsciiPunctuation(lastChar))
                {
                    int punctToken = _tokenMap.GetTokenIdOrDefault(lastChar.ToString(), -1);
                    if (punctToken >= 0)
                    {
                        tokens.Add(punctToken);
                        Debug.Log($"[DotNetG2PTokenizer] Added trailing punctuation '{lastChar}' as token {punctToken}");
                    }
                }
            }

            return tokens.ToArray();
        }

        /// <summary>
        /// 音素文字列をトークンID列に変換
        /// スペース区切りのマルチ文字トークンに対応
        /// </summary>
        /// <param name="phonemes">スペース区切りの音素文字列</param>
        /// <returns>トークンID配列</returns>
        public int[] PhonemeStringToTokens(string phonemes)
        {
            if (string.IsNullOrEmpty(phonemes))
            {
                return Array.Empty<int>();
            }

            string[] phonemeArray = phonemes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var tokens = new List<int>();
            var skippedPhonemes = new List<string>();

            foreach (string phoneme in phonemeArray)
            {
                int tokenId = _tokenMap.GetTokenIdOrDefault(phoneme, -1);

                if (tokenId >= 0)
                {
                    tokens.Add(tokenId);
                }
                else
                {
                    skippedPhonemes.Add($"'{phoneme}'");
                }
            }

            if (skippedPhonemes.Count > 0)
            {
                Debug.LogWarning($"[DotNetG2PTokenizer] Skipped {skippedPhonemes.Count} unknown phonemes: {string.Join(", ", skippedPhonemes)}. Phonemes string: \"{phonemes}\"");
            }

            Debug.Log($"[DotNetG2PTokenizer] Tokenized: {phonemeArray.Length} phonemes -> {tokens.Count} tokens");

            return tokens.ToArray();
        }

        /// <summary>
        /// リソースを解放
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_g2pEngine is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _g2pEngine = null;

            _isInitialized = false;
            _isDisposed = true;
        }

        /// <summary>
        /// 日本語句読点を英語句読点にマッピング
        /// </summary>
        private static string MapJapanesePunctuation(char c)
        {
            return c switch
            {
                '。' => ".",
                '、' => ",",
                '！' => "!",
                '？' => "?",
                '；' => ";",
                '：' => ":",
                _ => null
            };
        }

        /// <summary>
        /// ASCII句読点かどうかを判定
        /// </summary>
        private static bool IsAsciiPunctuation(char c)
        {
            return c == '.' || c == ',' || c == '!' || c == '?' ||
                   c == ';' || c == ':' || c == '-' || c == '\'';
        }

        /// <summary>
        /// 日本語句読点を除去
        /// </summary>
        private static string RemoveJapanesePunctuation(string text)
        {
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c != '。' && c != '、' && c != '！' && c != '？' &&
                    c != '；' && c != '：' && c != '.' && c != ',' &&
                    c != '!' && c != '?' && c != ';' && c != ':')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private void ThrowIfNotInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("DotNetG2PTokenizer is not initialized. Call Initialize() first.");
            }
        }
    }
}
