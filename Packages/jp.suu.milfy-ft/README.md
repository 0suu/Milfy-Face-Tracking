# Milfy Face Tracking

未改変の Milfy を VRCFaceTracking に対応させる、Milfy 専用の Modular Avatar プレハブです。

## 対応条件

- Unity 2022.3
- VRChat Avatars SDK 3.8.2 以上、4.0.0 未満
- Modular Avatar 1.13.4 以上、2.0.0 未満
- BlendShare 1.0.3 以上、2.0.0 未満
- VRCFT - Jerry's Templates **7.0.5**
- 未改変の Milfy
  - 顔の SkinnedMeshRenderer 名が `Body`
  - 元の FBX と `Body` の頂点構成が変更されていないこと

Jerry's Templates は互換性を保証するため 7.0.5 に固定しています。VRCFaceTracking 本体は PC 側で別途セットアップしてください。

現在の動作確認環境は VRChat Avatars SDK 3.10.4、Modular Avatar 1.18.1、BlendShare 1.0.3、Jerry's Templates 7.0.5 です。依存範囲内の全バージョンを個別に検証したものではありません。

## 導入方法

1. VCC に [BlendShare のVPMリポジトリ](https://Tr1turbo.github.io/BlendShare/index.json)を追加します。
2. VCC からこのパッケージをプロジェクトへ追加します。BlendShare と Jerry's Templates 7.0.5 も依存関係として導入されます。
3. Unity上部メニューの `Tools/suu_MifyFT/setup` を開きます。
4. Hierarchy上の未セットアップMilfyをウィンドウへドラッグ＆ドロップします。
5. `複製を作成してセットアップ` を押します。
6. `_FT` が付いた複製側をNDMF Previewまたはアバタービルドで確認します。

元のGameObjectとFBXは変更されません。Hierarchy上に `_FT` 付きの複製を作り、`Assets/suu_MilfyFT/Generated` にFT用Meshアセットを生成して複製側の `Body` だけへ割り当てます。複製側には `Milfy_FT.prefab` も自動で追加されます。

セットアップ画面の `口を開いたときにMilfyの既定口を弱める` は、標準の既定口（`mouth_Λ = 70`、`mouth_narrow = 45`）を検出した場合だけ使用でき、初期状態で有効になります。不要な場合はオフにできます。既定の `mouth_*` がない場合やカスタム口を検出した場合は、音声リップシンクとの正しい併用を保証できないため使用できません。

有効時は、非ゼロの `mouth_*` BlendShapeを取得し、`wide` と `narrow` を除いてJawOpenに比例して0へ補間します。`Visemes Enabled` がオンの音声リップシンク中は `Viseme` を優先して既定口を0にするため、Face Tracking側の補償で通常のリップシンクが上書きされません。無効時は補償用Controller、AnimationClip、Modular Avatarコンポーネントを生成しません。新しいExpression Parameterや同期bitも追加しません。

手動導入する場合も、BlendShareの `Create Meshes` で別Meshアセットを生成し、複製したMilfyへ割り当ててください。`Apply BlendShapes` は元のFBXを直接更新するため、原本を残す運用では使用しないでください。

`OSCmooth` を別途導入する必要はありません。眼球回転用に生成済みの24アニメーションをこのパッケージへ収録しています。

セットアップウィンドウは、未改変Milfyの `Body` を検証してから、配布BlendShareデータの41個のFT用BlendShapeを持つ別Meshアセットを生成します。元FBXと頂点構成が違う場合は処理を開始しません。

## 目の見開き（1.2.0以降）

従来の39形状に `EyeWideLeft` / `EyeWideRight` を追加しました。左右それぞれの見開き、瞳孔縮小、黒目全体と星型を含むハイライトの縮小・位置調整を、この2形状へ合成済みです。通常のセットアップ後からInspectorで確認でき、NDMFでMeshや独自の補正形状を追加生成する処理はありません。

最大見開きの合成内容は、Milfy標準の `eye_surprised_L/R` と、その側の `pupul_small` の50%相当、`eye_op_small_２`（末尾は全角２）の40%相当です。元の形状を直接固定せず、追加したFT形状で動かします。

Jerry's Templates 7.0.5のまぶた入力を使用します。目細め入力が0の場合、`FT/v2/EyeLidLeft/Right` が `0.75` で通常開眼、`1.0` で最大見開きとなり、間の値にも連続して追従します。EyeLid×EyeSquintの合成もJerryの既存2Dツリーに合わせています。

追跡停止・FTオフ時は、既存のShape Adapter内の分岐で `EyeWideLeft/Right` を明示的に0へ戻します。新しいAnimatorレイヤー、OSCパラメーター、同期bitは追加しません。

これは見開きに連動する表現で、実測瞳孔入力への新規対応ではありません。縮瞳はEyeWideに焼き込まれているため、`Eye Dilation Tracking` では瞳孔補正だけを切り離せません。カスタムの `EyeConstrict/EyeDilation` との加算は個別調整が必要です。

### 1.1.xからの更新

パッケージ更新だけでは、以前作った39形状のMeshは変わりません。次の操作で更新してください。

1. Hierarchy上のセットアップ済みアバタールートを選択します。
2. `Tools/suu_MifyFT/Update selected FT mesh to 1.2.0` を実行します。
3. `Body` に新しいMeshが割り当てられたことを確認し、Sceneを保存して再ビルド・アップロードします。

現在のMeshを複製し、配布データの `EyeWideLeft/Right` を追加または置換します。元Mesh、その他の形状、rendererの設定値を保持します。更新前からEyeWideがある場合も新しい合成形状へ更新されます。Sceneは自動保存せず、Undoで割当を戻せます。生成したMeshアセットはUndo後も残ります。

Prefab ModeとPreview Sceneは更新対象外です。試用版の `MilfyEyeWidePupil*` / `MilfyEyeWideIris*` が非ゼロの場合は、二重変形を避けるため更新を止めます。0へ戻してから更新してください。これらの形状データは自動削除しません。

新規導入では通常のsetupを実行すれば41形状を生成します。手動運用では従来どおりBlendShareの `Create Meshes` を使用できます。

[frameeyeosc](https://github.com/sasaken1102r/frameeyeosc) はVRCFaceTrackingの **LiveLink** または **FrameEyeOSC** モジュール経由を使用してください。ETVRモードでは見開きが伝わりません。このパッケージはJerryのビットパラメーターを使用するため、VRChatへの直接送信を標準の接続方法として扱いません。SteamOS 0.4.3で見開きを取得するには、frameeyeosc側のeye camerasも必要です。

## 追加6形状

Jerry's Templates 7.0.5が標準では直接出力しない次の形状は、`Milfy_FT_ShapeAdapter.controller` でJerryの平滑化済みProxy値から駆動します。

- `BrowInnerUpLeft`
- `BrowInnerUpRight`
- `MouthSadLeft`
- `MouthSadRight`
- `MouthLowerDownLeft`
- `MouthLowerDownRight`

Jerry 7.0.5には左右別の `MouthLowerDown` 入力がないため、`MouthLowerDownLeft` と `MouthLowerDownRight` は同じ値で動きます。左右を独立させるにはFT構成全体の変更が必要です。

`Milfy FT Shape Adapter` レイヤーが書き込むのは上記6形状と `EyeWideLeft/Right` の計8形状です。Milfyに元からある `BrowInnerUp` などのBlendShapeは変更・固定しません。既存表情とFT表情の競合や合成は、このパッケージより上位の表情制御レイヤーで調整してください。

## 設定メニュー

- `Gaze Sync`: 左右の視線方向だけを同期します。左右のまぶたは同期しないため、オンのままウィンクできます。
- `Eye Smile Correction`: 笑顔と閉眼度に応じて `EyeClosedSquintCorrectiveLeft` / `Right` を補正します。初期状態はオフです。Jerry側も同じBlendShapeを使用するため、有効時はJerryの補正を上書きします。使用する場合は笑顔・目細め・瞬きの組み合わせを確認してください。
- `Debug`: Jerry's Templates 7.0.5のVRCFT Debugメニューを開きます。

まぶたを左右同期する `Blink Sync` はありません。Jerry標準の平滑化済み左右まぶた入力を独立したまま使用します。

## パラメーター容量

`FT/Debug` と、Milfy FTの41形状に存在しない次の入力の量子化パラメーターは Local Only に設定しています。

- 瞳孔の拡縮
- Lip Suck Upper / Lower
- Mouth Upper Up Left / Right
- Mouth Stretch Left / Right
- Mouth Raiser Upper
- Mouth Press
- Mouth Tightener Left / Right
- 舌の左右・上下・Roll

これらはJerry's Templates内で、Milfy FTの生成Meshに存在しないBlendShapeだけを駆動する入力です。対応する41形状の同期は維持しています。

Milfy v1.5.0の初期Expression Parameters（99 bits）と合成した場合は **234 / 256 bits** です。対応形状の `TongueOut` は別パラメーターのためリモート同期を維持します。

## 注意事項

- `Body` の名前や階層、Unified Expressions の BlendShape 名を変更した Milfy は対象外です。
- `Apply BlendShapes` は元のMilfy FBXを更新するため、このパッケージの標準導入では使用しません。
- Jerry's Templates のバージョンを手動で変更すると、アニメーションやパラメーター構成が一致しなくなる可能性があります。
- 眼球回転用アニメーションは Jerry's Templates 7.0.5 のコントローラーから GUID 参照されます。`.meta` を削除・再生成しないでください。

## クレジット

Face tracking blendshapes are animated by [Adjerry91’s Face Tracking Templates](https://github.com/ADJERRY91/VRCFACETRACKING-TEMPLATES).

BlendShapeの追加には [Triturbo/BlendShare](https://github.com/Tr1turbo/BlendShare) を使用します。

第三者制作物については [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。

## ライセンス

Milfy Face Tracking は MIT License で提供します。詳細は [LICENSE.md](LICENSE.md) を参照してください。
