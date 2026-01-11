# テスト項目書

## 1. テスト方針

### Unity Test Framework

- **フレームワーク**: Unity Test Framework (NUnit 3.5ベース)
- **テストタイプ**: Edit Mode Tests（純粋C#テスト）
- **属性**: `[Test]`（同期テスト）、`[UnityTest]`（コルーチンテスト）

### テスト分類

| 分類 | 説明 | テストモード |
|------|------|-------------|
| Unit Tests | 単一クラス・メソッドのテスト | Edit Mode |
| Integration Tests | 複数コンポーネント連携テスト | Edit Mode |
| E2E Tests | 全体パイプラインテスト | Play Mode |

### テストフォルダ構成

```
Assets/uZipVoice/
└── Tests/
    ├── Editor/                          # Edit Mode Tests
    │   ├── uZipVoice.Tests.Editor.asmdef
    │   ├── TokenMapTests.cs
    │   ├── EspeakTokenizerTests.cs
    │   ├── EulerSolverTests.cs
    │   ├── ISTFTProcessorTests.cs
    │   ├── TextEncoderTests.cs
    │   ├── FMDecoderTests.cs
    │   ├── VocosTests.cs
    │   └── IntegrationTests.cs
    │
    └── Runtime/                         # Play Mode Tests
        ├── uZipVoice.Tests.Runtime.asmdef
        └── ZipVoiceManagerTests.cs
```

---

## 2. コンポーネント別テスト項目

### 2.1 TokenMap

トークンマッピング（tokens.txt → Dictionary）のテスト。

| ID | テスト名 | 説明 | 優先度 |
|----|---------|------|--------|
| TM-001 | LoadTokensFromFile | tokens.txtファイルの正常読み込み | 高 |
| TM-002 | LoadTokensFromTextAsset | TextAssetからの読み込み | 高 |
| TM-003 | GetTokenId_ValidPhoneme | 有効な音素のID取得 | 高 |
| TM-004 | GetTokenId_InvalidPhoneme | 無効な音素でのエラー処理 | 高 |
| TM-005 | GetTokenId_SpecialTokens | 特殊トークン（PAD, BOS, EOS）の確認 | 高 |
| TM-006 | TokenCount | トークン総数の確認 | 中 |
| TM-007 | ContainsPhoneme | 音素存在確認 | 中 |
| TM-008 | LoadTokens_EmptyFile | 空ファイルでのエラー処理 | 低 |
| TM-009 | LoadTokens_MalformedFile | 不正形式ファイルでのエラー処理 | 低 |

**テストデータ**:
```
# 期待値
PAD ("_") → 0
BOS ("^") → 1
EOS ("$") → 2
SPACE (" ") → 3
"h" → 20
"ə" → 59
```

---

### 2.2 EspeakTokenizer

espeak-ngを使用したG2P変換のテスト。

| ID | テスト名 | 説明 | 優先度 |
|----|---------|------|--------|
| ET-001 | Initialize_Success | 正常初期化 | 高 |
| ET-002 | Initialize_InvalidPath | 無効なデータパスでのエラー | 高 |
| ET-003 | TextToPhonemes_SimpleText | 単純テキストの音素変換 | 高 |
| ET-004 | TextToPhonemes_HelloWorld | "Hello world" → IPA | 高 |
| ET-005 | TextToPhonemes_Punctuation | 句読点付きテキスト | 高 |
| ET-006 | TextToPhonemes_Numbers | 数字を含むテキスト | 中 |
| ET-007 | TextToPhonemes_SpecialChars | 特殊文字の処理 | 中 |
| ET-008 | Tokenize_ValidText | テキスト→トークンID変換 | 高 |
| ET-009 | Tokenize_EmptyText | 空文字列の処理 | 高 |
| ET-010 | Tokenize_LongText | 長文テキストの処理 | 中 |
| ET-011 | SetVoice_EnUS | en-us音声設定 | 高 |
| ET-012 | Dispose_Cleanup | リソース解放確認 | 中 |

**テストデータ**:
```csharp
// ET-004: TextToPhonemes_HelloWorld
Input: "Hello world"
Expected: "həlˈoʊ wˈɜːld" (または同等のIPA)

// ET-005: TextToPhonemes_Punctuation
Input: "Hello, world!"
Expected: "həlˈoʊ, wˈɜːld!" (句読点保持)
```

