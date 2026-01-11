using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace uZipVoice.Tokenizer
{
    /// <summary>
    /// 音素からトークンIDへのマッピングを管理するクラス
    /// </summary>
    public class TokenMap
    {
        // 特殊トークン
        public const string PAD = "_";
        public const string BOS = "^";
        public const string EOS = "$";
        public const string SPACE = " ";

        private readonly Dictionary<string, int> _phonemeToId;
        private readonly Dictionary<int, string> _idToPhoneme;

        /// <summary>
        /// トークンの総数
        /// </summary>
        public int Count => _phonemeToId.Count;

        /// <summary>
        /// PADトークンID
        /// </summary>
        public int PadId => GetTokenId(PAD);

        /// <summary>
        /// BOSトークンID
        /// </summary>
        public int BosId => GetTokenId(BOS);

        /// <summary>
        /// EOSトークンID
        /// </summary>
        public int EosId => GetTokenId(EOS);

        /// <summary>
        /// SPACEトークンID
        /// </summary>
        public int SpaceId => GetTokenId(SPACE);

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TokenMap()
        {
            _phonemeToId = new Dictionary<string, int>();
            _idToPhoneme = new Dictionary<int, string>();
        }

        /// <summary>
        /// ファイルパスからトークンマップを読み込む
        /// </summary>
        /// <param name="filePath">tokens.txtのファイルパス</param>
        public void LoadFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Token file not found", filePath);
            }

            string content = File.ReadAllText(filePath);
            LoadFromString(content);
        }

        /// <summary>
        /// TextAssetからトークンマップを読み込む
        /// </summary>
        /// <param name="textAsset">tokens.txtのTextAsset</param>
        public void LoadFromTextAsset(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                throw new ArgumentNullException(nameof(textAsset));
            }

            LoadFromString(textAsset.text);
        }

        /// <summary>
        /// 文字列からトークンマップを読み込む
        /// </summary>
        /// <param name="content">tokens.txtの内容</param>
        public void LoadFromString(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                throw new ArgumentException("Content cannot be null or empty", nameof(content));
            }

            _phonemeToId.Clear();
            _idToPhoneme.Clear();

            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0)
            {
                throw new FormatException("Token file is empty");
            }

            foreach (string line in lines)
            {
                // タブ区切りで音素とIDを分離
                int tabIndex = line.LastIndexOf('\t');
                if (tabIndex <= 0)
                {
                    throw new FormatException($"Invalid line format: '{line}'");
                }

                string phoneme = line.Substring(0, tabIndex);
                string idStr = line.Substring(tabIndex + 1);

                if (!int.TryParse(idStr, out int id))
                {
                    throw new FormatException($"Invalid token ID: '{idStr}'");
                }

                if (_phonemeToId.ContainsKey(phoneme))
                {
                    throw new FormatException($"Duplicate phoneme: '{phoneme}'");
                }

                _phonemeToId[phoneme] = id;
                _idToPhoneme[id] = phoneme;
            }
        }

        /// <summary>
        /// 音素からトークンIDを取得
        /// </summary>
        /// <param name="phoneme">音素</param>
        /// <returns>トークンID</returns>
        public int GetTokenId(string phoneme)
        {
            if (_phonemeToId.TryGetValue(phoneme, out int id))
            {
                return id;
            }
            throw new KeyNotFoundException($"Unknown phoneme: '{phoneme}'");
        }

        /// <summary>
        /// 音素からトークンIDを取得（存在しない場合はデフォルト値を返す）
        /// </summary>
        /// <param name="phoneme">音素</param>
        /// <param name="defaultValue">デフォルト値</param>
        /// <returns>トークンID</returns>
        public int GetTokenIdOrDefault(string phoneme, int defaultValue = -1)
        {
            return _phonemeToId.TryGetValue(phoneme, out int id) ? id : defaultValue;
        }

        /// <summary>
        /// トークンIDから音素を取得
        /// </summary>
        /// <param name="id">トークンID</param>
        /// <returns>音素</returns>
        public string GetPhoneme(int id)
        {
            if (_idToPhoneme.TryGetValue(id, out string phoneme))
            {
                return phoneme;
            }
            throw new KeyNotFoundException($"Unknown token ID: {id}");
        }

        /// <summary>
        /// 音素が存在するかどうかを確認
        /// </summary>
        /// <param name="phoneme">音素</param>
        /// <returns>存在する場合はtrue</returns>
        public bool ContainsPhoneme(string phoneme)
        {
            return _phonemeToId.ContainsKey(phoneme);
        }

        /// <summary>
        /// トークンIDが存在するかどうかを確認
        /// </summary>
        /// <param name="id">トークンID</param>
        /// <returns>存在する場合はtrue</returns>
        public bool ContainsId(int id)
        {
            return _idToPhoneme.ContainsKey(id);
        }

        /// <summary>
        /// 音素配列をトークンID配列に変換
        /// </summary>
        /// <param name="phonemes">音素配列</param>
        /// <returns>トークンID配列</returns>
        public int[] PhonemeToIds(string[] phonemes)
        {
            if (phonemes == null)
            {
                throw new ArgumentNullException(nameof(phonemes));
            }

            int[] ids = new int[phonemes.Length];
            for (int i = 0; i < phonemes.Length; i++)
            {
                ids[i] = GetTokenId(phonemes[i]);
            }
            return ids;
        }
    }
}
