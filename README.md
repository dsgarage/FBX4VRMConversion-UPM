# FBX4VRM Conversion

FBX/PrefabをVRM形式に変換するUnity Editor拡張パッケージ。lilToonからMToonへの自動変換、表情・SpringBoneの自動セットアップに対応。

## UPMインストール

**Package Manager から直接インストール:**

```
https://github.com/dsgarage/FBX4VRMConversion.git
```

Unity Package Manager → `+` → `Add package from git URL...` に上記URLを入力

**manifest.json に直接追加:**

```json
{
  "dependencies": {
    "com.dsgarage.fbx4vrmconversion": "https://github.com/dsgarage/FBX4VRMConversion.git"
  }
}
```

> **Note**: 依存パッケージ（UniVRM-fork）は自動的にインストールされます。

---

## 機能

### マテリアル変換
- **lilToon → MToon 自動変換**: Base Color, Normal Map, Emission, Rim Light, Outline をマッピング
- **HDR値の自動クランプ**: glTF仕様（0-1）への自動修正と警告表示
- **非破壊処理**: オリジナルマテリアルは変更されません

### バリデーション
- **Humanoidボーン検証**: VRM必須15ボーン + 推奨9ボーンの検証
- **T-Pose検出**: 腕の角度ベースでポーズ確認
- **ボーン階層検証**: 親子関係の整合性チェック

### 表情・物理
- **BlendShape自動マッピング**: 17種類のVRM標準表情に自動対応
- **複数命名規則対応**: 英語・日本語・VRChat形式
- **SpringBone変換**: VRChat PhysBone / DynamicBone → VRM SpringBone

### UX
- **プリセット管理**: プラットフォーム別（VRChat, Cluster等）設定の保存・読込
- **クイック エクスポート**: ワンボタンでVRM出力
- **エクスポートレポート**: 変換結果の詳細表示とJSON保存

---

## 動作要件

| 項目 | バージョン |
|------|-----------|
| Unity | 2021.3 以上 |
| UniVRM | v0.130.1-f2 以上（自動インストール） |
| Render Pipeline | Built-in / URP |

---

## クイックスタート

### 1. パッケージのインストール

Package Manager → `+` → `Add package from git URL...`

```
https://github.com/dsgarage/FBX4VRMConversion.git
```

### 2. モデルの準備

1. FBXまたはPrefabをシーンに配置
2. **Humanoid**としてリグ設定されていることを確認
3. T-Poseであることを推奨

### 3. クイックエクスポート（推奨）

**方法A: メニューから**
1. Hierarchyでモデルを選択
2. `Tools > FBX4VRM > Quick Export` または `Ctrl+Shift+E`
3. プリセットを選択（オプション）
4. `Export VRM` をクリック

**方法B: 右クリックから**
1. Hierarchyでモデルを右クリック
2. `FBX4VRM > Quick Export VRM`

### 4. 詳細設定でエクスポート

詳細な設定が必要な場合：

1. `Tools > FBX4VRM > Export Window` を開く
2. Root Objectにモデルを設定
3. VRMバージョンを選択（0.x / 1.0）
4. 出力フォルダを指定
5. `Preview` で事前確認（推奨）
6. `Export` でVRM出力

---

## プロセッサパイプライン

変換は以下の順序で自動実行されます：

| 順序 | プロセッサ | 説明 |
|-----|-----------|------|
| 0 | RootValidation | Animator・メッシュの基本検証 |
| 5 | HumanoidValidation | Humanoidボーン・T-Pose検証 |
| 10 | LilToonDetect | lilToonマテリアル検出 |
| 20 | LilToonToMToon | lilToon → MToon変換 |
| 30 | GltfValueClamp | HDR値・範囲外値のクランプ |
| 40 | ExpressionsSetup | BlendShape → VRM Expression マッピング |
| 50 | SpringBoneConvert | PhysBone/DynamicBone → VRM SpringBone |

---

## ウィンドウ一覧

| メニュー | ショートカット | 説明 |
|---------|---------------|------|
| Tools > FBX4VRM > Export Window | - | フル機能エクスポートウィンドウ |
| Tools > FBX4VRM > Quick Export | Ctrl+Shift+E | ワンボタンエクスポート |
| 右クリック > FBX4VRM > Quick Export VRM | - | コンテキストメニューからエクスポート |
| 右クリック > FBX4VRM > Export with Settings... | - | 設定付きエクスポート |

---

## 対応シェーダー

### 入力（自動変換）
- lilToon（全バリアント: Cutout, Transparent, Outline等）

### 出力
- VRM 0.x: MToon
- VRM 1.0: MToon10

---

## プリセット

### 組み込みプリセット
- **Default**: 標準設定
- **VRChat**: VRChat向け最適化
- **Cluster**: Cluster向け最適化

### カスタムプリセット作成

1. Export Windowで設定を調整
2. `Save Preset` をクリック
3. 名前とタグを入力して保存

プリセットは `Assets/` 内に ScriptableObject として保存されます。

---

## トラブルシューティング

### エクスポートできない

- [ ] モデルがHumanoidとしてリグ設定されているか確認
- [ ] Animatorコンポーネントが付いているか確認
- [ ] SkinnedMeshRendererが存在するか確認

### マテリアルが正しく変換されない

- [ ] lilToonが正しくインストールされているか確認
- [ ] Export Previewで警告を確認
- [ ] HDR値が極端に高くないか確認（自動クランプされます）

### SpringBoneが動かない

- [ ] VRChat SDK / DynamicBoneがプロジェクトにあるか確認
- [ ] 元のPhysBone/DynamicBoneが正しく設定されているか確認
- [ ] Export Reportで変換結果を確認

---

## ライセンス

MIT License - 詳細は [LICENSE](LICENSE) を参照

---

## リンク

- [GitHub Repository](https://github.com/dsgarage/FBX4VRMConversion)
- [Changelog](CHANGELOG.md)
- [Issues](https://github.com/dsgarage/FBX4VRMConversion/issues)

---

## 依存パッケージ

このパッケージは以下のパッケージに依存しています（自動インストール）：

- [UniVRM-fork](https://github.com/dsgarage/UniVRM-fork) v0.130.1-f2
  - com.vrmc.gltf
  - com.vrmc.univrm
  - com.vrmc.vrm
