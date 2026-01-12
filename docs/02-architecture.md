# 技術選定・アーキテクチャ

## 1. 技術選定

### 最終構成

| コンポーネント | 選定技術 | バージョン | ライセンス |
|---------------|---------|-----------|-----------|
| 推論エンジン | Unity AI Inference Engine | 2.4.1 | Unity |
| G2P (Tokenizer) | espeak-ng | 1.52 | GPLv3 |
| ISTFT | NWaves + カスタム実装 | 0.9.6 | MIT |
| Euler Solver | C#実装 | - | - |
| 非同期処理 | UniTask | 2.5.10 | MIT |

### 選定理由

#### Unity AI Inference Engine 2.4.1
- Unity公式パッケージで安定性が高い
- ONNX Opset 7-15対応（ZipVoiceモデルと互換）
- GPU推論対応（BackendType.GPUCompute）
- 全Unityプラットフォーム対応

#### espeak-ng
- ZipVoiceと完全互換のIPA音素出力
- piper-unityで実績あり、実装難易度低
- マルチプラットフォーム対応（Windows/macOS/Android）
- 100+言語対応で拡張性あり

#### NWaves
- FFT/IFFT実装済み
- .NET Standard対応でUnity互換
- MITライセンスで商用利用可能
- 依存関係なし

---

## 2. システムアーキテクチャ

### 推論パイプライン

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

### コンポーネント詳細

#### 1. Tokenizer (espeak-ng)
- 入力: テキスト
- 出力: IPA音素列 → トークンID列
- 処理:
  1. `espeak_TextToPhonemes()` でIPA変換
  2. `tokens.txt` でID変換
  3. 末尾句読点を手動追加（piper_phonemize互換）

#### 2. Text Encoder
- 入力: トークンID列、プロンプトトークンID列
- 出力: テキスト条件ベクトル [1, T, 512]
- 推論: AI Inference Engine (GPU)

#### 3. Feature Extractor
- 入力: プロンプト音声 (AudioClip)
- 出力: メルスペクトログラム [T, 100]
- 処理:
  - リサンプリング（24kHz）
  - STFT (n_fft=1024, hop_length=256)
  - メルフィルターバンク適用
  - 対数スケール変換
  - **重要**: power=1, center=True

#### 4. FM Decoder + Euler Solver
- 入力: ノイズ、テキスト条件、音声条件
- 出力: メル特徴量 [1, T, 100]
- 処理:
  1. ガウスノイズ初期化
  2. 8-16ステップのEuler積分
  3. CFG (Classifier-Free Guidance) 適用
  4. feat_scale = 0.1 でスケーリング

#### 5. Vocos
- 入力: メル特徴量 [1, 100, T]
- 出力: STFT係数（magnitude, phase_cos, phase_sin）
- 推論: AI Inference Engine (GPU)

#### 6. ISTFT (NWaves + カスタム)
- 入力: STFT係数
- 出力: 波形データ (24kHz)
- 処理: 逆短時間フーリエ変換、Hannウィンドウ

---

## 3. Unity ONNX推論の制約事項

### 未サポート演算子

Unity AI Inference Engineで使用できない演算子：

| 演算子 | 説明 | 代替手段 |
|--------|------|---------|
| FFT | 高速フーリエ変換 | C#実装（NWaves） |
| IFFT | 逆FFT | C#実装（NWaves） |
| RFFT | 実数FFT | C#実装 |
| IRFFT | 逆実数FFT | C#実装（ISTFT） |
| Log1p | log(1+x) | Log(x+1)で代替 |
| ComplexAbs | 複素数絶対値 | Sqrt(re²+im²)で計算 |

### エクスポート時の対応

ZipVoiceモデルのONNXエクスポート時に以下の対応が必要：

1. **Vocosモデル**: ISTFTをONNX外に分離
   - magnitude, phase_cos, phase_sin を出力
   - Unity側でISTFTを実装

2. **TextEncoder/FMDecoder**: そのままエクスポート可能

3. **Opset設定**: version 15を使用

### テンソル形状の制約

- 最大次元数: 8次元
- バッチサイズ: 1を推奨（動的バッチは制限あり）
- シーケンス長: 動的対応可能

---

## 4. 外部依存関係

### Unity Packages

| パッケージ | バージョン | 用途 |
|-----------|-----------|------|
| com.unity.ai.inference | 2.4.1 | ONNX推論 |
| com.cysharp.unitask | 2.5.10 | 非同期処理 |
| com.unity.textmeshpro | 4.0.0 | UI |

### ネイティブプラグイン

| ファイル | プラットフォーム | 用途 |
|---------|----------------|------|
| libespeak-ng.dll | Windows x64 | G2P |
| libespeak-ng.1.dylib | macOS x64 | G2P |
| libespeak-ng.so | Android arm64 | G2P |

### マネージドDLL

| ファイル | 用途 |
|---------|------|
| NWaves.dll | FFT/IFFT処理 |

### データファイル

| ファイル | 用途 |
|---------|------|
| espeak-ng-data/ | espeak-ng言語データ |
| tokens.txt | 音素→トークンIDマッピング |

---

## 5. ライセンス考慮事項

| コンポーネント | ライセンス | 商用利用 | 注意点 |
|---------------|-----------|---------|--------|
| Unity AI Inference Engine | Unity | ◯ | Unity利用規約に準拠 |
| espeak-ng | GPLv3 | △ | ソース公開義務あり |
| NWaves | MIT | ◯ | 著作権表示のみ |
| UniTask | MIT | ◯ | 著作権表示のみ |
| ZipVoice モデル | Apache 2.0 | ◯ | k2-fsaライセンス |

**注意**: espeak-ngはGPLv3のため、商用利用時はライセンス確認が必要。

---

## 6. パフォーマンス

### 測定結果（Windows, RTX GPU）

| 項目 | 値 |
|------|------|
| 推論バックエンド | GPUCompute |
| Euler Solverステップ数 | 16 |
| 出力サンプリングレート | 24kHz |
| 合成時間（1秒音声） | 約25秒 |
| 1ステップあたり | 約1.5秒 |

### 最適化ポイント

| 最適化 | 説明 |
|-------|------|
| バッファ再利用 | Eulerステップ間で`_xBuffer`を再利用 |
| Yield頻度削減 | 4ステップごとに`UniTask.Yield()` |
| TensorShapeキャッシュ | ループ内でTensorShapeを再利用 |

---

## 7. 対応プラットフォーム

| プラットフォーム | 対応状況 | 備考 |
|-----------------|---------|------|
| Windows x64 | ◯ | 主要開発環境、GPU推論対応 |
| macOS x64 | △ | espeak-ng対応（要テスト） |
| Android arm64 | △ | espeak-ng対応（要テスト） |
| iOS | × | espeak-ng要ビルド |
| WebGL | × | ネイティブDLL非対応 |
