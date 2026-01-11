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

                // IPA音素に変換
                IntPtr resultPtr = EspeakNative.espeak_TextToPhonemes(
                    ref pointerToText,
                    EspeakNative.CHARS_AUTO,
                    EspeakNative.PHONEMES_IPA
                );

                if (resultPtr == IntPtr.Zero)
                {
                    Debug.LogWarning($"[EspeakTokenizer] Failed to phonemize text: '{text}'");
                    return string.Empty;
                }

                return PtrToUtf8String(resultPtr);
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
        /// </summary>
        /// <param name="text">入力テキスト</param>
        /// <returns>トークンID配列（BOS, 音素ID..., EOS）</returns>
        public int[] Tokenize(string text)
        {
            ThrowIfNotInitialized();

            if (string.IsNullOrEmpty(text))
            {
                // 空の場合はBOS, EOSのみ
                return new[] { _tokenMap.BosId, _tokenMap.EosId };
            }

            string phonemes = TextToPhonemes(text);
            return PhonemeStringToTokens(phonemes);
        }

        /// <summary>
        /// 音素文字列をトークンID列に変換
        /// </summary>
        /// <param name="phonemes">IPA音素文字列</param>
        /// <returns>トークンID配列</returns>
        public int[] PhonemeStringToTokens(string phonemes)
        {
            if (string.IsNullOrEmpty(phonemes))
            {
                return new[] { _tokenMap.BosId, _tokenMap.EosId };
            }

            var tokens = new List<int> { _tokenMap.BosId };

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
                    // 未知の音素はスキップ（警告は出さない、頻繁に発生するため）
                }
            }

            tokens.Add(_tokenMap.EosId);
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