---

### 2.3 EulerSolver

ODE積分ソルバーのテスト。

| ID | テスト名 | 説明 | 優先度 |
|----|---------|------|--------|
| ES-001 | GetTimesteps_DefaultParams | デフォルトパラメータでのタイムステップ生成 | 高 |
| ES-002 | GetTimesteps_NumSteps8 | 8ステップでの生成 | 高 |
| ES-003 | GetTimesteps_NumSteps16 | 16ステップでの生成 | 高 |
| ES-004 | GetTimesteps_TShift0_5 | t_shift=0.5での生成 | 高 |
| ES-005 | GetTimesteps_TShift1_0 | t_shift=1.0（線形）での生成 | 中 |
| ES-006 | GetTimesteps_StartEnd | 開始=0, 終了=1の確認 | 高 |
| ES-007 | GetTimesteps_Monotonic | 単調増加の確認 | 高 |
| ES-008 | Step_SingleStep | 単一ステップ計算 | 高 |
| ES-009 | Step_FullIntegration | 全ステップ積分 | 高 |
| ES-010 | Constructor_InvalidNumSteps | 無効なステップ数でのエラー | 中 |

**テストデータ**:
```csharp
// ES-004: GetTimesteps_TShift0_5
numSteps = 4, t_shift = 0.5f
Expected: [0.0, 0.111, 0.25, 0.429, 1.0] (近似値)

// タイムステップ計算式
// t_shifted = t_shift * t / (1 + (t_shift - 1) * t)
```

---

### 2.4 ISTFTProcessor

逆短時間フーリエ変換のテスト。

| ID | テスト名 | 説明 | 優先度 |
|----|---------|------|--------|
| IS-001 | Constructor_ValidParams | 有効なパラメータでの初期化 | 高 |
| IS-002 | Constructor_InvalidNfft | 無効なn_fftでのエラー | 中 |
| IS-003 | Constructor_InvalidHopLength | 無効なhop_lengthでのエラー | 中 |
| IS-004 | Process_ValidInput | 有効なSTFT係数からの波形生成 | 高 |
| IS-005 | Process_OutputShape | 出力波形の形状確認 | 高 |
| IS-006 | Process_OutputRange | 出力値の範囲確認（-1.0〜1.0） | 中 |
| IS-007 | Process_ZeroMagnitude | ゼロ振幅での処理 | 中 |
| IS-008 | Process_SinWave | 正弦波の再構成テスト | 高 |
| IS-009 | HannWindow_Correct | Hannウィンドウの正確性 | 中 |
| IS-010 | OverlapAdd_Correct | オーバーラップ加算の正確性 | 高 |

**テストデータ**:
```csharp
// IS-001: Constructor_ValidParams
n_fft = 1024, hop_length = 256
Expected: 正常初期化

// IS-005: Process_OutputShape
Input: magnitude[1, 513, 100], phase_cos[1, 513, 100], phase_sin[1, 513, 100]
Expected output length: (100 - 1) * 256 + 1024 = 26368 samples
```

---

### 2.5 TextEncoder

テキストエンコーダーONNX推論のテスト。

| ID | テスト名 | 説明 | 優先度 |
|----|---------|------|--------|
| TE-001 | LoadModel_Success | モデル正常読み込み | 高 |
| TE-002 | LoadModel_FileNotFound | モデルファイル不在時のエラー | 高 |
| TE-003 | Execute_ValidInput | 有効な入力での推論 | 高 |
| TE-004 | Execute_InputShape | 入力テンソル形状確認 | 高 |
| TE-005 | Execute_OutputShape | 出力テンソル形状確認 | 高 |
| TE-006 | Execute_OutputType | 出力テンソル型確認（float） | 中 |
| TE-007 | Execute_BatchSize1 | バッチサイズ1での推論 | 高 |
| TE-008 | Execute_VariableSeqLen | 可変シーケンス長での推論 | 高 |
| TE-009 | Dispose_Cleanup | リソース解放確認 | 中 |

