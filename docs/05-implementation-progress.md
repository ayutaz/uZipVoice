# 実装進捗

## 1. 概要

uZipVoiceプロジェクトの実装進捗を記録するドキュメントです。

### 進捗サマリー

| カテゴリ | 完了 | 残り | 進捗率 |
|---------|-----|------|--------|
| コアコンポーネント | 2 | 9 | 18% |
| テスト | 56 | - | 100% (実装済み分) |

---

## 2. 実装済みコンポーネント

### TokenMap（Tokenizer）

**ファイル**: `Assets/uZipVoice/Runtime/Tokenizer/TokenMap.cs`

音素からトークンIDへのマッピングを管理するクラス。

**機能**:
- tokens.txtファイルからのマッピング読み込み
- TextAssetからの読み込み
- 音素 ⇔ トークンID の双方向変換
- 特殊トークン（PAD, BOS, EOS, SPACE）のサポート

**テスト**: 24テストケース、全て成功

---

### EulerSolver（Inference）

**ファイル**: `Assets/uZipVoice/Runtime/Inference/EulerSolver.cs`

Flow MatchingのためのEuler ODE積分ソルバー。

**機能**:
- 非線形タイムステップ生成（t_shift パラメータ対応）
- 単一ステップ計算
- インプレース更新オプション
- パラメータ検証

**テスト**: 32テストケース、全て成功

---

## 3. 未実装コンポーネント

### 優先度: 高

| コンポーネント | 説明 | 依存関係 |
|--------------|------|---------|
| EspeakTokenizer | espeak-ngを使用したG2P変換 | espeak-ng DLL |
| TextEncoder | テキスト→条件ベクトル推論 | Inference Engine |
| FMDecoder | Flow Matchingデコーダ推論 | EulerSolver |

### 優先度: 中

| コンポーネント | 説明 | 依存関係 |
|--------------|------|---------|
| Vocos | Vocoder推論 | Inference Engine |
| ISTFTProcessor | ISTFT音声生成 | NWaves |
| FeatureExtractor | メル特徴抽出 | - |

### 優先度: 低

| コンポーネント | 説明 | 依存関係 |
|--------------|------|---------|
| ZipVoiceManager | メインAPI | 全コンポーネント |
| サンプル | デモシーン | ZipVoiceManager |

---

## 4. テスト実装状況

### Edit Mode Tests

| テストクラス | テスト数 | 状態 |
|------------|---------|------|
| TokenMapTests | 24 | ✅ 全成功 |
| EulerSolverTests | 32 | ✅ 全成功 |
| **合計** | **56** | **✅ 全成功** |

### 未実装テスト

| テストクラス | 予定テスト数 | 対象コンポーネント |
|------------|-------------|------------------|
| EspeakTokenizerTests | 12 | EspeakTokenizer |
| ISTFTProcessorTests | 10 | ISTFTProcessor |
| TextEncoderTests | 9 | TextEncoder |
| FMDecoderTests | 9 | FMDecoder |
| VocosTests | 8 | Vocos |
| FeatureExtractorTests | 7 | FeatureExtractor |
| IntegrationTests | 6 | 統合テスト |
| ZipVoiceManagerTests | 10 | ZipVoiceManager |

---

## 5. コミット履歴

| 日付 | コミット | 内容 |
|------|---------|------|
| 2026-01-11 | eff6dce | Add Microsoft.CodeAnalysis.CSharp via OpenUPM |
| 2026-01-11 | 2e7bf51 | Add TokenMap, EulerSolver with unit tests |
| 2026-01-11 | a20c8a0 | Add test specification document |
| 2026-01-11 | a8752f0 | Add technical documentation |
| 2026-01-11 | d931da4 | Add uZipVoice project structure and config files |
| 2026-01-11 | 5bcb4a5 | Add .gitattributes for consistent line endings |
| 2026-01-11 | 4dd2a8f | Initial commit: Unity project setup for uZipVoice |

---

## 6. 次のステップ

1. **EspeakTokenizer実装**
   - piper-unityを参考にP/Invokeラッパー作成
   - espeak-ng DLLの配置
   - espeak-ng-dataの配置

2. **TextEncoder実装**
   - Unity AI Inference Engine 2.3でONNXモデル読み込み
   - 入出力テンソル処理

3. **FMDecoder + Vocos実装**
   - EulerSolverを使用した積分ループ
   - ONNX推論パイプライン

4. **ISTFTProcessor実装**
   - NWavesライブラリ導入
   - STFT係数から波形への変換

---

## 7. 技術的メモ

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

### テスト実行コマンド

Unity Test Runnerまたはuloopメcp経由で実行可能。

---

*最終更新: 2026-01-11*
