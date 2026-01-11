# プロジェクト構成

## 1. フォルダ構成

```
uZipVoice/
├── Assets/
│   └── uZipVoice/
│       ├── Models/                      # ONNXモデル（git管理外）
│       │   ├── text_encoder.onnx
│       │   ├── fm_decoder.onnx
│       │   └── vocos_opset15.onnx
│       │
│       ├── Runtime/                     # ランタイムスクリプト
│       │   ├── Core/
│       │   │   ├── ZipVoiceManager.cs      # メインAPI
│       │   │   └── ZipVoiceConfig.cs       # 設定クラス
│       │   │
│       │   ├── Inference/
│       │   │   ├── TextEncoder.cs          # Text Encoder推論
│       │   │   ├── FMDecoder.cs            # FM Decoder推論
│       │   │   ├── Vocos.cs                # Vocos推論
│       │   │   └── EulerSolver.cs          # ODE積分
│       │   │
│       │   ├── Audio/
│       │   │   ├── ISTFTProcessor.cs       # ISTFT処理（NWaves使用）
│       │   │   ├── AudioUtility.cs         # オーディオユーティリティ
│       │   │   └── FeatureExtractor.cs     # メル特徴抽出
│       │   │
│       │   └── Tokenizer/
│       │       ├── ITokenizer.cs           # トークナイザーインターフェース
│       │       ├── EspeakTokenizer.cs      # espeak-ng実装
│       │       └── TokenMap.cs             # トークンマッピング
│       │
│       ├── Editor/                      # エディタ拡張
│       │   ├── ZipVoiceEditorWindow.cs     # テスト用エディタウィンドウ
│       │   └── ModelImporter.cs            # モデルインポート補助
│       │
│       ├── Plugins/                     # ネイティブプラグイン
│       │   ├── Windows/
│       │   │   └── x64/
│       │   │       └── libespeak-ng.dll
│       │   ├── macOS/
│       │   │   └── x64/
│       │   │       └── libespeak-ng.1.dylib
│       │   ├── Android/
│       │   │   └── arm64-v8a/
│       │   │       └── libespeak-ng.so
│       │   └── Managed/
│       │       └── NWaves.dll
│       │
│       ├── Resources/                   # リソースファイル
│       │   ├── tokens.txt                  # トークンマッピング
│       │   └── model.json                  # モデル設定
│       │
│       ├── StreamingAssets/             # ストリーミングアセット
│       │   └── espeak-ng-data/             # espeak-ng言語データ
│       │
│       └── Samples/                     # サンプル
│           ├── Scenes/
│           │   └── TTSSample.unity
│           ├── Scripts/
│           │   └── TTSSampleController.cs
│           └── Audio/
│               └── prompt.wav              # サンプルプロンプト音声
│
├── Tests/                              # テスト
│           ├── Editor/
│           │   ├── uZipVoice.Tests.Editor.asmdef
│           │   ├── TestUtility.cs
│           │   ├── TokenMapTests.cs
│           │   └── EulerSolverTests.cs
│           └── Runtime/
│               └── uZipVoice.Tests.Runtime.asmdef
│
├── docs/                                # ドキュメント
│   ├── 01-research.md
│   ├── 02-architecture.md
│   ├── 03-project-structure.md
│   ├── 04-test-specification.md
│   └── 05-implementation-progress.md
│
├── Packages/
│   └── manifest.json
│
├── ProjectSettings/
│
├── .gitignore
├── .gitattributes
└── CLAUDE.md
```

---

## 2. 主要クラス設計

### ZipVoiceManager（メインAPI）

```csharp
public class ZipVoiceManager : MonoBehaviour
{
    // 公開プロパティ
    public bool IsInitialized { get; }
    public bool IsProcessing { get; }

    // 初期化
    public async Task InitializeAsync();

    // 音声合成
    public async Task<AudioClip> SynthesizeAsync(
        string text,
        AudioClip promptAudio,
        string promptText,
        SynthesisOptions options = null
    );

    // 音声合成（ストリーミング）
    public IAsyncEnumerable<AudioClip> SynthesizeStreamAsync(
        string text,
        AudioClip promptAudio,
        string promptText,
        SynthesisOptions options = null
    );
}

public class SynthesisOptions
{
    public int NumSteps { get; set; } = 16;
    public float GuidanceScale { get; set; } = 1.0f;
    public float Speed { get; set; } = 1.0f;
}
```

### ITokenizer（トークナイザーインターフェース）

```csharp
public interface ITokenizer
{
    bool IsInitialized { get; }

    Task InitializeAsync(string dataPath);

    int[] Tokenize(string text);

    string[] TextToPhonemes(string text);
}
```

### EulerSolver（実装済み）

```csharp
namespace uZipVoice.Inference
{
    public class EulerSolver
    {
        // プロパティ
        public int NumSteps { get; }
        public float TShift { get; }
        public float TStart { get; }
        public float TEnd { get; }

        // コンストラクタ
        public EulerSolver(int numSteps, float tShift = 0.5f, float tStart = 0f, float tEnd = 1f);

        // タイムステップ取得
        public float[] GetTimesteps();
        public float GetTimestep(int index);
        public float GetDt(int stepIndex);

        // Euler積分ステップ
        public float[] Step(float[] x, float[] velocity, int stepIndex);
        public void StepInPlace(float[] x, float[] velocity, int stepIndex);
    }
}
```

### TokenMap（実装済み）

