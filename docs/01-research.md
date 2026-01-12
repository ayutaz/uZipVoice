# 技術調査結果

## 1. ZipVoice概要

ZipVoiceは、Flow Matchingベースの高速ゼロショットTTSモデルです。

- **リポジトリ**: https://github.com/k2-fsa/ZipVoice
- **ONNXモデル**: https://huggingface.co/ayousanz/uZipVoice-onnx

### モデル構成

| モデル | サイズ | Opset | 役割 |
|--------|--------|-------|------|
| text_encoder.onnx | ~3MB | 15 | テキスト→条件ベクトル |
| fm_decoder.onnx | ~200MB | 15 | Flow Matchingデコーダ |
| vocos_opset15.onnx | ~50MB | 15 | Vocoder（メル→STFT係数） |

### 推論パイプライン

```
テキスト
  ↓ Tokenizer (espeak-ng G2P)
トークンID [1, seq_len]
  ↓ text_encoder.onnx
text_condition [1, num_frames, 512]
  ↓ Euler ODE積分 (8-16ステップ)
mel_features [1, num_frames, 100]
  ↓ vocos.onnx
magnitude, phase_cos, phase_sin [1, 513, T]
  ↓ ISTFT
waveform [24kHz]
```

### モデル入出力

#### text_encoder.onnx
- 入力:
  - `tokens` [N, T] INT64
  - `prompt_tokens` [N, T] INT64
  - `prompt_features_len` スカラー INT64
  - `speed` スカラー FLOAT
- 出力:
  - `text_condition` [N, T, 512] FLOAT

#### fm_decoder.onnx
- 入力:
  - `t` スカラー FLOAT（時刻）
  - `x` [N, T, 100] FLOAT（現在の状態）
  - `text_condition` [N, T, 512] FLOAT
  - `speech_condition` [N, T, 100] FLOAT
  - `guidance_scale` スカラー FLOAT
- 出力:
  - `v` [N, T, 100] FLOAT（速度ベクトル）

#### vocos_opset15.onnx
- 入力:
  - `mel_spectrogram` [batch, 100, time] FLOAT
- 出力:
  - `magnitude` [batch, 513, time] FLOAT
  - `phase_cos` [batch, 513, time] FLOAT
  - `phase_sin` [batch, 513, time] FLOAT

---

## 2. Unity AI Inference Engine

### 概要
- パッケージ: `com.unity.ai.inference@2.4.1`
- 旧名称: Unity Sentis
- 用途: ONNXモデルの推論実行

### サポート範囲
- ONNX Opset: 7-15（ZipVoiceモデルはOpset 15で互換）
- プラットフォーム: 全Unityサポートプラットフォーム
- バックエンド: CPU, GPUCompute

### 制約事項（重要）

Unity AI Inference Engineには以下の制約があります：

#### 未サポート演算子
| 演算子 | 代替手段 |
|--------|---------|
| FFT/IFFT | C#で実装（NWavesライブラリ使用） |
| RFFT/IRFFT | C#で実装 |
| Log1p | `Log(x + 1)` で代替 |
| ComplexAbs | 実部・虚部から計算 |

#### テンソル制約
- 最大次元数: 8次元
- 動的形状: 一部制限あり（バッチ次元は固定推奨）

#### エクスポート時の注意点
- Opset 15を使用
- `torch.fft` 系関数はONNX内で使用不可（Python側で前処理）
- 複素数演算は実部・虚部に分離

### 基本API

```csharp
using Unity.InferenceEngine;

// モデル読み込み
var model = ModelLoader.Load(modelAsset);
var engine = new Worker(model, BackendType.GPUCompute);

// テンソル作成・推論
using var inputTensor = new Tensor<float>(new TensorShape(1, 100, 256));
engine.SetInput("input_name", inputTensor);
engine.Schedule();

// 出力取得
using var output = engine.PeekOutput() as Tensor<float>;
float[] data = output.DownloadToArray();
```

---

## 3. Tokenizer (G2P) 調査

### ZipVoiceの音素形式

ZipVoiceは`piper_phonemize`（espeak-ngベース）のIPA音素を使用。

```
Text: Hello world
IPA:  həlˈoʊ wˈɜːld
List: ['h', 'ə', 'l', 'ˈ', 'o', 'ʊ', ' ', 'w', 'ˈ', 'ɜ', 'ː', 'l', 'd']
```

### G2P選択肢の比較

