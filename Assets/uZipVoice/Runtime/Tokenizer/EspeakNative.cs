using System;
using System.Runtime.InteropServices;

namespace uZipVoice.Tokenizer
{
    /// <summary>
    /// espeak-ng ネイティブライブラリのP/Invokeラッパー
    /// </summary>
    internal static class EspeakNative
    {
        private const string LibName = "espeak-ng";

        /// <summary>
        /// espeak-ngを初期化
        /// </summary>
        /// <param name="output">出力モード（0 = 同期再生なし）</param>
        /// <param name="buflength">バッファ長（0 = デフォルト）</param>
        /// <param name="path">データディレクトリのパス</param>
        /// <param name="options">オプション（0 = デフォルト）</param>
        /// <returns>成功時はサンプルレート、失敗時は-1</returns>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int espeak_Initialize(int output, int buflength, string path, int options);

        /// <summary>
        /// 音声を名前で設定
        /// </summary>
        /// <param name="name">音声名（例: "en-us"）</param>
        /// <returns>成功時は0</returns>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int espeak_SetVoiceByName(string name);

        /// <summary>
        /// テキストを音素に変換
        /// </summary>
        /// <param name="text">テキストポインタへのポインタ（呼び出し後に更新される）</param>
        /// <param name="textmode">テキストモード（0 = AUTO）</param>
        /// <param name="phonememode">音素モード（2 = IPA UTF-8）</param>
        /// <returns>音素文字列へのポインタ</returns>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr espeak_TextToPhonemes(ref IntPtr text, int textmode, int phonememode);

        /// <summary>
        /// サンプルレートを取得
        /// </summary>
        /// <returns>サンプルレート（Hz）</returns>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int espeak_GetSampleRate();

        /// <summary>
        /// espeak-ngを終了
        /// </summary>
        /// <returns>成功時は0</returns>
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int espeak_Terminate();

        // 定数
        public const int AUDIO_OUTPUT_SYNCHRONOUS = 0x02;
        public const int CHARS_AUTO = 0;
        public const int PHONEMES_IPA = 0x02;
    }
}
