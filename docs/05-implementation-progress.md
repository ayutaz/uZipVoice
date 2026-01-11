# 実装進捗

## 1. 概要

uZipVoiceプロジェクトの実装進捗を記録するドキュメントです。

### 進捗サマリー

| カテゴリ | 完了 | 残り | 進捗率 |
|---------|-----|------|--------|
| コアコンポーネント | 10 | 1 | 91% |
| テスト | 75 | - | 100% (実装済み分) |

---

## 2. 実装済みコンポーネント

### TokenMap（Tokenizer）

**ファイル**: `Assets/uZipVoice/Runtime/Tokenizer/TokenMap.cs`

音素からトークンIDへのマッピングを管理するクラス。

**テスト**: 24テストケース、全て成功

---

### ITokenizer + EspeakTokenizer（Tokenizer）

**ファイル**:
- `Assets/uZipVoice/Runtime/Tokenizer/ITokenizer.cs`
- `Assets/uZipVoice/Runtime/Tokenizer/EspeakNative.cs`
- `Assets/uZipVoice/Runtime/Tokenizer/EspeakTokenizer.cs`

espeak-ngを使用したG2P（Grapheme-to-Phoneme）変換。

**機能**:
- ITokenizerインターフェース定義
- espeak-ng P/Invokeラッパー
- テキスト→IPA音素→トークンID変換
- 複数言語対応（en-us, en-gbなど）

**テスト**: 19テストケース、全て成功

---

### EulerSolver（Inference）

**ファイル**: `Assets/uZipVoice/Runtime/Inference/EulerSolver.cs`

Flow MatchingのためのEuler ODE積分ソルバー。

**テスト**: 32テストケース、全て成功

---

### TextEncoder（Inference）

**ファイル**: `Assets/uZipVoice/Runtime/Inference/TextEncoder.cs`

テキストトークンを条件ベクトルに変換するONNX推論ラッパー。

**機能**:
- Unity AI Inference Engine 2.4対応
- GPU/CPUバックエンド選択可能
- 入力: tokens, prompt_tokens, prompt_features_len, speed
- 出力: text_condition [1, T, 512]

---

### FMDecoder（Inference）

**ファイル**: `Assets/uZipVoice/Runtime/Inference/FMDecoder.cs`

Flow Matchingデコーダ。EulerSolverと統合してメル特徴量を生成。

**機能**:
- 単一ステップ推論
- EulerSolverを使用した全ステップ積分
- CFG (Classifier-Free Guidance)対応

---

### Vocos（Inference）

**ファイル**: `Assets/uZipVoice/Runtime/Inference/Vocos.cs`

Vocoder。メルスペクトログラムからSTFT係数を生成。

**機能**:
- メル特徴量→STFT係数変換
- 出力: magnitude, phase_cos, phase_sin

---

### ISTFTProcessor（Audio）

**ファイル**: `Assets/uZipVoice/Runtime/Audio/ISTFTProcessor.cs`

逆短時間フーリエ変換。STFT係数から波形を生成。

**機能**:
- Hannウィンドウ
- オーバーラップ加算
- カスタムIFFT実装

---

### FeatureExtractor（Audio）

**ファイル**: `Assets/uZipVoice/Runtime/Audio/FeatureExtractor.cs`

音声波形からメルスペクトログラムを抽出。

**機能**:
- STFT計算
- メルフィルターバンク適用
- リサンプリング対応
- AudioClipからの直接抽出

---

### ZipVoiceConfig（Core）

**ファイル**: `Assets/uZipVoice/Runtime/Core/ZipVoiceConfig.cs`

設定管理用ScriptableObject。

---

### ZipVoiceManager（Core）

**ファイル**: `Assets/uZipVoice/Runtime/Core/ZipVoiceManager.cs`

メインAPI。すべてのコンポーネントを統合。

**機能**:
- 非同期初期化
- 音声合成（テキスト→AudioClip）
- プロンプト音声による声質制御
- 合成オプション（ステップ数、速度、CFGスケール）

---

## 3. 未実装コンポーネント

| コンポーネント | 説明 | 優先度 |
|--------------|------|--------|
| サンプルシーン | デモシーン・UIサンプル | 中 |

---

## 4. テスト実装状況

### Edit Mode Tests

| テストクラス | テスト数 | 状態 |
|------------|---------|------|
| TokenMapTests | 24 | ✅ 全成功 |
| EulerSolverTests | 32 | ✅ 全成功 |
| EspeakTokenizerTests | 19 | ✅ 全成功 |
| **合計** | **75** | **✅ 全成功** |

---

## 5. コミット履歴

| 日付 | コミット | 内容 |
|------|---------|------|
| 2026-01-11 | 2248a46 | Add Unity AI Inference Engine package |
| 2026-01-11 | f700375 | Update implementation progress documentation |
| 2026-01-11 | 432ec64 | Add core TTS pipeline components |
| 2026-01-11 | 5b715c0 | Add espeak-ng native plugin for Windows |
| 2026-01-11 | f719280 | Add ITokenizer interface and EspeakTokenizer |
| 2026-01-11 | 6020eb1 | Update documentation with implementation progress |
| 2026-01-11 | eff6dce | Add Microsoft.CodeAnalysis.CSharp via OpenUPM |
| 2026-01-11 | 2e7bf51 | Add TokenMap, EulerSolver with unit tests |
| 2026-01-11 | a20c8a0 | Add test specification document |
| 2026-01-11 | a8752f0 | Add technical documentation |
| 2026-01-11 | d931da4 | Add uZipVoice project structure and config files |
| 2026-01-11 | 5bcb4a5 | Add .gitattributes for consistent line endings |
| 2026-01-11 | 4dd2a8f | Initial commit: Unity project setup for uZipVoice |

---

## 6. 次のステップ

1. **サンプルシーン作成**
   - TTSSample.unity シーン
   - UIコントローラー
   - 使用方法のデモ

2. **E2Eテスト**
   - 実際のONNXモデルを使用した統合テスト
   - 音声品質の確認

3. **最適化**
   - FFT/IFFTの最適化（NWaves導入検討）
   - メモリ使用量の最適化

---

## 7. 技術的メモ

### espeak-ng セットアップ

1. `libespeak-ng.dll` を `Assets/uZipVoice/Plugins/Windows/x64/` に配置
2. `espeak-ng-data/` を `Assets/StreamingAssets/` に配置

espeak-ngインストール済みの場合:
```
C:\Program Files\eSpeak NG\espeak-ng-data → Assets\StreamingAssets\espeak-ng-data
```

### Unity AI Inference Engine

ONNX推論に必要なパッケージ:

```
Package: com.unity.ai.inference
Version: 2.4.1
Namespace: Unity.InferenceEngine
Assembly: Unity.InferenceEngine
```

インストール: Package Manager → Add package by name → `com.unity.ai.inference`

---

### OpenUPMスコープレジストリ

uLoopMCPでテスト実行するために必要:

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": ["org.nuget"]
    }
  ]
}
```

---

*最終更新: 2026-01-11*
