# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

uZipVoiceは、ZipVoice（Flow Matchingベースの高速TTS）をUnity Sentisで動作させるプロジェクトです。

- **元プロジェクト**: `C:\Users\yuta\Desktop\Private\ZipVoice`（Python、ONNX生成）
- **本プロジェクト**: Unity 6 (6000.0.58f2) でSentis推論を実装

### ZipVoice概要
- 123Mパラメータの軽量ゼロショットTTS
- Flow Matchingによる高速生成（5-16ステップ）
- サンプリングレート: 24kHz
- 特徴量: Vocos fbank（100次元）

## 実装すべきコンポーネント

### 1. ONNXモデル読み込み

ZipVoiceから以下のONNXファイルをエクスポートしてAssetsに配置:

| ファイル | 役割 | 入出力 |
|----------|------|--------|
| `text_encoder.onnx` | テキスト→条件ベクトル | tokens → text_condition |
| `fm_decoder.onnx` | Flow Matchingデコーダ | (t, x, text_cond, speech_cond) → velocity |
| `vocos.onnx` | Vocoder（メル→波形） | mel_features → waveform |

### 2. EulerSolver（ODE積分）

Flow Matchingの推論に必要。Sentisで実装する必要あり。

```csharp
// 疑似コード
for (int i = 0; i < numSteps; i++) {
    float t = 1.0f - (float)i / numSteps;
    var velocity = fmDecoder.Execute(t, x, textCond, speechCond);
    x = x + dt * velocity;
}
```

- **推奨ステップ数**: 8-16（品質重視）、4-8（速度重視）
- **CFG (Classifier-Free Guidance)**: `v = (1+scale)*v_cond - scale*v_uncond`

### 3. ISTFT（逆短時間フーリエ変換）

Vocosの出力をオーディオ波形に変換。NWavesライブラリを使用。

- **ライブラリ**: NWaves 0.9.6 (`Assets/uZipVoice/Plugins/NWaves.dll`)
- **n_fft**: 1024
- **hop_length**: 256
- **window**: Hann

```csharp
// NWaves STFT逆変換
var stft = new Stft(nFft: 1024, hopSize: 256, WindowType.Hann);
float[] waveform = stft.Inverse(spectrogram);
```

### 4. Tokenizer

テキストをトークンIDに変換。Python側の実装を参考に移植:
- `ZipVoice/zipvoice/tokenizer/tokenizer.py`

## ONNXエクスポート手順

ZipVoiceプロジェクトで以下を実行:

```bash
# Sentis用ONNXエクスポート
cd C:\Users\yuta\Desktop\Private\ZipVoice
uv run python -m zipvoice.bin.onnx_export_sentis \
    --model-name zipvoice \
    --model-dir exp/zipvoice_moe_90h \
    --onnx-model-dir exp/zipvoice_sentis

# Vocos Vocoderダウンロード
uv run python scripts/download_vocos_onnx.py

# Sentis互換性検証
uv run python -m zipvoice.bin.verify_sentis_onnx \
    --onnx-dir exp/zipvoice_sentis
```

## Sentis制約事項

- **Opset version**: 7-15（15推奨）
- **テンソル次元**: 最大8次元
- **未サポート演算子**:
  - `If` - 条件分岐（静的グラフに変換が必要）
  - `Log1p` - `log(1+x)`で代替
  - `FFT`, `IFFT`, `RFFT`, `IRFFT` - 信号処理系

### If演算子の回避

ZipVoiceの`CompactRelPositionalEncoding`クラスでは、位置エンコーディングの動的拡張に条件分岐を使用していました。これはONNXの`If`ノードに変換され、Sentisでエラーになります。

**解決策**: `torch.jit.is_tracing()`を使用して、ONNX export時は事前計算済みの位置エンコーディングを使用するように修正しました。

