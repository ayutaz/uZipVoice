# 技術調査結果

## 1. ZipVoice概要

ZipVoiceは、Flow Matchingベースの高速ゼロショットTTSモデルです。

### モデル構成

| モデル | サイズ | Opset | 役割 |
|--------|--------|-------|------|
| text_encoder.onnx | 17MB | 15 | テキスト→条件ベクトル |
| fm_decoder.onnx | 456MB | 15 | Flow Matchingデコーダ |
| vocos_opset15.onnx | 52MB | 15 | Vocoder（メル→STFT係数） |

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
  - `text_condition` [N, T, 100] FLOAT
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

## 2. Unity AI Inference Engine 2.3

### 概要
- パッケージ: `com.unity.ai.inference@2.3`
- 旧名称: Unity Sentis
- 用途: ONNXモデルの推論実行

### サポート範囲
- ONNX Opset: 7-15（ZipVoiceモデルはOpset 15で互換）
- プラットフォーム: 全Unityサポートプラットフォーム
- バックエンド: CPU, GPUCompute, GPUPixel

### 制約事項
- FFT/IFFT演算子は未サポート
- ISTFTは外部ライブラリで実装が必要

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

### Misaki vs piper_phonemize 比較

| テキスト | piper_phonemize | Misaki |
|---------|-----------------|--------|
| Hello world. | `həlˈoʊ wˈɜːld.` | `həlˈO wˈɜɹld.` |
| This is a test. | `ðɪs ɪz ɐ tˈɛst.` | `ðˌɪs ɪz ɐ tˈɛst.` |

**結論**: Misakiは`O`, `W`等の独自記号を使用し、ZipVoiceのtokens.txtと非互換。espeak-ngが最適。

### espeak-ng Unity実装（piper-unity参照）

[skykim/piper-unity](https://github.com/skykim/piper-unity)の実装:

```csharp
// ESpeakNG.cs - P/Invokeラッパー
[DllImport("espeak-ng")]
public static extern int espeak_Initialize(int output, int buflength, string path, int options);

[DllImport("espeak-ng")]
public static extern IntPtr espeak_TextToPhonemes(ref IntPtr text, int textmode, int phonememode);
```

対応プラットフォーム:
- Windows: `libespeak-ng.dll`
- macOS: `libespeak-ng.1.dylib`
- Android: `libespeak-ng.so`

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

### NWaves

- バージョン: 0.9.6
- GitHub: https://github.com/ar1st0crat/NWaves
- NuGet: `Install-Package NWaves`

```csharp
using NWaves.Transforms;

var stft = new Stft(nFft: 1024, hopSize: 256, window: WindowTypes.Hann);
var reconstructed = stft.Inverse(timefreq);
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

## 参考リンク

- [ZipVoice GitHub](https://github.com/k2-fsa/ZipVoice)
- [Unity AI Inference Engine 2.3](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.3/manual/)
- [skykim/piper-unity](https://github.com/skykim/piper-unity)
- [NWaves](https://github.com/ar1st0crat/NWaves)
- [espeak-ng](https://github.com/espeak-ng/espeak-ng)
