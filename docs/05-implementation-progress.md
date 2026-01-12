# 実装進捗

## 1. 概要

uZipVoiceプロジェクトの実装は**完了**しています。

### 進捗サマリー

| カテゴリ | 完了 | 残り | 進捗率 |
|---------|-----|------|--------|
| コアコンポーネント | 11 | 0 | 100% |
| テスト | 75 | - | 100% |
| ドキュメント | 6 | 0 | 100% |

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
- 末尾句読点の追加（piper_phonemize互換）
- 複数言語対応（en-us, en-gbなど）

**テスト**: 19テストケース、全て成功

---

### EulerSolver（Inference）

**ファイル**: `Assets/uZipVoice/Runtime/Inference/EulerSolver.cs`

Flow MatchingのためのEuler ODE積分ソルバー。

**機能**:
- t_shiftによるタイムステップ変換
- 可変ステップ数対応（4-32）
- インプレース演算対応

**テスト**: 32テストケース、全て成功

---

### TextEncoder（Inference）

**ファイル**: `Assets/uZipVoice/Runtime/Inference/TextEncoder.cs`

テキストトークンを条件ベクトルに変換するONNX推論ラッパー。

**機能**:
- Unity AI Inference Engine 2.4.1対応
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
- バッファ再利用による最適化
- feat_scale = 0.1 でスケーリング

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
- NWavesライブラリによるIFFT

---

### FeatureExtractor（Audio）

**ファイル**: `Assets/uZipVoice/Runtime/Audio/FeatureExtractor.cs`

音声波形からメルスペクトログラムを抽出。

**機能**:
- STFT計算（NWaves使用）
- メルフィルターバンク適用
- リサンプリング対応
- AudioClipからの直接抽出
- **重要**: power=1（magnitude）、center=True（reflect padding）

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
- プロンプト部分のトリミング

---

### TTSSampleController（Samples）

**ファイル**:
- `Assets/uZipVoice/Samples/TTSSampleController.cs`
- `Assets/uZipVoice/Samples/TTSSample.unity`

TTSデモ用UIコントローラーとサンプルシーン。

**機能**:
- テキスト入力フィールド
- プロンプトテキスト入力
- パラメータ調整（ステップ数、速度、ガイダンス）
- 再生/停止コントロール
- ステータス表示
- 波形デバッグログ

---

## 3. テスト実装状況

### Edit Mode Tests

| テストクラス | テスト数 | 状態 |
|------------|---------|------|
| TokenMapTests | 24 | ✅ 全成功 |
| EulerSolverTests | 32 | ✅ 全成功 |
| EspeakTokenizerTests | 19 | ✅ 全成功 |
| **合計** | **75** | **✅ 全成功** |

---

## 4. 解決済みの問題

### FeatureExtractor設定
- **問題**: メルスペクトログラムの計算がPythonと不一致
- **原因**: power=2（power spectrum）を使用していた
- **解決**: power=1（magnitude spectrum）に修正、center paddingを追加

### プロンプトテキストの不一致
- **問題**: 合成音声に「context」のような不要な音が混入
- **原因**: シーンに保存されたプロンプトテキストが古い内容のままだった
- **解決**: プロンプトテキストを音声ファイルの内容に一致させる

### piper_phonemizeとの互換性
- **問題**: espeak_TextToPhonemes は句読点を出力しない
- **解決**: 元テキストの末尾句読点を手動でトークンに追加

---

## 5. リンク

- **GitHub**: https://github.com/ayutaz/uZipVoice
- **ONNX Models**: https://huggingface.co/ayousanz/uZipVoice-onnx
- **Original ZipVoice**: https://github.com/k2-fsa/ZipVoice

---

*最終更新: 2026-01-12*
