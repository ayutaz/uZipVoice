# ONNXエクスポート手順

## 1. 概要

ZipVoiceモデルをUnity AI Inference Engine（旧Sentis）で使用するためのONNXエクスポート手順です。

### 事前準備されたモデル

Hugging Faceから事前エクスポート済みのモデルをダウンロードできます：

**https://huggingface.co/ayousanz/uZipVoice-onnx**

---

## 2. Unity AI Inference Engineの制約

### 未サポート演算子

以下の演算子はUnity AI Inference Engineでサポートされていません：

| 演算子 | 説明 | 影響するモデル |
|--------|------|---------------|
| FFT | 高速フーリエ変換 | Vocos |
| IFFT | 逆FFT | Vocos |
| RFFT | 実数FFT | Vocos |
| IRFFT | 逆実数FFT | Vocos |
| Log1p | log(1+x) | - |
| ComplexAbs | 複素数絶対値 | Vocos |

### 対応方法

#### Vocosモデル

VocosモデルにはISTFTが含まれていますが、Unity AI Inference Engineではサポートされていません。

**解決策**: ISTFTをONNXモデルから分離し、Unity側（C#）で実装

```
[標準Vocos]
mel → backbone → ISTFT → waveform

[Unity向けVocos]
mel → backbone → magnitude, phase_cos, phase_sin
                       ↓
              [Unity C# ISTFT] → waveform
```

---

## 3. エクスポート手順（Python）

### 環境準備

```bash
cd /path/to/ZipVoice
uv sync  # または pip install -e .
```

### TextEncoderのエクスポート

```python
import torch
from zipvoice.models import ZipVoice

model = ZipVoice.from_pretrained("k2-fsa/ZipVoice")
model.eval()

# ダミー入力
tokens = torch.randint(0, 100, (1, 20), dtype=torch.long)
prompt_tokens = torch.randint(0, 100, (1, 50), dtype=torch.long)
prompt_features_len = torch.tensor(100, dtype=torch.long)
speed = torch.tensor(1.0, dtype=torch.float32)

# エクスポート
torch.onnx.export(
    model.text_encoder,
    (tokens, prompt_tokens, prompt_features_len, speed),
    "text_encoder.onnx",
    input_names=["tokens", "prompt_tokens", "prompt_features_len", "speed"],
    output_names=["text_condition"],
    dynamic_axes={
        "tokens": {0: "batch", 1: "seq_len"},
        "prompt_tokens": {0: "batch", 1: "prompt_seq_len"},
        "text_condition": {0: "batch", 1: "time"}
    },
    opset_version=15
)
```

### FMDecoderのエクスポート

```python
# ダミー入力
t = torch.tensor(0.5, dtype=torch.float32)
x = torch.randn(1, 100, 100)
text_condition = torch.randn(1, 100, 512)
speech_condition = torch.randn(1, 100, 100)
guidance_scale = torch.tensor(1.0, dtype=torch.float32)

torch.onnx.export(
    model.fm_decoder,
    (t, x, text_condition, speech_condition, guidance_scale),
    "fm_decoder.onnx",
    input_names=["t", "x", "text_condition", "speech_condition", "guidance_scale"],
    output_names=["velocity"],
    dynamic_axes={
        "x": {0: "batch", 1: "time"},
        "text_condition": {0: "batch", 1: "time"},
        "speech_condition": {0: "batch", 1: "time"},
        "velocity": {0: "batch", 1: "time"}
    },
    opset_version=15
)
```

### Vocosのエクスポート（ISTFT分離版）

Vocosは標準のエクスポートではISTFTが含まれるため、カスタムラッパーが必要です。

