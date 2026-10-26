# 1.2.0 目の見開き: 設計と検証

## 変更の理由と境界

1.1.xのBlendShareデータは39形状で、Jerry 7.0.5が駆動する`EyeWideLeft/Right`が存在しないため、まぶた入力が通常開眼を超えても見開きが表示されなかった。

1.2.0はNDMF Transforming / Modular Avatar後に、Milfy固有の`Milfy/GazeSync`を持つControllerだけを対象にする。`Body`のMeshを複製し、不足側だけ`eye_surprised_L/R`の全フレーム・weight・頂点・法線・接線をコピーする。標準の`pupul_small`を元頂点のlocal Xで左右に分離し、頂点・法線・接線を50%にした`MilfyEyeWidePupilLeft/Right`も追加する。最大見開きでネイティブ縮小の50%相当（100%では瞳孔がほぼ消える実描画を比較して決定）。さらに`eye_op_small_２`（全角２）を40%にした左右の`MilfyEyeWideIrisLeft/Right`を追加し、黒目と星型を含むハイライトの位置・大きさを一緒に縮める。虹彩だけを縮める形状では星が相対的に大きく浮くことを描画で確認し、ハイライトも含む形状を選んだ。元の39形状データは変えず、ビルド中の追加形状は計6個、FT用・補正用は計45形状。既存形状番号と既存のカスタム見開き形状データを保持する。元FBX・Meshアセットは変更しない。必要な元形状が欠ける場合や変位がない場合は明示的に失敗する。

見開きは平滑化済み`OSCm/Proxy/FT/v2/EyeLidLeft/Right`から、`.75=0%`、`1=100%`として左右独立に補間する。`EyeTrackingActive=0`では明示的に0を出力する。専用2層はpriority 1001でJerryのpriority 1000の後へ追加する。瞬き・視線・口・他の既存形状のcurveを変更しない。JerryのEyeLid×EyeSquintの2D合成を複製する実装ではなく、見開き・連動縮瞳・黒目とハイライトの縮小をEyeLidに比例して制御する。瞳孔縮小も同じ入力へ左右独立に連動し、通常開眼・FTオフで0へ戻す。実Meshに`EyeConstrict/EyeDilation`の両方がある場合だけ、`EyeDilationEnable`オンで瞳孔補正を0にして既存制御へ譲り、まぶたと黒目・ハイライトの縮小は維持する。標準Meshには両形状がなく、このトグルの保存値がオンでも縮瞳を止めない。実測瞳孔入力への新規対応ではなく、黒目全体の縮小による最終見た目の瞳孔サイズ変化は残る。カスタムAnimatorが同じ形状を制御する場合はlayer順を考慮する必要があり、Meshデータの保持とAnimatorの出力優先順位は別である。

OSC/Expression Parameters、同期bit、メニュー、public API、シリアライズ済みの39形状データ、依存バージョンは変更しない。旧セットアップにも適用され、パッケージ更新後の再ビルド・アップロードが必要。

## レビューで防いだ問題

- 設計レビューで、検証プロジェクト固有のSteam Frame笑い目制御が上限`1.25`を想定していることを発見。正しい上限`1` / 見開き区間`.25`へローカル修正した。これはパッケージPR外のコードであり、本PRには含めない。
- 実装レビューで、元Meshへの書き込み、不足側以外への不要な検証、カスタム形状の上書きを回避した。
- 生成後検証で、見開きからFTオフへ切り替えると値が残留する問題を再現。WD OffでゼロweightのDirect BlendTreeだけに解除を任せず、専用制御で明示的に0へ戻す実装と回帰テストを追加した。
- 低いpriorityのbaselineだけでは単独の回帰ケースを解決できないことも検証し、最終実装は上記の専用2層にした。

## 実施した検証（2026-10-26）

環境: Unity 2022.3.22f1、VRChat Avatars SDK 3.10.4、MA 1.18.1、NDMF 1.14.3、BlendShare 1.0.3、Jerry 7.0.5、AAO 1.9.16。

