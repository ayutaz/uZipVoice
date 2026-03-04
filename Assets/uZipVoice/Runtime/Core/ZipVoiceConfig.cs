using UnityEngine;

namespace uZipVoice.Core
{
    /// <summary>
    /// ZipVoice設定
    /// </summary>
    [CreateAssetMenu(fileName = "ZipVoiceConfig", menuName = "uZipVoice/Config")]
    public class ZipVoiceConfig : ScriptableObject
    {
        [Header("Audio Settings")]
        [Tooltip("サンプルレート")]
        public int SampleRate = 24000;

        [Tooltip("FFTサイズ")]
        public int NFft = 1024;

        [Tooltip("ホップ長")]
        public int HopLength = 256;

        [Tooltip("メルバンド数")]
        public int NMels = 100;

        [Header("Inference Settings")]
        [Tooltip("Euler Solverのステップ数（蒸留モデル推奨: 4-8）")]
        [Range(4, 16)]
        public int NumSteps = 8;

        [Tooltip("タイムシフトパラメータ")]
        [Range(0.1f, 1.0f)]
        public float TShift = 0.5f;

        [Tooltip("CFGスケール")]
        [Range(0f, 3f)]
        public float GuidanceScale = 1.0f;

        [Tooltip("生成速度")]
        [Range(0.5f, 2.0f)]
        public float Speed = 1.0f;

        [Header("Tokenizer Settings")]
        [Tooltip("使用言語")]
        public Language Language = Language.English;

        [Tooltip("espeak-ng音声")]
        public string Voice = "en-us";
    }

    /// <summary>
    /// 音声合成オプション
    /// </summary>
    public class SynthesisOptions
    {
        /// <summary>
        /// Euler Solverのステップ数（蒸留モデル推奨: 4-8）
        /// </summary>
        public int NumSteps { get; set; } = 8;

        /// <summary>
        /// CFGスケール
        /// </summary>
        public float GuidanceScale { get; set; } = 1.0f;

        /// <summary>
        /// 生成速度
        /// </summary>
        public float Speed { get; set; } = 1.0f;
    }
}