```csharp
namespace uZipVoice.Tokenizer
{
    public class TokenMap
    {
        // 特殊トークン定数
        public const string PAD = "_";
        public const string BOS = "^";
        public const string EOS = "$";
        public const string SPACE = " ";

        // プロパティ
        public int Count { get; }
        public int PadId { get; }
        public int BosId { get; }
        public int EosId { get; }
        public int SpaceId { get; }

        // 読み込み
        public void LoadFromFile(string filePath);
        public void LoadFromTextAsset(TextAsset textAsset);
        public void LoadFromString(string content);

        // トークン操作
        public int GetTokenId(string phoneme);
        public int GetTokenIdOrDefault(string phoneme, int defaultValue = -1);
        public string GetPhoneme(int id);
        public bool ContainsPhoneme(string phoneme);
        public bool ContainsId(int id);
        public int[] PhonemeToIds(string[] phonemes);
    }
}
```

---

## 3. 名前空間

```
uZipVoice
├── uZipVoice.Core
│   ├── ZipVoiceManager
│   ├── ZipVoiceConfig
│   └── SynthesisOptions
├── uZipVoice.Inference
│   ├── TextEncoder
│   ├── FMDecoder
│   ├── Vocos
│   └── EulerSolver
├── uZipVoice.Audio
│   ├── ISTFTProcessor
│   ├── AudioUtility
│   └── FeatureExtractor
├── uZipVoice.Tokenizer
│   ├── ITokenizer
│   ├── EspeakTokenizer
│   └── TokenMap
└── uZipVoice.Editor
    ├── ZipVoiceEditorWindow
    └── ModelImporter
```

---

## 4. Assembly Definition

### Runtime

**uZipVoice.Runtime.asmdef**
```json
{
    "name": "uZipVoice.Runtime",
    "rootNamespace": "uZipVoice",
    "references": [
        "Unity.InferenceEngine"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": true
}
```

### Editor

**uZipVoice.Editor.asmdef**
```json
{
    "name": "uZipVoice.Editor",
    "rootNamespace": "uZipVoice.Editor",
    "references": [
        "uZipVoice.Runtime",
        "Unity.InferenceEngine"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": []
}
```

---

## 5. 依存関係グラフ

```
┌─────────────────────────────────────────────────────────┐
│                    ZipVoiceManager                      │
└────────────────────────┬────────────────────────────────┘
                         │
         ┌───────────────┼───────────────┐
         │               │               │
         ▼               ▼               ▼
┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│ TextEncoder │  │  FMDecoder  │  │    Vocos    │
└──────┬──────┘  └──────┬──────┘  └──────┬──────┘
       │                │                │
       │         ┌──────┴──────┐         │
       │         │             │         │
       │         ▼             │         ▼
       │  ┌─────────────┐      │  ┌─────────────┐
       │  │ EulerSolver │      │  │ISTFTProcessor│
       │  └─────────────┘      │  └─────────────┘
       │                       │         │
       ▼                       ▼         ▼
┌─────────────┐         ┌─────────────────────┐
│EspeakTokenizer│        │     NWaves.dll      │
└──────┬──────┘         └─────────────────────┘
       │
       ▼
┌─────────────┐
│libespeak-ng │
│   (native)  │
└─────────────┘
```

---

## 6. 設定ファイル

### model.json

```json
{
  "model": {
    "fm_decoder_dim": 512,
    "text_encoder_dim": 192,
    "feat_dim": 100
  },
  "feature": {
    "sampling_rate": 24000,
    "type": "vocos"
  },
  "audio": {
    "n_fft": 1024,
    "hop_length": 256
  }
}
```

### tokens.txt

```
_   0
^   1
$   2
    3
h   20
ə   59
l   24
...
```

---

## 7. Git管理

### .gitignore（抜粋）

```gitignore
# Large model files
*.onnx

# Native plugins (optional)
# Assets/uZipVoice/Plugins/

# espeak-ng data
Assets/uZipVoice/StreamingAssets/espeak-ng-data/
```

### Git LFS（推奨）

大容量ファイルを管理する場合:

```
*.onnx filter=lfs diff=lfs merge=lfs -text
*.dll filter=lfs diff=lfs merge=lfs -text
*.dylib filter=lfs diff=lfs merge=lfs -text
*.so filter=lfs diff=lfs merge=lfs -text
```

---

## 8. 実装優先順位

| 順序 | コンポーネント | 依存関係 | 状態 |
|------|---------------|---------|------|
| 1 | プロジェクト基盤 | - | ✅ 完了 |
| 2 | TokenMap | - | ✅ 完了 |
| 3 | EulerSolver | - | ✅ 完了 |
| 4 | EspeakTokenizer | espeak-ng DLL | 🔲 未実装 |
| 5 | TextEncoder | Inference Engine | 🔲 未実装 |
| 6 | FMDecoder | EulerSolver | 🔲 未実装 |
| 7 | Vocos | Inference Engine | 🔲 未実装 |
| 8 | ISTFTProcessor | NWaves | 🔲 未実装 |
| 9 | FeatureExtractor | - | 🔲 未実装 |
| 10 | ZipVoiceManager | 全コンポーネント | 🔲 未実装 |
| 11 | サンプル・テスト | ZipVoiceManager | 🔲 未実装 |

### テスト実装状況

| コンポーネント | テスト数 | 状態 |
|--------------|---------|------|
| TokenMapTests | 24 | ✅ 全テスト成功 |
| EulerSolverTests | 32 | ✅ 全テスト成功 |
| 合計 | 56 | ✅ 全テスト成功 |