```python
# zipformer.py - CompactRelPositionalEncoding.forward()
if torch.jit.is_scripting() or torch.jit.is_tracing():
    pe = self.pe.to(dtype=x.dtype, device=x.device)  # 事前計算済みを使用
else:
    self.extend_pe(x, left_context_len)  # 動的拡張
    pe = self.pe
```

## モデルパラメータ（参考）

```
Text Encoder:
- 層数: 4
- 隠し次元: 192
- アテンションヘッド: 4

FM Decoder:
- ダウンサンプリング: [1,2,4,2,1]
- 各ブロック層数: [2,2,4,4,4]
- 隠し次元: 512
- アテンションヘッド: 4

Audio:
- サンプリングレート: 24000Hz
- メル次元: 100
- n_fft: 1024
- hop_length: 256
```

## 推論パイプライン

```
テキスト
  ↓ Tokenizer
トークンID列
  ↓ Text Encoder (ONNX)
条件ベクトル
  ↓ FM Decoder (ONNX) × numSteps (Euler法)
メル特徴量 (100次元)
  ↓ Vocos (ONNX)
STFT係数
  ↓ ISTFT (NWaves)
波形 (24kHz)
```

## 実装状況

### 完了
- ✅ TextEncoder - テキストトークン→条件ベクトル変換
- ✅ FMDecoder - Flow Matching ODE積分（非同期対応・最適化済み）
- ✅ Vocos - メル特徴量→STFT係数変換
- ✅ ISTFTProcessor - NWavesライブラリによるSTFT→波形変換
- ✅ EspeakTokenizer - テキスト→トークン変換
- ✅ ZipVoiceManager - 統合API
- ✅ UniTask対応 - UIフリーズ防止
- ✅ NWaves導入 - 高精度ISTFT実装
- ✅ パフォーマンス最適化 - バッファ再利用、Yield頻度削減

### テンソル形状の重要な注意点

ONNXモデルの入力形状には注意が必要です:

| 入力 | 正しい形状 | 誤った形状 |
|------|-----------|-----------|
| `prompt_features_len` | `TensorShape()` (rank 0, scalar) | `TensorShape(1)` (rank 1) |
| `speed` | `TensorShape()` (rank 0, scalar) | `TensorShape(1)` (rank 1) |
| `t` | `TensorShape()` (rank 0, scalar) | `TensorShape(1)` (rank 1) |
| `guidance_scale` | `TensorShape()` (rank 0, scalar) | `TensorShape(1)` (rank 1) |

スカラー値は `new Tensor<float>(new TensorShape(), new float[] { value })` で作成します。

### 非同期処理

UniTaskを使用してUIフリーズを防止しています:

```csharp
// FMDecoderのEulerループ内で4ステップごとにyield（高速化のため頻度削減）
if (step % 4 == 0)
{
    await UniTask.Yield();
}

// 進捗コールバック
onProgress?.Invoke((float)(step + 1) / solver.NumSteps);
```

### パフォーマンス最適化

FMDecoderには以下の最適化が実装されています:

1. **バッファ再利用**: `_xBuffer`をEulerステップ間で再利用し、メモリアロケーションを削減
2. **Yield頻度削減**: `UniTask.Yield()`を毎ステップから4ステップごとに変更
3. **TensorShapeキャッシュ**: Euler積分ループでTensorShapeを再利用

```csharp
// バッファ再利用の例
if (_xBuffer == null || _xBuffer.Length != totalSize)
{
    _xBuffer = new float[totalSize];
}

// CPU上でEulerステップを実行してバッファに格納
for (int i = 0; i < totalSize; i++)
{
    _xBuffer[i] = xData[i] + dt * vData[i];
}

// バッファからテンソルを作成（1回のアップロード）
x = new Tensor<float>(shape, _xBuffer);
```

## Unity Sentis参考

- [Unity Sentis Documentation](https://docs.unity3d.com/Packages/com.unity.sentis@2.1/manual/index.html)
- [Supported ONNX Operators](https://docs.unity3d.com/Packages/com.unity.sentis@2.1/manual/supported-operators.html)
