# uZipVoice

[ZipVoice](https://github.com/k2-fsa/ZipVoice)的Unity实现 - 基于Flow Matching的轻量级零样本语音合成系统。

[English](README.md) | [日本語](README_ja.md) | 中文

## 特性

- **零样本TTS**: 仅需几秒参考音频即可生成任意声音
- **快速生成**: Flow Matching技术实现4-16步高质量语音合成
- **Unity原生**: 基于Unity 6的AI Inference Engine (Sentis)构建
- **跨平台**: 支持Windows（GPU/CPU推理）

## 系统要求

- Unity 6 (6000.0.38f1或更高版本)

## 安装

```bash
git clone https://github.com/ayutaz/uZipVoice.git
```

使用Unity 6打开项目。依赖项（espeak-ng数据、包）都已包含在内。

### 设置ONNX模型

从[Hugging Face](https://huggingface.co/ayousanz/uZipVoice-onnx)下载ONNX模型并放置到`Assets/uZipVoice/Models/`:

| 文件 | 描述 |
|-----|------|
| `text_encoder.onnx` | 文本到条件向量转换 |
| `fm_decoder.onnx` | Flow Matching解码器 |
| `vocos_opset15.onnx` | 声码器（梅尔到STFT） |

## 快速开始

### 使用示例场景

1. 打开示例场景: `Assets/uZipVoice/Samples/TTSSample.unity`
2. ONNX模型和tokens.txt已在ZipVoiceManager组件中预配置
3. 进入Play模式测试语音合成

### 编程使用

```csharp
using Cysharp.Threading.Tasks;
using uZipVoice.Core;
using UnityEngine;

public class TTSExample : MonoBehaviour
{
    public ZipVoiceManager zipVoice;
    public AudioSource audioSource;
    public AudioClip promptAudio; // 参考音频（可选）

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

        // promptAudio为null时使用默认声音估计
        AudioClip clip = await zipVoice.SynthesizeAsync(
            "你好，这是一个测试。",
            promptAudio,
            "参考音频的文本内容。",
            options
        );

        audioSource.clip = clip;
        audioSource.Play();
    }
}
```

## 架构

```
文本输入
    │
    ▼ 分词器 (espeak-ng G2P)
Token ID序列
    │
    ▼ TextEncoder (ONNX)
条件向量
    │
    ▼ FMDecoder (ONNX) × N步 (Euler ODE)
梅尔特征 (100维)
    │
    ▼ Vocos (ONNX)
STFT系数
    │
    ▼ ISTFT (NWaves库)
波形 (24kHz)
```

## 组件

| 组件 | 描述 |
|-----|------|
| `ZipVoiceManager` | TTS合成的主API |
| `ZipVoiceConfig` | 配置用ScriptableObject |
| `EspeakTokenizer` | 使用espeak-ng进行文本到音素转换 |
| `TokenMap` | 音素到Token ID映射 |
| `TextEncoder` | 文本编码的ONNX推理 |
| `FMDecoder` | 带Euler求解器的Flow Matching解码器 |
| `Vocos` | 梅尔到STFT转换的声码器 |
| `ISTFTProcessor` | 使用NWaves库的逆STFT |
| `FeatureExtractor` | 从音频提取梅尔频谱图 |

## 性能优化

FMDecoder包含多项快速合成优化:

| 优化 | 描述 |
|-----|------|
| 缓冲区复用 | 在Euler步骤间复用`_xBuffer`以减少内存分配 |
| 减少Yield | 每4步调用一次`UniTask.Yield()`而非每步 |
| TensorShape缓存 | 在Euler积分循环中复用TensorShape |

### 性能提示

- **NumSteps**: 较低值（4-8）更快但可能降低质量。较高值（16-32）提供更好质量。
- **Backend**: 在支持的硬件上使用`GPUCompute`获得最佳性能。
- **批量大小**: 处理单个语句以获得最低延迟。

## 配置

### ZipVoiceConfig

| 参数 | 默认值 | 描述 |
|-----|-------|------|
| SampleRate | 24000 | 音频采样率 |
| NFft | 1024 | FFT大小 |
| HopLength | 256 | STFT的跳跃长度 |
| NMels | 100 | 梅尔频带数 |
| NumSteps | 16 | Euler求解器步数（4-32） |
| TShift | 0.5 | 时间偏移参数 |
| GuidanceScale | 1.0 | CFG比例（0-3） |
| Speed | 1.0 | 语速（0.5-2.0） |
| Voice | en-us | espeak-ng声音 |

## 项目结构

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

## 测试

项目包含97个单元测试:

| 测试类 | 测试数 | 描述 |
|-------|-------|------|
| TokenMapTests | 24 | Token映射验证 |
| EulerSolverTests | 32 | ODE求解器验证 |
| EspeakTokenizerTests | 19 | G2P转换测试 |
| TensorShapeTests | 22 | ONNX张量形状验证 |

通过Unity Test Runner（Window > General > Test Runner）运行测试。

## 许可证

MIT License

**注意**: 本项目使用[espeak-ng](https://github.com/espeak-ng/espeak-ng)（GPLv3）进行文本到音素转换。有关第三方许可证详情，请参阅[THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md)。

## 致谢

- [ZipVoice](https://github.com/k2-fsa/ZipVoice) - 原始Python实现
- [espeak-ng](https://github.com/espeak-ng/espeak-ng) - 文本到音素转换
- [Vocos](https://github.com/gemelo-ai/vocos) - 神经声码器
- [NWaves](https://github.com/ar1st0crat/NWaves) - 用于ISTFT的数字信号处理库

## 相关项目

- [ZipVoice](https://github.com/k2-fsa/ZipVoice) - 原始实现
- [piper-unity](https://github.com/Macoron/piper-unity) - espeak-ng集成参考
