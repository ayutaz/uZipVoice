# ZipVoiceをUnityで動かす - 技術解説

## 1. はじめに

### 1.1 本記事の目的

本記事では、Flow Matchingベースの高速音声合成システム「ZipVoice」をUnityで動作させるために行った技術的な取り組みを解説します。事前調査から実装、遭遇した問題の解決策まで、一連の流れを紹介します。

今回作成したライブラリは以下で公開しています。

[https://github.com/ayutaz/uZipVoice:embed:cite]

### 1.2 ZipVoiceについて

ZipVoiceの詳細については、以前の記事をご参照ください。

[https://ayousanz.hatenadiary.jp/entry/2025/12/28/111858:embed:cite]

簡潔にまとめると：
- 123Mパラメータの軽量ゼロショットTTS
- Flow Matchingによる高速生成（4-16ステップ）
- サンプリングレート: 24kHz
- 特徴量: Vocos fbank（100次元）

ZipVoiceには2つのモデルバリエーションがあります：

| モデル | 説明 | 推奨ステップ数 |
|--------|------|---------------|
| ZipVoice | ベースモデル。品質重視 | 8-16 |
| ZipVoice-Distill | 蒸留版。最小限の品質劣化で高速化 | 4-8 |

本記事では**ZipVoice-Distill（蒸留版）**を使用します。リアルタイム音声合成では速度が重要であり、蒸留版はCFG（Classifier-Free Guidance）計算が内蔵されているため、ベースモデルの約2倍高速に推論できます。

### 1.3 なぜUnityで動かすのか

ゲームやVRアプリケーションでリアルタイム音声合成を行うためです。Unity上でTTSを動作させることで、以下のメリットがあります：

- キャラクターの台詞をリアルタイムで生成
- 外部サーバー不要でオフライン動作
- ゼロショットによる任意の声質での合成

### 1.4 完成したもの

- **GitHub**: [https://github.com/ayutaz/uZipVoice]
- **ONNXモデル**: [https://huggingface.co/ayousanz/uZipVoice-onnx]

Unity 6上で動作するTTSシステムを実装しました。テキスト入力からリアルタイムで音声を生成できます。

---

## 2. 事前調査

ZipVoiceをUnityで動作させるにあたり、以下の3つの技術課題を解決する必要がありました。

1. **ONNXモデルの推論**: ZipVoiceの3つのモデル（TextEncoder, FMDecoder, Vocos）をUnity上で実行する方法
2. **テキストの音素変換（G2P）**: Python版で使用しているpiper_phonemizeと互換性のある音素変換
3. **波形生成（ISTFT）**: Vocosの出力（STFT係数）から音声波形を生成する方法

それぞれについて調査を行いました。

### 2.1 Unity AI Inference Engine（旧Sentis）の調査

ZipVoiceの3つのONNXモデルをUnityで実行するため、Unity公式のONNX推論エンジンを調査しました。特に、どの演算子がサポートされているかを把握することが重要です。

Unity AI Inference Engine 2.4.1を使用してONNXモデルを推論します。

**サポート範囲**
- ONNX Opset: 7-15（ZipVoiceモデルはOpset 15で互換）
- プラットフォーム: 全Unityサポートプラットフォーム
- バックエンド: CPU, GPUCompute

**未サポート演算子（重要）**

| 演算子 | 説明 | 代替手段 |
|--------|------|---------|
| FFT/IFFT | 高速フーリエ変換 | C#で実装（NWavesライブラリ使用） |
| RFFT/IRFFT | 実数FFT | C#で実装 |
| If | 条件分岐 | Python側で静的グラフに変換 |
| Log1p | log(1+x) | Log(x+1)で代替 |

特にFFT系の演算子がサポートされていないことは、Vocoderの実装に大きな影響を与えます。

### 2.2 G2P（Grapheme-to-Phoneme）の選択肢

ZipVoiceのTextEncoderは、テキストそのものではなく「音素（phoneme）」を入力として受け取ります。Python版では`piper_phonemize`（espeak-ngベース）を使用してテキストをIPA音素に変換しています。

Unity側でも同じ形式の音素を生成しないと、モデルが正しく動作しません。そのため、Unity上でG2P（文字→音素変換）を実現する方法を調査しました。

**選択肢の比較**

| 方式 | 互換性 | Unity完結 | 実装難易度 |
|------|--------|----------|-----------|
| espeak-ng DLL | 完全互換 | ネイティブDLL必要 | 低 |
| Misaki (辞書ベース) | 要変換 | 純C# | 中 |
| OpenPhonemizer ONNX | espeak互換 | ONNX推論 | 高 |
| CMU辞書 + ルール | 要変換 | 純C# | 中 |

**採用**: espeak-ng DLL

piper_phonemizeと完全互換であり、[piper-unity](https://github.com/Macoron/piper-unity)で実績があるため採用しました。

### 2.3 ISTFT実装の選択肢

ZipVoiceのVocoderであるVocosは、メルスペクトログラムからSTFT係数（magnitude, phase）を出力します。最終的な音声波形を得るには、このSTFT係数に対してISTFT（逆短時間フーリエ変換）を適用する必要があります。

しかし、前述の通りUnity AI Inference EngineはFFT/IFFT演算子をサポートしていません。そのため、ISTFTをUnity側（C#）で実装する必要があり、その方法を調査しました。

**選択肢の比較**

| ライブラリ | ISTFT | ライセンス | Unity互換 |
|-----------|-------|-----------|----------|
| NWaves | あり | MIT | .NET Standard対応 |
| FftSharp | 要実装 | MIT | 対応 |
| DSPLib | 要実装 | - | 対応 |

**採用**: NWaves 0.9.6

FFT/IFFT実装済みで、MITライセンス、依存関係なしのため採用しました。

---

## 3. 技術選定とアーキテクチャ設計

### 3.1 最終構成

| コンポーネント | 選定技術 | バージョン | ライセンス |
|---------------|---------|-----------|-----------|
| 推論エンジン | Unity AI Inference Engine | 2.4.1 | Unity |
| G2P (Tokenizer) | espeak-ng | 1.52 | GPLv3 |
| ISTFT | NWaves + カスタム実装 | 0.9.6 | MIT |
| Euler Solver | C#実装 | - | - |
| 非同期処理 | UniTask | 2.5.10 | MIT |

### 3.2 システムアーキテクチャ図

```
┌─────────────────────────────────────────────────────────────┐
│                        uZipVoice                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────┐    ┌──────────────┐    ┌───────────────┐  │
│  │   Input     │    │  Tokenizer   │    │ Text Encoder  │  │
│  │   Text      │───▶│ (espeak-ng)  │───▶│    (ONNX)     │  │
│  │  (string)   │    │              │    │               │  │
│  └─────────────┘    └──────────────┘    └───────┬───────┘  │
│                                                  │          │
│                                                  ▼          │
│  ┌─────────────┐    ┌──────────────┐    ┌───────────────┐  │
│  │   Prompt    │    │   Feature    │    │  FM Decoder   │  │
│  │   Audio     │───▶│  Extractor   │───▶│    (ONNX)     │  │
│  │   (wav)     │    │              │    │ + Euler Solver│  │
│  └─────────────┘    └──────────────┘    └───────┬───────┘  │
│                                                  │          │
│                                                  ▼          │
│  ┌─────────────┐    ┌──────────────┐    ┌───────────────┐  │
│  │   Output    │    │    ISTFT     │    │    Vocos      │  │
│  │   Audio     │◀───│   (NWaves)   │◀───│    (ONNX)     │  │
│  │ (AudioClip) │    │              │    │               │  │
│  └─────────────┘    └──────────────┘    └───────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 3.3 依存関係とライセンス

| コンポーネント | ライセンス | 商用利用 | 注意点 |
|---------------|-----------|---------|--------|
| Unity AI Inference Engine | Unity | 可 | Unity利用規約に準拠 |
| espeak-ng | GPLv3 | 要確認 | ソース公開義務あり |
| NWaves | MIT | 可 | 著作権表示のみ |
| UniTask | MIT | 可 | 著作権表示のみ |
| ZipVoice モデル | Apache 2.0 | 可 | - |

**注意**: espeak-ngはGPLv3のため、商用利用時はライセンス確認が必要です。

---

## 4. ONNXエクスポート

### 4.1 Unity向けの制約対応

#### If演算子の回避

ZipVoiceの`CompactRelPositionalEncoding`クラスでは、位置エンコーディングの動的拡張に条件分岐を使用しています。これはONNXの`If`ノードに変換され、Unity AI Inference Engineでエラーになります。

**解決策**: `torch.jit.is_tracing()`を使用して、ONNXエクスポート時は事前計算済みの位置エンコーディングを使用するように修正しました。

```python
# zipformer.py - CompactRelPositionalEncoding.forward()
if torch.jit.is_scripting() or torch.jit.is_tracing():
    pe = self.pe.to(dtype=x.dtype, device=x.device)  # 事前計算済みを使用
else:
    self.extend_pe(x, left_context_len)  # 動的拡張
    pe = self.pe
```

#### Vocos ISTFTの分離

VocosにはISTFTが含まれていますが、FFT演算子がUnityでサポートされていません。

**解決策**: ISTFTをONNXモデルから分離し、Unity側（C#）で実装しました。

```
[標準Vocos]
mel → backbone → ISTFT → waveform

[Unity向けVocos]
mel → backbone → magnitude, phase_cos, phase_sin
                       ↓
              [Unity C# ISTFT] → waveform
```

### 4.2 各モデルのエクスポート手順

エクスポートを行う前に、ZipVoice側のコードに修正が必要です。

#### ZipVoice側の修正（zipformer.py）

`CompactRelPositionalEncoding`クラスの`forward`メソッドを修正し、ONNX tracing時は事前計算済みの位置エンコーディングを使用するようにします。

```python
# zipvoice/models/modules/zipformer.py

def forward(self, x: Tensor, left_context_len: int = 0) -> Tensor:
    # When scripting or tracing for ONNX export, we use pre-computed PE
    # and avoid the conditional extension logic.
    if torch.jit.is_scripting() or torch.jit.is_tracing():
        # Use pre-computed PE without dynamic extension
        pe = self.pe.to(dtype=x.dtype, device=x.device)
    else:
        # Normal training/inference path with dynamic extension
        self.extend_pe(x, left_context_len)
        pe = self.pe

    x_size_left = x.size(0) + left_context_len
    pe_len = pe.size(0)
    center = pe_len // 2
    pos_emb = pe[
        center - x_size_left + 1 : center + x.size(0),
        :,
    ]
    pos_emb = pos_emb.unsqueeze(0)
    return self.dropout(pos_emb)
```

#### エクスポートスクリプト（onnx_export_sentis.py）

エクスポート前に位置エンコーディングを事前計算する処理を追加します。

```python
def _precompute_positional_encodings(model: nn.Module, max_len: int) -> None:
    """Pre-compute positional encodings for all encoder_pos modules."""
    dummy_input = torch.zeros(max_len)

    for name, module in model.named_modules():
        if hasattr(module, 'extend_pe') and hasattr(module, 'pe'):
            module.extend_pe(dummy_input)
            if module.pe is not None:
                logging.info(f"  {name}: PE shape = {module.pe.shape}")

# エクスポート前に呼び出し
max_pe_len = 4000  # ~170 seconds at 24kHz with hop_length=256
_precompute_positional_encodings(model, max_pe_len)
```

修正済みのコードは以下で公開しています。

[https://github.com/ayutaz/ZipVoice/blob/feature/distill-onnx-sentis/zipvoice/bin/onnx_export_sentis.py:embed:cite]

#### エクスポートの実行

上記の修正を行った後、以下のコマンドでONNXモデルをエクスポートできます。

```bash
cd ZipVoice
uv run python -m zipvoice.bin.onnx_export_sentis \
    --model-name zipvoice_distill \
    --onnx-model-dir exp/zipvoice_distill_sentis
```

モデルファイルはHuggingFaceから自動的にダウンロードされます。ローカルにモデルがある場合は`--model-dir`オプションで指定できます。

エクスポートが完了すると、以下の3つのONNXファイルが生成されます。

#### 生成されるモデルファイル

**text_encoder.onnx**（約17MB）

テキストトークンを条件ベクトルに変換します。

| 入力 | 形状 | 型 |
|------|------|-----|
| tokens | [N, T] | INT64 |
| prompt_tokens | [N, T] | INT64 |
| prompt_features_len | scalar | INT64 |
| speed | scalar | FLOAT |

| 出力 | 形状 | 型 |
|------|------|-----|
| text_condition | [N, T, 512] | FLOAT |

**fm_decoder.onnx**（約456MB）

Flow Matchingデコーダ。Euler積分の各ステップで速度ベクトルを計算します。

| 入力 | 形状 | 型 |
|------|------|-----|
| t | scalar | FLOAT |
| x | [N, T, 100] | FLOAT |
| text_condition | [N, T, 512] | FLOAT |
| speech_condition | [N, T, 100] | FLOAT |
| guidance_scale | scalar | FLOAT |

| 出力 | 形状 | 型 |
|------|------|-----|
| v | [N, T, 100] | FLOAT |

**vocos_opset15.onnx**（約52MB）

メルスペクトログラムからSTFT係数を生成します。ISTFTはUnity側で実装するため、このモデルはSTFT係数までを出力します。

| 入力 | 形状 | 型 |
|------|------|-----|
| mel_spectrogram | [N, 100, T] | FLOAT |

| 出力 | 形状 | 型 |
|------|------|-----|
| magnitude | [N, 513, T] | FLOAT |
| phase_cos | [N, 513, T] | FLOAT |
| phase_sin | [N, 513, T] | FLOAT |

### 4.3 モデルの検証

エクスポートしたモデルは、onnxruntimeで動作確認を行います。

```python
import onnxruntime as ort

session = ort.InferenceSession("text_encoder.onnx")

# 入力確認
for inp in session.get_inputs():
    print(f"{inp.name}: {inp.shape} ({inp.type})")

# 出力確認
for out in session.get_outputs():
    print(f"{out.name}: {out.shape} ({out.type})")
```

---

## 5. 実装詳細

### 5.1 プロジェクト構成

```
Assets/uZipVoice/
├── Runtime/
│   ├── Audio/
│   │   ├── FeatureExtractor.cs    # メル特徴量抽出
│   │   └── ISTFTProcessor.cs      # 逆STFT
│   ├── Core/
│   │   ├── ZipVoiceConfig.cs      # 設定
│   │   └── ZipVoiceManager.cs     # メインAPI
│   ├── Inference/
│   │   ├── EulerSolver.cs         # ODE積分
│   │   ├── FMDecoder.cs           # Flow Matchingデコーダ
│   │   ├── TextEncoder.cs         # テキストエンコーダ
│   │   └── Vocos.cs               # ボコーダ
│   └── Tokenizer/
│       ├── EspeakNative.cs        # P/Invoke
│       ├── EspeakTokenizer.cs     # トークナイザ
│       └── TokenMap.cs            # トークンマッピング
├── Models/                         # ONNXモデル（525MB）
├── Plugins/
│   ├── NWaves.dll                 # FFTライブラリ
│   └── Windows/x64/
│       └── libespeak-ng.dll       # G2Pエンジン
└── Resources/
    └── tokens.txt                 # トークンマッピング
```

### 5.2 Tokenizer

テキストを音素に変換し、さらにトークンIDに変換するコンポーネントです。

#### なぜ必要か

ZipVoiceのTextEncoderは、テキスト文字列ではなくトークンID列を入力として受け取ります。そのため、テキスト→音素→トークンIDという変換が必要です。

#### 実装

espeak-ngのネイティブDLLをP/Invokeで呼び出します。

```csharp
// EspeakNative.cs - P/Invokeラッパー
[DllImport("libespeak-ng")]
public static extern int espeak_Initialize(int output, int buflength, string path, int options);

[DllImport("libespeak-ng")]
public static extern IntPtr espeak_TextToPhonemes(ref IntPtr text, int textmode, int phonememode);
```

トークン変換の流れ：

1. **espeak-ng初期化**: `espeak_Initialize()`でデータパスを指定して初期化
2. **テキスト→音素変換**: `espeak_TextToPhonemes()`でIPA音素列を取得
3. **音素→トークンID変換**: `tokens.txt`のマッピングを使用してIDに変換
4. **特殊トークン追加**: BOS（開始）とEOS（終了）トークンを追加

### 5.3 TextEncoder / FMDecoder / Vocos

ONNXモデルの推論を行うラッパークラスです。

#### なぜ必要か

エクスポートした3つのONNXモデルをUnity AI Inference Engineで実行するため、それぞれのモデルに対応したラッパークラスを作成します。

#### 実装

各モデルは同様のパターンで実装しています。

```csharp
// モデル読み込み
var model = ModelLoader.Load(modelAsset);
var worker = new Worker(model, BackendType.GPUCompute);

// 推論実行
worker.SetInput("input_name", inputTensor);
worker.Schedule();

// 出力取得
var output = worker.PeekOutput("output_name") as Tensor<float>;
return output.ReadbackAndClone();
```

#### 注意点：スカラー入力のテンソル形状

ONNXモデルにスカラー値を渡す際、`TensorShape(1)`（rank 1）ではなく`TensorShape()`（rank 0）で作成する必要があります。

```csharp
// 正しい: rank 0 (scalar)
var t = new Tensor<float>(new TensorShape(), new float[] { value });

// 誤り: rank 1 - これではエラーになる
// var t = new Tensor<float>(new TensorShape(1), new float[] { value });
```

| 入力 | 正しい形状 | 誤った形状 |
|------|-----------|-----------|
| prompt_features_len | TensorShape() | TensorShape(1) |
| speed | TensorShape() | TensorShape(1) |
| t | TensorShape() | TensorShape(1) |
| guidance_scale | TensorShape() | TensorShape(1) |

### 5.4 EulerSolver

Flow MatchingのODE積分を行うソルバーです。

#### なぜ必要か

Flow Matchingでは、ノイズから音声特徴量への変換をODE（常微分方程式）の積分として定式化しています。FMDecoderは各時刻での「速度」を出力するので、これをEuler法で積分して最終的な特徴量を得ます。

#### 実装

```csharp
public class EulerSolver
{
    // タイムステップを生成（t_shiftで非線形変換）
    public float[] GetTimesteps()
    {
        float[] timesteps = new float[NumSteps + 1];
        for (int i = 0; i <= NumSteps; i++)
        {
            float t = (float)i / NumSteps;
            timesteps[i] = TShift * t / (1f + (TShift - 1f) * t);
        }
        return timesteps;
    }
}
```

FMDecoderでの使用：

```csharp
// 初期状態はガウスノイズ
var x = GenerateGaussianNoise(shape);

// Euler積分ループ
for (int step = 0; step < solver.NumSteps; step++)
{
    float t = timesteps[step];
    float dt = timesteps[step + 1] - timesteps[step];

    // FMDecoderで速度を計算
    var velocity = fmDecoder.ExecuteStep(t, x, textCond, speechCond, guidanceScale);

    // Eulerステップ: x = x + dt * v
    x = x + dt * velocity;
}
```

### 5.5 FeatureExtractor

プロンプト音声からメルスペクトログラムを抽出するコンポーネントです。

#### なぜ必要か

ZipVoiceはゼロショットTTSであり、参照音声（プロンプト）の声質を模倣して音声を生成します。そのため、プロンプト音声からメル特徴量を抽出し、FMDecoderの入力として使用します。

#### 実装

Vocos/ZipVoiceと同じ設定でメルスペクトログラムを計算する必要があります。

**重要な設定**:
- `power=1`（magnitude spectrum）: `power=2`（power spectrum）ではない
- `center=True`: 信号の両端にn_fft/2のreflect paddingを適用

```csharp
public float[,] ExtractMelSpectrogram(AudioClip audioClip)
{
    // 1. AudioClipからサンプルを取得
    float[] samples = new float[audioClip.samples];
    audioClip.GetData(samples, 0);

    // 2. Center padding（reflect）- Pythonのtorch.stft(center=True)と同等
    int padLength = _nFft / 2;
    float[] paddedSamples = ApplyReflectPadding(samples, padLength);

    // 3. STFT計算（NWaves使用）
    // 4. メルフィルターバンク適用
    // 5. 対数スケール変換

    return melSpectrogram;
}
```

### 5.6 ISTFTProcessor

STFT係数から音声波形を生成するコンポーネントです。

#### なぜ必要か

Unity AI Inference EngineはFFT/IFFT演算子をサポートしていないため、VocosのISTFT部分をC#で実装する必要があります。Vocosが出力するmagnitude、phase_cos、phase_sinからISTFTで波形を再構成します。

#### 実装

NWavesライブラリのFFT機能を使用して実装しています。

```csharp
public float[] Process(float[] magnitude, float[] phaseCos, float[] phaseSin,
                       int numBins, int numFrames)
{
    float[] output = new float[expectedLength];

    for (int frame = 0; frame < numFrames; frame++)
    {
        // 1. 複素スペクトルを構築: real = mag * cos, imag = mag * sin
        for (int f = 0; f < numBins; f++)
        {
            real[f] = magnitude[frame, f] * phaseCos[frame, f];
            imag[f] = magnitude[frame, f] * phaseSin[frame, f];
        }

        // 2. IFFTで時間領域に変換
        _fft.Inverse(real, imag);

        // 3. 窓関数を適用してオーバーラップ加算
        OverlapAdd(output, real, frame * _hopLength);
    }

    return output;
}
```

### 5.7 ZipVoiceManager

全コンポーネントを統合したメインAPIです。

#### なぜ必要か

上記のコンポーネントを正しい順序で呼び出し、音声合成パイプラインを実行するための統合クラスです。ユーザーはこのクラスを通じて簡単に音声合成を行えます。

#### 実装

```csharp
public async UniTask<AudioClip> SynthesizeAsync(
    string text,           // 合成するテキスト
    AudioClip promptAudio, // 参照音声（声質の元）
    string promptText,     // 参照音声のテキスト
    SynthesisOptions options = null)
{
    // 1. テキストをトークン化
    int[] tokens = _tokenizer.Tokenize(text);
    int[] promptTokens = _tokenizer.Tokenize(promptText);

    // 2. プロンプト音声からメル特徴量を抽出
    float[,] promptMel = _featureExtractor.ExtractMelSpectrogram(promptAudio);

    // 3. TextEncoderで条件ベクトルを生成
    using var textCondition = _textEncoder.Execute(tokens, promptTokens, ...);

    // 4. 音声条件を作成（プロンプトメル特徴量 × feat_scale）
    using var speechCondition = CreateSpeechCondition(promptMel, ...);

    // 5. EulerSolverでFMDecoderを積分（ノイズ→メル特徴量）
    using var melFeatures = await _fmDecoder.GenerateAsync(
        solver, textCondition, speechCondition, guidanceScale);

    // 6. プロンプト部分をトリムして生成部分のみを取得
    using var trimmedMel = TrimPromptFrames(melFeatures, promptFeaturesLen);

    // 7. Vocosでメル→STFT係数
    using var vocosOutput = _vocos.Execute(trimmedMel);

    // 8. ISTFTで波形に変換
    float[] waveform = _istftProcessor.Process(...);

    // 9. AudioClipを作成して返す
    AudioClip clip = AudioClip.Create("Synthesized", waveform.Length, 1, 24000, false);
    clip.SetData(waveform, 0);
    return clip;
}
```

---

## 6. 遭遇した問題と解決策

### 6.1 テンソル形状の不一致

**問題**: ONNXモデルの入力にスカラー値を渡す際、`TensorShape(1)`で作成するとエラーが発生。

**原因**: ONNXモデルはrank 0のスカラーを期待しているが、`TensorShape(1)`はrank 1（要素数1の配列）になる。

**解決策**: `TensorShape()`でrank 0のスカラーテンソルを作成。

```csharp
// 正しい形状
| 入力 | 正しい形状 | 誤った形状 |
|------|-----------|-----------|
| prompt_features_len | TensorShape() (rank 0) | TensorShape(1) (rank 1) |
| speed | TensorShape() (rank 0) | TensorShape(1) (rank 1) |
| t | TensorShape() (rank 0) | TensorShape(1) (rank 1) |
| guidance_scale | TensorShape() (rank 0) | TensorShape(1) (rank 1) |
```

### 6.2 メルスペクトログラムの不一致

**問題**: 合成音声の品質が悪く、Python版と異なる結果になる。

**原因**: メルスペクトログラムの計算設定がPythonと異なっていた。
- `power=2`（power spectrum）を使用していた → `power=1`（magnitude spectrum）が正しい
- center paddingが実装されていなかった

**解決策**: Vocos/ZipVoiceのfbank設定に合わせて修正。

```csharp
// power=1: マグニチュードスペクトル
float magnitude = Mathf.Sqrt(real * real + imag * imag);

// center=True: reflect padding適用
int padLength = nFft / 2;
float[] paddedSamples = ApplyReflectPadding(samples, padLength);
```

### 6.3 espeak-ngとpiper_phonemizeの差異

**問題**: ZipVoiceのPython版はpiper_phonemizeを使用しているが、espeak_TextToPhonemes関数は句読点を出力しない。

**原因**: espeak-ngの音素出力関数は、テキストの句読点を音素列に含めない仕様。

**解決策**: 元テキストの末尾句読点を手動でトークン列に追加。

```csharp
// 元テキストの末尾句読点を確認して追加
if (text.EndsWith("."))
    tokens.Add(_tokenMap.GetTokenId("."));
else if (text.EndsWith("!"))
    tokens.Add(_tokenMap.GetTokenId("!"));
else if (text.EndsWith("?"))
    tokens.Add(_tokenMap.GetTokenId("?"));
```

---

## 7. パフォーマンス最適化

### 7.1 ボトルネックの特定

プロファイリングの結果、以下がボトルネックでした：

| 処理 | 時間（8ステップ） | 割合 |
|------|------------------|------|
| FMDecoder推論 | 約12秒 | 約96% |
| TextEncoder推論 | 約0.5秒 | 約2% |
| Vocos推論 | 約0.3秒 | 約1% |
| その他（ISTFT等） | 約0.2秒 | 約1% |

**主な原因**: GPU-CPU間のデータ転送が毎ステップ発生

**蒸留モデルの利点**: ベースモデルはCFG計算のため各ステップで2回の推論が必要でしたが、蒸留モデルはCFGが内蔵されているため1回で済みます。さらにステップ数も4-8に削減可能です。

### 7.2 実装した最適化

#### バッファ再利用

Eulerステップ間でバッファを再利用し、メモリアロケーションを削減。

```csharp
// バッファを再利用
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

#### デバッグログの条件付きコンパイル

リリースビルドではデバッグログを無効化。

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log($"[FMDecoder] Step {step}: t={t:F4}");
#endif
```

### 7.3 UIフリーズ対応

UniTaskを使用して、重い処理の間にUIに制御を戻します。

```csharp
// FMDecoderのEulerループ内で毎ステップyield
for (int step = 0; step < solver.NumSteps; step++)
{
    // 推論処理...

    // UIに制御を戻す
    await UniTask.Yield();

    // 進捗コールバック
    onProgress?.Invoke((float)(step + 1) / solver.NumSteps);
}
```

ZipVoiceManager側でも、各重い処理の後にyieldを挿入。

```csharp
// TextEncoder後
await UniTask.Yield();

// FMDecoder後（内部でもyield）
using var melFeatures = await _fmDecoder.GenerateAsync(...);

// Vocos後
await UniTask.Yield();

// ISTFT後
await UniTask.Yield();
```

---

## 参考リンク

- [uZipVoice GitHub](https://github.com/ayutaz/uZipVoice)
- [uZipVoice ONNX Models (Hugging Face)](https://huggingface.co/ayousanz/uZipVoice-onnx)
- [ZipVoice（元プロジェクト）](https://github.com/k2-fsa/ZipVoice)
- [Unity AI Inference Engine Documentation](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.4/manual/)
- [Unity AI Inference Engine - Supported Operators](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.4/manual/supported-operators.html)
- [NWaves](https://github.com/ar1st0crat/NWaves)
- [espeak-ng](https://github.com/espeak-ng/espeak-ng)
- [piper-unity](https://github.com/Macoron/piper-unity) - espeak-ng統合の参考
