# プロジェクト構成

## 1. フォルダ構成

```
uZipVoice/
├── Assets/
│   ├── StreamingAssets/
│   │   └── espeak-ng-data/           # espeak-ng言語データ
│   │
│   └── uZipVoice/
│       ├── Models/                    # ONNXモデル（git管理外）
│       │   ├── text_encoder.onnx
│       │   ├── fm_decoder.onnx
│       │   └── vocos_opset15.onnx
│       │
│       ├── Runtime/                   # ランタイムスクリプト
│       │   ├── Core/
│       │   │   ├── ZipVoiceManager.cs
│       │   │   ├── ZipVoiceConfig.cs
│       │   │   └── SynthesisOptions.cs
│       │   │
│       │   ├── Inference/
│       │   │   ├── TextEncoder.cs
│       │   │   ├── FMDecoder.cs
│       │   │   ├── Vocos.cs
│       │   │   └── EulerSolver.cs
│       │   │
│       │   ├── Audio/
│       │   │   ├── ISTFTProcessor.cs
│       │   │   └── FeatureExtractor.cs
│       │   │
│       │   └── Tokenizer/
│       │       ├── ITokenizer.cs
│       │       ├── EspeakNative.cs
│       │       ├── EspeakTokenizer.cs
│       │       └── TokenMap.cs
│       │
│       ├── Editor/                    # エディタ拡張
│       │   └── TTSSampleSceneCreator.cs
│       │
│       ├── Plugins/                   # ネイティブプラグイン
│       │   ├── NWaves.dll
│       │   └── Windows/x64/
│       │       └── libespeak-ng.dll
│       │
│       ├── Resources/                 # リソースファイル
│       │   └── tokens.txt
│       │
│       ├── Samples/                   # サンプル
│       │   ├── TTSSample.unity
│       │   ├── TTSSampleController.cs
│       │   └── Audio/
│       │       ├── prompt_english.wav
│       │       └── prompt_english.txt
│       │
│       └── Tests/                     # テスト
│           ├── Editor/
│           │   ├── uZipVoice.Tests.Editor.asmdef
│           │   ├── TokenMapTests.cs
│           │   ├── EulerSolverTests.cs
│           │   └── EspeakTokenizerTests.cs
│           └── Runtime/
│               └── uZipVoice.Tests.Runtime.asmdef
│
├── docs/                              # ドキュメント
│   ├── 01-research.md
│   ├── 02-architecture.md
│   ├── 03-project-structure.md
│   ├── 04-test-specification.md
│   ├── 05-implementation-progress.md
│   └── 06-onnx-export.md
│
├── Packages/
│   └── manifest.json
│
├── ProjectSettings/
│
├── .gitignore
├── .gitattributes
├── CLAUDE.md
├── README.md
├── README_ja.md
└── README_zh.md
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
    public async UniTask InitializeAsync();

    // 音声合成
    public async UniTask<AudioClip> SynthesizeAsync(
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
public interface ITokenizer : IDisposable
{
    bool IsInitialized { get; }

    void Initialize(string dataPath);
    Task InitializeAsync(string dataPath);

    int[] Tokenize(string text);
    string TextToPhonemes(string text);
}
```

### EulerSolver

```csharp
public class EulerSolver
{
    public int NumSteps { get; }
    public float TShift { get; }

    public EulerSolver(int numSteps, float tShift = 0.5f);

    public float[] GetTimesteps();
    public float GetTimestep(int index);
    public float GetDt(int stepIndex);

    public float[] Step(float[] x, float[] velocity, int stepIndex);
    public void StepInPlace(float[] x, float[] velocity, int stepIndex);
}
```

### TokenMap

```csharp
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
    public void LoadFromTextAsset(TextAsset textAsset);
    public void LoadFromString(string content);

    // トークン操作
    public int GetTokenId(string phoneme);
    public int GetTokenIdOrDefault(string phoneme, int defaultValue = -1);
    public string GetPhoneme(int id);
    public bool ContainsPhoneme(string phoneme);
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
│   └── FeatureExtractor
├── uZipVoice.Tokenizer
│   ├── ITokenizer
│   ├── EspeakNative
│   ├── EspeakTokenizer
│   └── TokenMap
└── uZipVoice.Samples
    └── TTSSampleController
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
        "Unity.InferenceEngine",
        "UniTask"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": true,
    "overrideReferences": true,
    "precompiledReferences": [
        "NWaves.dll"
    ]
}
```

### Tests

**uZipVoice.Tests.Editor.asmdef**
```json
{
    "name": "uZipVoice.Tests.Editor",
    "rootNamespace": "uZipVoice.Tests",
    "references": [
        "uZipVoice.Runtime",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll",
        "NWaves.dll"
    ],
    "defineConstraints": ["UNITY_INCLUDE_TESTS"]
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

## 6. 実装状況

| コンポーネント | 状態 | テスト |
|---------------|------|--------|
| TokenMap | ✅ 完了 | 24テスト |
| EulerSolver | ✅ 完了 | 32テスト |
| EspeakTokenizer | ✅ 完了 | 19テスト |
| TextEncoder | ✅ 完了 | - |
| FMDecoder | ✅ 完了 | - |
| Vocos | ✅ 完了 | - |
| ISTFTProcessor | ✅ 完了 | - |
| FeatureExtractor | ✅ 完了 | - |
| ZipVoiceManager | ✅ 完了 | - |
| TTSSampleController | ✅ 完了 | - |

**合計テスト数**: 75テスト（全成功）

---

## 7. Git管理

### .gitignore（抜粋）

```gitignore
# Large model files
*.onnx

# Unity generated
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
```

### リポジトリ

- **GitHub**: https://github.com/ayutaz/uZipVoice
- **ONNX Models**: https://huggingface.co/ayousanz/uZipVoice-onnx
