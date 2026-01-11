# 技術選定・アーキテクチャ

## 1. 技術選定

### 最終構成

| コンポーネント | 選定技術 | バージョン | ライセンス |
|---------------|---------|-----------|-----------|
| 推論エンジン | Unity AI Inference Engine | 2.3 | Unity |
| G2P (Tokenizer) | espeak-ng | 1.52 | GPLv3 |
| ISTFT | NWaves | 0.9.6 | MIT |
| Euler Solver | C#実装 | - | - |

### 選定理由

#### Unity AI Inference Engine 2.3
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
- STFT/ISTFTが実装済み
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
- 入力: 英語テキスト
- 出力: IPA音素列 → トークンID列
- 処理:
  1. `espeak_TextToPhonemes()` でIPA変換
  2. `tokens.txt` でID変換

#### 2. Text Encoder
- 入力: トークンID列
- 出力: テキスト条件ベクトル [1, T, 512]
- 推論: Inference Engine (GPU)

#### 3. FM Decoder + Euler Solver
- 入力: ノイズ、テキスト条件、音声条件
- 出力: メル特徴量 [1, T, 100]
- 処理:
  1. ランダムノイズ初期化
  2. 8-16ステップのEuler積分
  3. CFG (Classifier-Free Guidance) 適用

#### 4. Vocos
- 入力: メル特徴量 [1, 100, T]
- 出力: STFT係数（magnitude, phase_cos, phase_sin）
- 推論: Inference Engine (GPU)

#### 5. ISTFT (NWaves)
- 入力: STFT係数
- 出力: 波形データ (24kHz)
- 処理: 逆短時間フーリエ変換

---

## 3. 外部依存関係

### Unity Packages

| パッケージ | バージョン | 用途 |
|-----------|-----------|------|
| com.unity.ai.inference | 2.3.x | ONNX推論 |

### ネイティブプラグイン

| ファイル | プラットフォーム | 用途 |
|---------|----------------|------|
| libespeak-ng.dll | Windows x64 | G2P |
| libespeak-ng.1.dylib | macOS x64 | G2P |
| libespeak-ng.so | Android arm64 | G2P |

### マネージドDLL

| ファイル | 用途 |
|---------|------|
| NWaves.dll | ISTFT処理 |

### データファイル

| ファイル | 用途 |
|---------|------|
| espeak-ng-data/ | espeak-ng言語データ |
| tokens.txt | 音素→トークンIDマッピング |
| model.json | モデル設定 |

---

## 4. ライセンス考慮事項

| コンポーネント | ライセンス | 商用利用 | 注意点 |
|---------------|-----------|---------|--------|
| Unity AI Inference Engine | Unity | ◯ | Unity利用規約に準拠 |
| espeak-ng | GPLv3 | △ | ソース公開義務あり |
| NWaves | MIT | ◯ | 著作権表示のみ |
| ZipVoice モデル | 要確認 | 要確認 | k2-fsaライセンス確認 |

**注意**: espeak-ngはGPLv3のため、商用利用時はライセンス確認が必要。

---

## 5. パフォーマンス目標

| 項目 | 目標値 |
|------|--------|
| 推論バックエンド | GPU (GPUCompute) |
| Euler Solverステップ数 | 8-16 |
| 出力サンプリングレート | 24kHz |
| リアルタイム係数 | < 1.0 (リアルタイム以上) |

---

## 6. 対応プラットフォーム

| プラットフォーム | 対応状況 | 備考 |
|-----------------|---------|------|
| Windows x64 | ◯ | 主要開発環境 |
| macOS x64 | ◯ | espeak-ng対応 |
| Android arm64 | ◯ | espeak-ng対応 |
| iOS | △ | espeak-ng要ビルド |
| WebGL | × | ネイティブDLL非対応 |