- Unityコンパイル成功。変更箇所のエラーなし。
- [17件のNUnit fixture検証](eye-wide-fixture-checks.txt): 両側追加、元Mesh・既存形状・geometryの保持、全フレーム/法線/接線、左右片側だけの追加、冪等性、元形状欠落、変位なし、WD Offの下位writerが100でもFTオン→オフを繰り返すと見開きと縮瞳が解除されること。縮瞳の左右分離・半量変位、黒目とハイライトの左右分離・40%変位・全フレーム保持、追跡中に瞳孔制御トグルを0→1→0と切り替える所有権切替、元形状欠落時の保護も検証。
- 未保存Sceneを維持するため、Unity Test Runnerは実行していない。uLoopの`run-tests --unsaved-changes fail`が未保存Sceneを検出して停止した後、EditModeで各fixtureのSetUp/Test/TearDownを直接実行した。Test Runnerによる成功とは区別する。
- [生成Animatorの23ケース](eye-wide-animator-validation.txt): 0/25/50/75/100%、左右別の見開き、左右ウインク、再開眼、パー中の見開き/半見開き/閉眼、Quest Pro、FTオフ、再有効化、追跡停止/再開、見開きと目細めの同時入力、標準Meshで保存済み瞳孔トグルがオン/オフの場合、縮瞳後の通常開眼。全ケースで左右の瞳孔・黒目補正量も確認。実際のNDMF生成ControllerをUnity Playablesで評価し、VRChat SDKのParameter Driver / Layer ControlだけをEditMode上で模擬した。
- 現在のセットアップ済みアバターは生成後216/256 bits。見開き用Expression Parameterは追加しない。
- [706個の既存renderer weight](eye-wide-weight-validation.txt)がMesh再割当後も保持されることを確認。
- [AAO Trace And Optimize](eye-wide-aao-validation.txt)を検証用複製で有効化し、リネームされた見開き・縮瞳・黒目/ハイライトの6制御とAnimator bindingの一致を確認。同じカーブの瞳孔/虹彩はAAOで左右それぞれ1形状へ統合される。最適化後も216/256 bits。
- [新規セットアップと非FTアバター](eye-wide-new-setup-validation.txt): 新規セットアップにも2形状が追加され、Milfy FTを導入していないアバターは対象外になることを確認。
- Unityで生成後Meshを描画し、通常開眼・50%・100%・左右別の見開き・ウインクを目視確認。

[実Meshの縮瞳検証](eye-wide-pupil-geometry-validation.txt)では、両側補正の最大値が標準`pupul_small=50`と一致することを確認（最大頂点差`1.193256E-07`）。元の706個のweightとMesh参照も保持。

[黒目・ハイライトと瞳孔の合成検証](eye-wide-iris-geometry-validation.txt)では、最終補正が標準`pupul_small=50 + eye_op_small_２=40`と一致することを確認（最大頂点差`1.19282E-07`）。星型ハイライトが虹彩と一緒に縮まり、位置も内側へ追従することを左右それぞれ描画で確認した。

![黒目と星型ハイライトの追従比較](eye-wide-iris-highlight-before-after.png)

見開き0/50/100%に対し、瞳孔専用縮小は標準形状0/25/50%相当、黒目とハイライトの縮小は標準形状0/20/40%相当に連動する。

![生成後Meshの実描画比較](eye-wide-comparison.png)

## 未実施

VRChat Build & Test、実機OSC入力、リモート表示、Android実機での動作確認は未実施。コンパイル・NDMF生成・EditMode描画の成功から実機成功を推定しない。実機ではframeeyeoscのLiveLinkまたはFrameEyeOSCモジュール経由で確認する（ETVRでは見開きが失われる）。

PR作成段階ではリリースWorkflow、VPM配布、アバターアップロードは実行しない。
