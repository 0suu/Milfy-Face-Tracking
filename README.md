# Milfy Face Tracking

Milfy v1.5.0 向けの VRChat フェイストラッキング設定パッケージです。

[VPM パッケージ一覧](https://suu31.net/vpm/)

## 前提

- 未改変の Milfy v1.5.0
- Unity 2022.3
- VRChat SDK Avatars
- Modular Avatar
- BlendShare
- Jerry's Templates 7.0.5

依存パッケージは VCC で導入してください。

## 導入

1. VCC に配布ページで案内する VPM リポジトリ URL を追加します。
2. VCC から **Milfy Face Tracking** をプロジェクトへ追加します。
3. Unity 上部メニューから `Tools/suu_MifyFT/setup` を開きます。
4. Hierarchy の Milfy をウィンドウへドラッグ＆ドロップします。
5. **複製を作成してセットアップ** を押します。

元の GameObject と FBX は変更しません。`_FT` が付いた複製側だけに、FT 用の Mesh と設定を追加します。

## 注意

- Milfy v1.5.0 の FBX が改変されていないことを確認してください。
- Milfy 本体のモデルデータ・テクスチャはこのパッケージに含みません。
- 口開き時に既定口を相殺する設定は、セットアップ画面で選択できます。

## ライセンス

このパッケージは MIT License です。含まれる第三者ライセンスは `Packages/jp.suu.milfy-ft/THIRD_PARTY_NOTICES.md` を確認してください。