**テストデータ**:
```csharp
// TE-004: Execute_InputShape
tokens: [1, T] (INT64)
prompt_tokens: [1, T] (INT64)
prompt_features_len: scalar (INT64)
speed: scalar (FLOAT)

// TE-005: Execute_OutputShape
Expected: text_condition [1, T, 512] (FLOAT)
```

---

### 2.6 FMDecoder

Flow Matchingデコーダー推論のテスト。

| ID | テスト名 | 説明 | 優先度 |
|----|---------|------|--------|
| FM-001 | LoadModel_Success | モデル正常読み込み | 高 |
| FM-002 | Execute_SingleStep | 単一ステップ推論 | 高 |
| FM-003 | Execute_InputShape | 入力テンソル形状確認 | 高 |
| FM-004 | Execute_OutputShape | 出力テンソル形状確認 | 高 |
| FM-005 | Execute_GuidanceScale0 | guidance_scale=0での推論 | 高 |
| FM-006 | Execute_GuidanceScale1 | guidance_scale=1での推論 | 高 |
| FM-007 | Execute_WithEulerSolver | EulerSolverとの統合テスト | 高 |
| FM-008 | Execute_FullIntegration | 全ステップ積分テスト | 高 |
| FM-009 | Dispose_Cleanup | リソース解放確認 | 中 |

**テストデータ**:
```csharp
// FM-003: Execute_InputShape
t: scalar (FLOAT)
x: [1, T, 100] (FLOAT)
text_condition: [1, T, 100] (FLOAT)
speech_condition: [1, T, 100] (FLOAT)
guidance_scale: scalar (FLOAT)

// FM-004: Execute_OutputShape
Expected: v [1, T, 100] (FLOAT)
```

---

### 2.7 Vocos

Vocoderの推論テスト。

| ID | テスト名 | 説明 | 優先度 |
|----|---------|------|--------|
| VO-001 | LoadModel_Success | モデル正常読み込み | 高 |
| VO-002 | Execute_ValidInput | 有効な入力での推論 | 高 |
| VO-003 | Execute_InputShape | 入力テンソル形状確認 | 高 |
| VO-004 | Execute_OutputShape_Magnitude | magnitude出力形状確認 | 高 |
| VO-005 | Execute_OutputShape_PhaseCos | phase_cos出力形状確認 | 高 |
| VO-006 | Execute_OutputShape_PhaseSin | phase_sin出力形状確認 | 高 |
| VO-007 | Execute_OutputRange | 出力値の範囲確認 | 中 |
| VO-008 | Dispose_Cleanup | リソース解放確認 | 中 |

**テストデータ**:
```csharp
// VO-003: Execute_InputShape
mel_spectrogram: [1, 100, T] (FLOAT)

// VO-004〜006: Execute_OutputShape
magnitude: [1, 513, T] (FLOAT)
phase_cos: [1, 513, T] (FLOAT)
phase_sin: [1, 513, T] (FLOAT)
```

---

### 2.8 FeatureExtractor

音声特徴抽出のテスト。

| ID | テスト名 | 説明 | 優先度 |
|----|---------|------|--------|
| FE-001 | ExtractMel_ValidAudio | 有効な音声からのメル特徴抽出 | 高 |
| FE-002 | ExtractMel_OutputShape | 出力形状確認 [T, 100] | 高 |
| FE-003 | ExtractMel_SampleRate24k | 24kHzサンプルレート処理 | 高 |
| FE-004 | ExtractMel_Resample | リサンプリング処理 | 中 |
| FE-005 | RmsNormalize_ValidInput | RMS正規化処理 | 高 |
| FE-006 | RmsNormalize_TargetRms | ターゲットRMS値への正規化 | 中 |
| FE-007 | ExtractMel_SilentAudio | 無音音声の処理 | 中 |

**テストデータ**:
```csharp
// FE-002: ExtractMel_OutputShape
Input: audio[24000] (1秒, 24kHz)
Expected: mel[94, 100] (hop_length=256で約94フレーム)
```

---

### 2.9 ZipVoiceManager (E2E)

統合テスト・E2Eテスト。

