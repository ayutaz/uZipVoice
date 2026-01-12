using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace uZipVoice.Tokenizer
{
    /// <summary>
    /// espeak-ngを使用したトークナイザー
    /// テキストをIPA音素に変換し、トークンIDに変換する
    /// </summary>
    public class EspeakTokenizer : ITokenizer
    {
        private TokenMap _tokenMap;
        private bool _isInitialized;
        private bool _isDisposed;
        private string _voice = "en-us";

        /// <summary>
        /// 初期化済みかどうか
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 使用する音声
        /// </summary>
        public string Voice
        {
            get => _voice;
            set
            {
                if (_isInitialized)
                {
                    SetVoice(value);
                }
                _voice = value;
            }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="tokenMap">トークンマップ（nullの場合は内部で作成）</param>
        public EspeakTokenizer(TokenMap tokenMap = null)
        {
            _tokenMap = tokenMap ?? new TokenMap();
        }

        /// <summary>
        /// トークナイザーを初期化
        /// </summary>
        /// <param name="dataPath">espeak-ng-dataディレクトリのパス</param>
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

            // espeak-ng初期化
            int result = EspeakNative.espeak_Initialize(
                EspeakNative.AUDIO_OUTPUT_SYNCHRONOUS,
                0,
                dataPath,
                0
            );

            if (result < 0)
            {
                throw new InvalidOperationException($"Failed to initialize espeak-ng. Error code: {result}");
            }

            Debug.Log($"[EspeakTokenizer] espeak-ng initialized. Sample rate: {result}");

            // 音声設定
            SetVoice(_voice);

            _isInitialized = true;
        }

        /// <summary>
        /// トークナイザーを非同期で初期化
        /// </summary>
        /// <param name="dataPath">espeak-ng-dataディレクトリのパス</param>
        public Task InitializeAsync(string dataPath)
        {
            Initialize(dataPath);
            return Task.CompletedTask;
        }

        /// <summary>
        /// トークンマップを読み込む
        /// </summary>
        /// <param name="tokensContent">tokens.txtの内容</param>
        public void LoadTokenMap(string tokensContent)
        {
            _tokenMap.LoadFromString(tokensContent);
        }

        /// <summary>
        /// トークンマップをTextAssetから読み込む
        /// </summary>
        /// <param name="textAsset">tokens.txtのTextAsset</param>
        public void LoadTokenMap(TextAsset textAsset)
        {
            _tokenMap.LoadFromTextAsset(textAsset);
        }

        /// <summary>
        /// 音声を設定
        /// </summary>
        /// <param name="voiceName">音声名（例: "en-us"）</param>
        public void SetVoice(string voiceName)
        {
            int result = EspeakNative.espeak_SetVoiceByName(voiceName);
            if (result != 0)
            {
                Debug.LogWarning($"[EspeakTokenizer] Failed to set voice '{voiceName}'. Error code: {result}");
            }
            else
            {
                Debug.Log($"[EspeakTokenizer] Voice set to '{voiceName}'");
                _voice = voiceName;
            }
        }

        /// <summary>
        /// テキストを音素列に変換
        /// </summary>
        /// <param name="text">入力テキスト</param>
        /// <returns>IPA音素文字列</returns>
        public string TextToPhonemes(string text)
        {
            ThrowIfNotInitialized();

            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            IntPtr textPtr = IntPtr.Zero;
            try
            {
                // UTF-8でテキストをエンコード（null終端）
                byte[] textBytes = Encoding.UTF8.GetBytes(text + "\0");
                textPtr = Marshal.AllocHGlobal(textBytes.Length);
                Marshal.Copy(textBytes, 0, textPtr, textBytes.Length);

                IntPtr pointerToText = textPtr;
                var allPhonemes = new StringBuilder();

                // espeak_TextToPhonemes は1回の呼び出しで1節/句だけを処理する
                // テキスト全体を処理するにはポインタがnullになるまでループする必要がある
                while (true)
                {
                    // IPA音素に変換
                    IntPtr resultPtr = EspeakNative.espeak_TextToPhonemes(
                        ref pointerToText,
                        EspeakNative.CHARS_AUTO,
                        EspeakNative.PHONEMES_IPA
                    );

                    if (resultPtr == IntPtr.Zero)
                    {
                        // 処理するテキストがなくなった
                        break;
                    }

                    string phonemeChunk = PtrToUtf8String(resultPtr);
                    if (string.IsNullOrEmpty(phonemeChunk))
                    {
                        break;
                    }

                    allPhonemes.Append(phonemeChunk);

                    // pointerToTextが更新されてnull終端に達したかチェック
                    if (pointerToText == IntPtr.Zero)
                    {
                        break;
                    }

                    // テキストの終端（null文字）に達したかチェック
                    byte nextByte = Marshal.ReadByte(pointerToText);
                    if (nextByte == 0)
                    {
                        break;
                    }
                }

                if (allPhonemes.Length == 0)
                {
                    Debug.LogWarning($"[EspeakTokenizer] Failed to phonemize text: '{text}'");
                    return string.Empty;
                }

                return allPhonemes.ToString();
            }
            finally
            {
                if (textPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(textPtr);
                }
            }
        }

        /// <summary>
        /// テキストをトークンID列に変換
        /// NOTE: Python側と同じく、BOS/EOSトークンは追加しない
        /// NOTE: espeak_TextToPhonemes は句読点を出力しないため、
        ///       元テキストから句読点を抽出してトークンに追加する（piper_phonemize互換）
        /// </summary>
        /// <param name="text">入力テキスト</param>
        /// <returns>トークンID配列</returns>
        public int[] Tokenize(string text)
        {
            ThrowIfNotInitialized();

            if (string.IsNullOrEmpty(text))
            {
                // 空の場合は空配列
                return Array.Empty<int>();
            }

            string phonemes = TextToPhonemes(text);
            var tokens = new List<int>(PhonemeStringToTokens(phonemes));

            // piper_phonemize互換: 元テキストの末尾句読点をトークンに追加
            // espeak_TextToPhonemes は句読点を音素に変換しないため、手動で追加
            if (text.Length > 0)
            {
                char lastChar = text[text.Length - 1];
                // 句読点文字かどうかを確認
                if (IsPunctuation(lastChar))
                {
                    int punctuationToken = _tokenMap.GetTokenIdOrDefault(lastChar.ToString(), -1);
                    if (punctuationToken >= 0)
                    {
                        tokens.Add(punctuationToken);
                        Debug.Log($"[EspeakTokenizer] Added trailing punctuation '{lastChar}' as token {punctuationToken}");
                    }
                }
            }

            return tokens.ToArray();
        }

        /// <summary>
        /// 文字が句読点かどうかを判定
        /// </summary>
        private static bool IsPunctuation(char c)
        {
            // Python piper_phonemize が保持する句読点
            return c == '.' || c == ',' || c == '!' || c == '?' ||
                   c == ';' || c == ':' || c == '-' || c == '\'';
        }

        /// <summary>
        /// 音素文字列をトークンID列に変換
        /// NOTE: Python側のEspeakTokenizerはBOS/EOSトークンを追加しないため、
        ///       Unity側でも追加しない（ONNXモデルとの互換性のため）
        /// </summary>
        /// <param name="phonemes">IPA音素文字列</param>
        /// <returns>トークンID配列</returns>
        public int[] PhonemeStringToTokens(string phonemes)
        {
            if (string.IsNullOrEmpty(phonemes))
            {
                return Array.Empty<int>();
            }

            var tokens = new List<int>();
            var skippedPhonemes = new List<string>();

            // 各文字を音素として処理
            foreach (char c in phonemes)
            {
                string phoneme = c.ToString();
                int tokenId = _tokenMap.GetTokenIdOrDefault(phoneme, -1);

                if (tokenId >= 0)
                {
                    tokens.Add(tokenId);
                }
                else
                {
                    // 未知の音素を記録
                    skippedPhonemes.Add($"'{phoneme}'(U+{((int)c):X4})");
                }
            }

            // スキップされた音素があればログに出力
            if (skippedPhonemes.Count > 0)
            {
                Debug.LogWarning($"[EspeakTokenizer] Skipped {skippedPhonemes.Count} unknown phonemes: {string.Join(", ", skippedPhonemes)}. Phonemes string: \"{phonemes}\"");
            }

            Debug.Log($"[EspeakTokenizer] Tokenized: phonemes={phonemes.Length} chars, tokens={tokens.Count}");

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

            if (_isInitialized)
            {
                EspeakNative.espeak_Terminate();
                _isInitialized = false;
            }

            _isDisposed = true;
        }

        private void ThrowIfNotInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("EspeakTokenizer is not initialized. Call Initialize() first.");
            }
        }

        private static string PtrToUtf8String(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return string.Empty;
            }

            var byteList = new List<byte>();
            for (int offset = 0; ; offset++)
            {
                byte b = Marshal.ReadByte(ptr, offset);
                if (b == 0) break;
                byteList.Add(b);
            }

            return Encoding.UTF8.GetString(byteList.ToArray());
        }
    }
}
