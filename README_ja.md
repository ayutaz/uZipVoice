# uZipVoice

[ZipVoice](https://github.com/Zengyi-Qin/ZipVoice)のUnity実装 - Flow Matchingを使用した軽量ゼロショット音声合成システム。

[English](README.md) | 日本語 | [中文](README_zh.md)

## 特徴

- **ゼロショットTTS**: 数秒の参照音声だけで任意の声を生成
- **高速生成**: Flow Matchingにより4〜16ステップで高品質な音声合成
- **Unity Native**: Unity 6のAI Inference Engine (Sentis)で構築
- **クロスプラットフォーム**: Windows対応（GPU/CPU推論）

## 動作要件

- Unity 6 (6000.0.38f1以降)

## インストール

```bash
git clone https://github.com/ayutaz/uZipVoice.git
```

Unity 6でプロジェクトを開きます。依存関係（espeak-ngデータ、パッケージ）は同梱されています。

### ONNXモデルのセットアップ

オリジナルの[ZipVoice](https://github.com/Zengyi-Qin/ZipVoice)プロジェクトからONNXモデルをエクスポートし、`Assets/uZipVoice/Models/`に配置:

| ファイル | 説明 |
|---------|------|
| `text_encoder.onnx` | テキスト→条件ベクトル変換 |
| `fm_decoder.onnx` | Flow Matchingデコーダ |
| `vocos_opset15.onnx` | ボコーダ（メル→STFT） |

## クイックスタート

### サンプルシーンの使用

1. サンプルシーンを開く: `Assets/uZipVoice/Samples/TTSSample.unity`
2. ONNXモデルとtokens.txtはZipVoiceManagerコンポーネントに事前設定済み
3. Playモードで音声合成をテスト

### プログラムからの使用

```csharp
using Cysharp.Threading.Tasks;
using uZipVoice.Core;
using UnityEngine;

public class TTSExample : MonoBehaviour
{
    public ZipVoiceManager zipVoice;
    public AudioSource audioSource;
    public AudioClip promptAudio; // 参照音声（省略可）

    void Start()
    {
        InitializeAndSynthesize().Forget();
    }

    async UniTask InitializeAndSynthesize()
    {
        await zipVoice.InitializeAsync();

        var options = new SynthesisOptions
        {
            NumSteps = 16,
            Speed = 1.0f,
            GuidanceScale = 1.0f
        };

        // promptAudioがnullの場合はデフォルトの声で推定
        AudioClip clip = await zipVoice.SynthesizeAsync(
            "こんにちは、テストです。",
            promptAudio,
            "参照音声のテキスト内容。",
            options
        );

        audioSource.clip = clip;
        audioSource.Play();
    }
}
```

## アーキテクチャ

```
テキスト入力
    │
    ▼ トークナイザ (espeak-ng G2P)
トークンID列
    │
    ▼ TextEncoder (ONNX)
条件ベクトル
    │
    ▼ FMDecoder (ONNX) × Nステップ (Euler ODE)
メル特徴量 (100次元)
    │
    ▼ Vocos (ONNX)
STFT係数
    │
    ▼ ISTFT (NWavesライブラリ)
波形 (24kHz)
```

## コンポーネント

| コンポーネント | 説明 |
|--------------|------|
| `ZipVoiceManager` | TTS合成のメインAPI |
| `ZipVoiceConfig` | 設定用ScriptableObject |
| `EspeakTokenizer` | espeak-ngによるテキスト→音素変換 |
| `TokenMap` | 音素→トークンIDマッピング |
| `TextEncoder` | テキストエンコーディングのONNX推論 |
| `FMDecoder` | Eulerソルバ付きFlow Matchingデコーダ |
| `Vocos` | メル→STFT変換用ボコーダ |
| `ISTFTProcessor` | NWavesライブラリによる逆STFT |
| `FeatureExtractor` | 音声からメルスペクトログラム抽出 |

## パフォーマンス最適化

FMDecoderには高速合成のための最適化が含まれています:

| 最適化 | 説明 |
|-------|------|
| バッファ再利用 | Eulerステップ間で`_xBuffer`を再利用してメモリ割り当てを削減 |
| Yield頻度削減 | 毎ステップではなく4ステップごとに`UniTask.Yield()`を呼び出し |
| TensorShapeキャッシュ | Euler積分ループでTensorShapeを再利用 |

### パフォーマンスのヒント

- **NumSteps**: 低い値（4-8）は高速だが品質が下がる可能性あり。高い値（16-32）で品質向上。
- **Backend**: 対応ハードウェアでは`GPUCompute`を使用して最高性能を発揮。
- **バッチサイズ**: 最小レイテンシには単一発話を処理。

## 設定

### ZipVoiceConfig

| パラメータ | デフォルト | 説明 |
|-----------|----------|------|
| SampleRate | 24000 | 音声サンプルレート |
| NFft | 1024 | FFTサイズ |
| HopLength | 256 | STFTのホップ長 |
| NMels | 100 | メルバンド数 |
| NumSteps | 16 | Eulerソルバステップ数（4-32） |
| TShift | 0.5 | タイムシフトパラメータ |
| GuidanceScale | 1.0 | CFGスケール（0-3） |
| Speed | 1.0 | 発話速度（0.5-2.0） |
| Voice | en-us | espeak-ngボイス |

## プロジェクト構造

```
Assets/uZipVoice/
├── Runtime/
│   ├── Audio/
│   │   ├── FeatureExtractor.cs
│   │   └── ISTFTProcessor.cs
│   ├── Core/
│   │   ├── ZipVoiceConfig.cs
│   │   └── ZipVoiceManager.cs
│   ├── Inference/
│   │   ├── EulerSolver.cs
│   │   ├── FMDecoder.cs
│   │   ├── TextEncoder.cs
│   │   └── Vocos.cs
│   └── Tokenizer/
│       ├── EspeakNative.cs
│       ├── EspeakTokenizer.cs
│       ├── ITokenizer.cs
│       └── TokenMap.cs
├── Samples/
│   ├── TTSSample.unity
│   └── TTSSampleController.cs
├── Tests/
│   ├── Editor/
│   │   ├── EulerSolverTests.cs
│   │   ├── TokenMapTests.cs
│   │   └── EspeakTokenizerTests.cs
│   └── Runtime/
├── Models/
│   ├── text_encoder.onnx
│   ├── fm_decoder.onnx
│   └── vocos_opset15.onnx
├── Plugins/
│   ├── NWaves.dll
│   └── Windows/x64/
│       └── libespeak-ng.dll
└── Resources/
    └── tokens.txt
```

## テスト

プロジェクトには97のユニットテストが含まれています:

| テストクラス | テスト数 | 説明 |
|------------|---------|------|
| TokenMapTests | 24 | トークンマッピング検証 |
| EulerSolverTests | 32 | ODEソルバ検証 |
| EspeakTokenizerTests | 19 | G2P変換テスト |
| TensorShapeTests | 22 | ONNXテンソル形状検証 |

テストはUnity Test Runner（Window > General > Test Runner）から実行できます。

## ライセンス

MIT License

## 謝辞

- [ZipVoice](https://github.com/Zengyi-Qin/ZipVoice) - オリジナルのPython実装
- [espeak-ng](https://github.com/espeak-ng/espeak-ng) - テキスト→音素変換
- [Vocos](https://github.com/gemelo-ai/vocos) - ニューラルボコーダ
- [NWaves](https://github.com/ar1st0crat/NWaves) - ISTFTのためのデジタル信号処理ライブラリ

## 関連プロジェクト

- [ZipVoice](https://github.com/Zengyi-Qin/ZipVoice) - オリジナル実装
- [piper-unity](https://github.com/Macoron/piper-unity) - espeak-ng統合の参考