| ID | テスト名 | 説明 | 優先度 | モード |
|----|---------|------|--------|--------|
| ZM-001 | Initialize_Success | 正常初期化 | 高 | Edit |
| ZM-002 | Initialize_MissingModel | モデル不在時のエラー | 高 | Edit |
| ZM-003 | Synthesize_SimpleText | 単純テキストの合成 | 高 | Play |
| ZM-004 | Synthesize_OutputFormat | 出力AudioClip形式確認 | 高 | Play |
| ZM-005 | Synthesize_OutputSampleRate | 出力サンプルレート確認(24kHz) | 高 | Play |
| ZM-006 | Synthesize_WithPrompt | プロンプト音声付き合成 | 高 | Play |
| ZM-007 | Synthesize_LongText | 長文テキストの合成 | 中 | Play |
| ZM-008 | Synthesize_Punctuation | 句読点付きテキストの合成 | 中 | Play |
| ZM-009 | Synthesize_Cancel | 合成キャンセル処理 | 中 | Play |
| ZM-010 | Dispose_Cleanup | 全リソース解放確認 | 高 | Edit |

---

## 3. 統合テスト項目

コンポーネント間の連携テスト。

| ID | テスト名 | 対象コンポーネント | 優先度 |
|----|---------|------------------|--------|
| IT-001 | Tokenizer_To_TextEncoder | EspeakTokenizer → TextEncoder | 高 |
| IT-002 | TextEncoder_To_FMDecoder | TextEncoder → FMDecoder | 高 |
| IT-003 | FMDecoder_To_Vocos | FMDecoder → Vocos | 高 |
| IT-004 | Vocos_To_ISTFT | Vocos → ISTFTProcessor | 高 |
| IT-005 | FullPipeline_NoPrompt | 全パイプライン（プロンプトなし） | 高 |
| IT-006 | FullPipeline_WithPrompt | 全パイプライン（プロンプトあり） | 高 |

---

## 4. テストユーティリティ

### テストヘルパークラス

```csharp
public static class TestUtility
{
    // テスト用tokens.txt生成
    public static string CreateTestTokensFile();

    // テスト用音声データ生成
    public static float[] GenerateSineWave(float frequency, int sampleRate, float duration);

    // テンソル比較
    public static bool TensorsEqual(float[] a, float[] b, float tolerance = 1e-5f);

    // ランダムテンソル生成
    public static float[] GenerateRandomTensor(int[] shape);
}
```

### モックオブジェクト

```csharp
// espeak-ng非依存のテスト用
public class MockTokenizer : ITokenizer
{
    public int[] Tokenize(string text) => ...;
}

// ONNXモデル非依存のテスト用
public class MockTextEncoder
{
    public float[] Execute(int[] tokens) => ...;
}
```

---

## 5. テストカバレッジ目標

| カテゴリ | 目標カバレッジ |
|---------|--------------|
| TokenMap | 90%+ |
| EulerSolver | 95%+ |
| ISTFTProcessor | 85%+ |
| EspeakTokenizer | 80%+ |
| TextEncoder | 80%+ |
| FMDecoder | 80%+ |
| Vocos | 80%+ |
| ZipVoiceManager | 75%+ |

---

## 6. テスト実行環境

### 必要条件

- Unity 6000.0.50f1以上
- Unity AI Inference Engine 2.3
- espeak-ng-data (StreamingAssets)
- ONNXモデルファイル

### Assembly Definition

**uZipVoice.Tests.Editor.asmdef**
```json
{
    "name": "uZipVoice.Tests.Editor",
    "rootNamespace": "uZipVoice.Tests",
    "references": [
        "uZipVoice.Runtime",
        "Unity.InferenceEngine",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "optionalUnityReferences": ["TestAssemblies"]
}
```

---

## 7. テスト命名規則

```
[MethodName]_[Scenario]_[ExpectedResult]
```

例:
- `GetTimesteps_NumSteps8_Returns9Elements`
- `Tokenize_EmptyString_ReturnsEmptyArray`
- `Execute_ValidInput_ReturnsCorrectShape`

---

## 8. 参考リンク

- [Unity Test Framework](https://docs.unity3d.com/6000.2/Documentation/Manual/test-framework/test-framework-introduction.html)
- [NUnit Documentation](https://docs.nunit.org/)
- [Unity Testing Best Practices](https://unity.com/how-to/automated-tests-unity-test-framework)