| 方式 | 互換性 | Unity完結 | 実装難易度 |
|------|--------|----------|-----------|
| espeak-ng DLL | ◎ 完全互換 | △ ネイティブDLL | 低 |
| Misaki (辞書ベース) | △ 要変換 | ◯ 純C# | 中 |
| OpenPhonemizer ONNX | ◎ espeak互換 | ◯ | 高 |
| CMU辞書 + ルール | △ 要変換 | ◯ 純C# | 中 |

### espeak-ng Unity実装

piper-phonemizeと同等の出力を得るため、espeak-ngのネイティブDLLを使用。

```csharp
// EspeakNative.cs - P/Invokeラッパー
[DllImport("libespeak-ng")]
public static extern int espeak_Initialize(int output, int buflength, string path, int options);

[DllImport("libespeak-ng")]
public static extern IntPtr espeak_TextToPhonemes(ref IntPtr text, int textmode, int phonememode);
```

対応プラットフォーム:
- Windows: `libespeak-ng.dll`
- macOS: `libespeak-ng.1.dylib`
- Android: `libespeak-ng.so`

### piper_phonemizeとの差異

| 項目 | piper_phonemize | espeak_TextToPhonemes |
|------|-----------------|----------------------|
| 句読点 | 保持 | 出力しない |
| ストレス記号 | 含む | 含む |
| 対応 | 末尾句読点を手動追加 | Unity側で実装済み |

---

## 4. ISTFT調査

### Unity標準機能
- `AudioSource.GetSpectrumData`: FFTのみ（逆変換なし）
- ISTFT/逆FFT: **標準機能なし**

### 外部ライブラリ

| ライブラリ | ISTFT | ライセンス | Unity互換 |
|-----------|-------|-----------|----------|
| NWaves | ◯ | MIT | ◯ .NET Standard |
| FftSharp | △ 要実装 | MIT | ◯ |
| DSPLib | △ 要実装 | - | ◯ |

### NWaves（採用）

- バージョン: 0.9.6
- GitHub: https://github.com/ar1st0crat/NWaves
- 用途: FFT処理のみ使用（ISTFTはカスタム実装）

**使用例**:
```csharp
using NWaves.Transforms;

var fft = new Fft(nFft);
fft.Direct(real, imag);  // FFT
fft.Inverse(real, imag); // IFFT
```

---

## 5. Euler Solver調査

### ZipVoiceの実装（Python）

```python
# タイムステップ生成
def get_time_steps(t_start=0.0, t_end=1.0, num_step=10, t_shift=0.5):
    timesteps = torch.linspace(t_start, t_end, num_step+1)
    timesteps = t_shift * timesteps / (1 + (t_shift-1) * timesteps)
    return timesteps

# Euler積分
for step in range(num_step):
    v = fm_decoder(t=timesteps[step], x=x, text_cond, speech_cond, guidance_scale)
    x = x + v * (timesteps[step+1] - timesteps[step])
```

### C#移植

```csharp
// タイムステップ生成
float t_shift = 0.5f;
float[] timesteps = new float[numStep + 1];
for (int i = 0; i <= numStep; i++) {
    float t = (float)i / numStep;
    timesteps[i] = t_shift * t / (1f + (t_shift - 1f) * t);
}

// Euler積分
for (int step = 0; step < numStep; step++) {
    float dt = timesteps[step + 1] - timesteps[step];
    var v = fmDecoder.Execute(timesteps[step], x, textCond, speechCond, guidanceScale);
    x = x + v * dt;
}
```

---

## 6. FeatureExtractor調査

### メルスペクトログラム抽出

Vocos/ZipVoiceのfbank設定:
- `power=1`（magnitude spectrum、power spectrumではない）
- `center=True`（reflect padding適用）

```csharp
// center=Trueに相当: 信号の両端にn_fft/2のパディング
int padLength = nFft / 2;
// reflect padding実装

// マグニチュードスペクトル（power=1）
float magnitude = Mathf.Sqrt(real * real + imag * imag);
```

---

## 参考リンク

- [ZipVoice GitHub](https://github.com/k2-fsa/ZipVoice)
- [uZipVoice ONNX Models](https://huggingface.co/ayousanz/uZipVoice-onnx)
- [Unity AI Inference Engine](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.4/manual/)
- [Supported ONNX Operators](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.4/manual/supported-operators.html)
- [NWaves](https://github.com/ar1st0crat/NWaves)
- [espeak-ng](https://github.com/espeak-ng/espeak-ng)