```python
class VocosWithoutISTFT(torch.nn.Module):
    def __init__(self, vocos):
        super().__init__()
        self.backbone = vocos.backbone
        self.head = vocos.head

    def forward(self, mel):
        # mel: [batch, n_mels, time]
        x = self.backbone(mel)

        # head出力: magnitude, phase
        mag, phase = self.head(x)

        # phase をcos, sinに分解
        phase_cos = torch.cos(phase)
        phase_sin = torch.sin(phase)

        return mag, phase_cos, phase_sin

vocos_wrapper = VocosWithoutISTFT(vocos_model)
vocos_wrapper.eval()

mel = torch.randn(1, 100, 100)  # [batch, n_mels, time]

torch.onnx.export(
    vocos_wrapper,
    (mel,),
    "vocos_opset15.onnx",
    input_names=["mel_spectrogram"],
    output_names=["magnitude", "phase_cos", "phase_sin"],
    dynamic_axes={
        "mel_spectrogram": {0: "batch", 2: "time"},
        "magnitude": {0: "batch", 2: "time"},
        "phase_cos": {0: "batch", 2: "time"},
        "phase_sin": {0: "batch", 2: "time"}
    },
    opset_version=15
)
```

---

## 4. エクスポート済みモデルの検証

### ONNX形状確認

```python
import onnx

model = onnx.load("text_encoder.onnx")
print(onnx.helper.printable_graph(model.graph))
```

### onnxruntime検証

```python
import onnxruntime as ort
import numpy as np

session = ort.InferenceSession("text_encoder.onnx")

# 入力確認
for inp in session.get_inputs():
    print(f"{inp.name}: {inp.shape} ({inp.type})")

# 出力確認
for out in session.get_outputs():
    print(f"{out.name}: {out.shape} ({out.type})")
```

---

## 5. モデルファイル仕様

### text_encoder.onnx

| 入力 | 形状 | 型 |
|------|------|-----|
| tokens | [N, T] | INT64 |
| prompt_tokens | [N, T] | INT64 |
| prompt_features_len | scalar | INT64 |
| speed | scalar | FLOAT |

| 出力 | 形状 | 型 |
|------|------|-----|
| text_condition | [N, T, 512] | FLOAT |

### fm_decoder.onnx

| 入力 | 形状 | 型 |
|------|------|-----|
| t | scalar | FLOAT |
| x | [N, T, 100] | FLOAT |
| text_condition | [N, T, 512] | FLOAT |
| speech_condition | [N, T, 100] | FLOAT |
| guidance_scale | scalar | FLOAT |

| 出力 | 形状 | 型 |
|------|------|-----|
| velocity | [N, T, 100] | FLOAT |

### vocos_opset15.onnx

| 入力 | 形状 | 型 |
|------|------|-----|
| mel_spectrogram | [N, 100, T] | FLOAT |

| 出力 | 形状 | 型 |
|------|------|-----|
| magnitude | [N, 513, T] | FLOAT |
| phase_cos | [N, 513, T] | FLOAT |
| phase_sin | [N, 513, T] | FLOAT |

---

## 6. Unityへの配置

### ファイル配置

```
Assets/uZipVoice/Models/
├── text_encoder.onnx
├── fm_decoder.onnx
└── vocos_opset15.onnx
```

### インポート設定

1. Unityでプロジェクトを開く
2. ONNXファイルを`Assets/uZipVoice/Models/`にコピー
3. インポート完了を待つ（自動的に.onnxassetが生成される）

### ZipVoiceManagerへの設定

1. シーンの`ZipVoiceManager`オブジェクトを選択
2. Inspectorで以下を設定:
   - `Text Encoder Model`: text_encoder.onnx
   - `FM Decoder Model`: fm_decoder.onnx
   - `Vocos Model`: vocos_opset15.onnx
   - `Tokens Text Asset`: tokens.txt

---

## 7. トラブルシューティング

### エラー: 未サポート演算子

```
Error: Operator 'FFT' is not supported
```

**原因**: VocosにISTFTが含まれている
**解決**: ISTFT分離版のVocosを使用

### エラー: 形状不一致

```
Error: Shape mismatch
```

**原因**: 動的形状の扱いが不正
**解決**: `dynamic_axes`でバッチとシーケンス長を指定

### エラー: Opsetバージョン

```
Error: Unsupported opset version
```

**原因**: Opset 16以上を使用
**解決**: `opset_version=15`を指定

---

## 8. 参考リンク

- [Unity AI Inference Engine - Supported Operators](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.4/manual/supported-operators.html)
- [ONNX Opset Versions](https://github.com/onnx/onnx/blob/main/docs/Versioning.md)
- [PyTorch ONNX Export](https://pytorch.org/docs/stable/onnx.html)
