using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace uZipVoice.Tests
{
    /// <summary>
    /// テスト用ユーティリティクラス
    /// </summary>
    public static class TestUtility
    {
        /// <summary>
        /// テスト用のtokens.txtファイル内容を生成
        /// </summary>
        public static string CreateTestTokensContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine("_\t0");   // PAD
            sb.AppendLine("^\t1");   // BOS
            sb.AppendLine("$\t2");   // EOS
            sb.AppendLine(" \t3");   // SPACE
            sb.AppendLine("!\t4");
            sb.AppendLine("h\t20");
            sb.AppendLine("ə\t59");
            sb.AppendLine("l\t24");
            sb.AppendLine("ˈ\t120");
            sb.AppendLine("o\t27");
            sb.AppendLine("ʊ\t100");
            sb.AppendLine("w\t35");
            sb.AppendLine("ɜ\t62");
            sb.AppendLine("ː\t122");
            sb.AppendLine("d\t17");
            return sb.ToString();
        }

        /// <summary>
        /// テスト用のtokens.txt（日本語音素含む）を生成
        /// </summary>
        public static string CreateTestTokensContentWithJapanese()
        {
            var sb = new StringBuilder();
            // 基本トークン
            sb.AppendLine("_\t0");   // PAD
            sb.AppendLine("^\t1");   // BOS
            sb.AppendLine("$\t2");   // EOS
            sb.AppendLine(" \t3");   // SPACE
            sb.AppendLine("!\t4");
            sb.AppendLine(".\t10");
            sb.AppendLine(",\t11");
            // 英語IPA音素（一部）
            sb.AppendLine("a\t14");
            sb.AppendLine("d\t17");
            sb.AppendLine("h\t20");
            sb.AppendLine("i\t21");
            sb.AppendLine("k\t23");
            sb.AppendLine("l\t24");
            sb.AppendLine("m\t25");
            sb.AppendLine("n\t26");
            sb.AppendLine("o\t27");
            sb.AppendLine("p\t28");
            sb.AppendLine("r\t29");
            sb.AppendLine("s\t30");
            sb.AppendLine("t\t31");
            sb.AppendLine("u\t32");
            sb.AppendLine("w\t35");
            sb.AppendLine("y\t36");
            sb.AppendLine("z\t37");
            // 日本語音素
            sb.AppendLine("A\t360");
            sb.AppendLine("E\t361");
            sb.AppendLine("I\t362");
            sb.AppendLine("N\t363");
            sb.AppendLine("O\t364");
            sb.AppendLine("U\t365");
            sb.AppendLine("by\t366");
            sb.AppendLine("ch\t367");
            sb.AppendLine("cl\t368");
            sb.AppendLine("dy\t369");
            sb.AppendLine("dz\t370");
            sb.AppendLine("gw\t371");
            sb.AppendLine("gy\t372");
            sb.AppendLine("hy\t373");
            sb.AppendLine("kw\t374");
            sb.AppendLine("ky\t375");
            sb.AppendLine("my\t376");
            sb.AppendLine("ny\t377");
            sb.AppendLine("pau\t378");
            sb.AppendLine("py\t379");
            sb.AppendLine("ry\t380");
            sb.AppendLine("sh\t381");
            sb.AppendLine("sil\t382");
            sb.AppendLine("ts\t383");
            sb.AppendLine("ty\t384");
            sb.AppendLine("[H]\t385");
            return sb.ToString();
        }

        /// <summary>
        /// テスト用の一時ファイルを作成
        /// </summary>
        public static string CreateTempFile(string content)
        {
            string tempPath = Path.Combine(Application.temporaryCachePath, $"test_{Guid.NewGuid()}.txt");
            File.WriteAllText(tempPath, content, Encoding.UTF8);
            return tempPath;
        }

        /// <summary>
        /// 一時ファイルを削除
        /// </summary>
        public static void DeleteTempFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// テスト用の正弦波データを生成
        /// </summary>
        /// <param name="frequency">周波数 (Hz)</param>
        /// <param name="sampleRate">サンプルレート (Hz)</param>
        /// <param name="duration">長さ (秒)</param>
        /// <returns>正弦波サンプル配列</returns>
        public static float[] GenerateSineWave(float frequency, int sampleRate, float duration)
        {
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t);
            }

            return samples;
        }

        /// <summary>
        /// 2つの配列が許容誤差内で等しいか比較
        /// </summary>
        public static bool ArraysEqual(float[] a, float[] b, float tolerance = 1e-5f)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (Mathf.Abs(a[i] - b[i]) > tolerance)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// ランダムなfloat配列を生成
        /// </summary>
        public static float[] GenerateRandomArray(int length, float min = -1f, float max = 1f)
        {
            float[] result = new float[length];
            System.Random random = new System.Random(42); // 再現性のため固定シード

            for (int i = 0; i < length; i++)
            {
                result[i] = (float)(random.NextDouble() * (max - min) + min);
            }

            return result;
        }

        /// <summary>
        /// 配列が単調増加かどうかを確認
        /// </summary>
        public static bool IsMonotonicallyIncreasing(float[] array)
        {
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i] < array[i - 1])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 配列が単調減少かどうかを確認
        /// </summary>
        public static bool IsMonotonicallyDecreasing(float[] array)
        {
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i] > array[i - 1])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
