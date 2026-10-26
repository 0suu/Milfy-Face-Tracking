# Changelog

## 1.2.0 - 2026-10-26

- 既存BlendShareデータへ合成済み `EyeWideLeft/Right` を追加し、FT用形状を39から41へ拡張
- 見開きに合わせ、左右の瞳孔、黒目全体、星型を含むハイライトを連続縮小
- Jerry 7.0.5のEyeLid×EyeSquint合成を既存Shape Adapter内でも再利用し、FTオフ時の見開き解除を保証
- 既存の担当形状を各クリップで明示し、WD OffのDirect BlendTreeで前フレームの値が加算される問題を防止
- セットアップ済みMeshを複製して更新するメニューを追加（元Mesh・他形状・設定値を保持、Undo対応）
- 新しいNDMFプラグイン、追加Animatorレイヤー、独自補正形状、同期bit、依存バージョンの追加なし
- frameeyeoscの接続方法、移行手順、回帰テストを追加

## 1.1.0 - 2026-08-03

- `mouth_Λ = 70`、`mouth_narrow = 45` を標準の既定口として判定し、標準Milfyだけで口補償を選択できるよう変更
- 口補償を明示的に無効化した場合は、補償用アセットとModular Avatarコンポーネントを生成しないよう変更
- `Viseme` を優先する3状態の補償Controllerへ変更し、音声リップシンクとの競合を修正
- 1引数の `MilfyFtSetupService.Setup` を非推奨化し、口補償の有無を明示する2引数版を推奨
- Face TrackingのMerge Animator priorityを1000、口補償を900へ変更
- `Gaze Sync` を視線方向だけへ適用し、左右のまぶたとウィンクへ影響しないよう変更
- 不要な `Blink Sync` と旧まぶたProxy出力を削除し、Jerry標準の平滑化済みまぶた出力へ統一
- オプションの `Eye Smile Correction` と設定トグルを追加
- Expression MenuからJerry's TemplatesのVRCFT Debugメニューを開けるよう変更
- Milfy v1.5.0初期構成との合成時のExpression Parameters使用量を234 / 256 bitsへ更新

## 1.0.0 - 2026-07-28

- Milfy 専用の `Milfy_FT.prefab` を追加
- 元のGameObjectとFBXを変更せず複製側へ導入するセットアップウィンドウを追加
- Milfy用の39形状BlendShareデータを追加
- Jerryの平滑化済みProxy値から未使用6形状を駆動する補正レイヤーを追加
- FT DebugのBlendshape Syncを配布39形状に限定
- 未対応の舌方向・Roll用量子化パラメーターをLocal Only化し、クリーンMilfyで255/256 bitsに調整
- Jerry's Templates 7.0.5 の眼球回転用 OSCmooth 生成アニメーション24個を収録
- `FT/Debug` を Local Only に設定
