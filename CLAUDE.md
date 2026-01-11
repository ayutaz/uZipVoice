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

Vocosの出力をオーディオ波形に変換。

- **n_fft**: 1024
- **hop_length**: 256
- **window**: hann

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
- **未サポート演算子**: `Log1p`, `FFT`, `IFFT`, `RFFT`, `IRFFT`

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
  ↓ ISTFT (C#実装)
波形 (24kHz)
```

## Unity Sentis参考

- [Unity Sentis Documentation](https://docs.unity3d.com/Packages/com.unity.sentis@2.1/manual/index.html)
- [Supported ONNX Operators](https://docs.unity3d.com/Packages/com.unity.sentis@2.1/manual/supported-operators.html)
