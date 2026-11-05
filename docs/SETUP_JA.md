# Scene編集ガイド（2026-11-05）

## 今回変わった点

従来はゲーム開始時に `BoardView.Build` で地形・駅・線路のPrimitiveを生成していました。この仕組みは廃止し、**GitHubにコミットされた `Assets/Scenes/Main.unity` の静的オブジェクトが盤面の正本**になりました。

Sceneには駅マス131個、線路133本、平面型駅名札、地形・屋根・木、列車4台、サイコロを保存済みです。`SceneRailBoard.Export()` はScene内の `SceneRailNode` と `SceneRailSegment` を読んで経路グラフを作ります。通常のPlay中に背景やレールを生成・削除しません。

## 起動とカメラ

1. Unity HubでUnity 6.3を使いルートフォルダを開く。
2. `Assets/Scenes/Main.unity` をダブルクリック。
3. 盤面をSceneビューで確認。Hierarchyの `03_STATIONS - DRAG ON GRID` を展開すると各駅をクリックできます。
4. PlayするとCPUとのローカル対戦。カメラは列車のそばを**約6.1のOrthographic Size**で追従。表示は周辺駅だけに絞ります。

| 操作 | 動作 |
| --- | --- |
| Space / 下部ボタン | 3Dサイコロの回転・跳ね・着地 |
| 矢印 / WASD / 下部ボタン | 十字移動、移動可能な方向のみ |
| M / FULL MAP | 別画面で全体の路線を表示 |
| マウスホイール | カメラズーム |
| CARD | 手札を開閉 |
| ONLINE | サーバーURL・ルーム設定 |

## Sceneで駅を移動する

1. Hierarchyの `03_STATIONS - DRAG ON GRID` → `STOP 006 / Tokyo` などを選択。
2. Sceneビューの移動ギズモ（Wキー）で位置変更。**1マス = 2.2 Unityワールド単位**、X/Z軸方向に配置してください。
3. 駅オブジェクトの `SceneRailNode` Inspectorで駅名、マス種別（Property, Cardなど）、物件価格、収益、海上マスを編集。
4. 移動後は `Rail Trip > 08 Snap Station Roots to Grid`、`Rail Trip > 05 Refresh Saved Rail Geometry`、Scene保存（Ctrl/Cmd+S）。
5. `Rail Trip > 03 Validate Scene Map` で断線・重複・接続違反がないか確認。経路は**直交する隣接マス**のみに張れます。

駅の下にある `FLAT NAMEPLATE` を選択すると、白いプレートと平面テキストを移動できます。画角に応じて手前/奥の札位置を調整してください。Play中は遠い駅札を非表示にします。

## 駅・線路を追加または削除する

- `Rail Trip > 07 Add Station by Duplicating Selected`：選択した駅の隣の空きマスへ、コピーと接続線路を追加。
- `Rail Trip > 06 Link Selected Stations`：Hierarchyで隣接する駅のルート2個をCtrl/Cmd選択して接続。
- 接続解除：`02_TRACKS` にある該当 `TRACK 000-001` を削除。
- 駅削除後：該当する線路も削除し、`Rail Trip > 09 Reindex Station IDs After Deletions` でIDを連番にする。
- 線路を延長するとき：隣接マスを追加し、線路を接続。`SceneRailSegment.from / to` をInspectorで差し替えたらRefreshしてください。

## 建物と地形を編集する

- `01_LAND_AND_WATER`：地形Cubeと水面。必要に応じて複製、削除、スケール変更ができます。Sceneから動かした地形はPlay開始で再配置されません。
- `04_TREES_AND_BUILDINGS`：家や木のパーツがCubeの子として置かれています。Transform、素材、スケールを自由に変更できます。
- `Assets/Art/Materials`：各色のMaterialアセットが保存されています。色を変更すると該当する全モデルに反映されます。

## カメラと列車

- `FOLLOW CAMERA - EDIT CAMERA`：Orthographic Size 6.1。Play中は `BoardView` が列車の移動を追いかけ、少し進行方向前方を映します。
- `05_TRAINS_DICE_AND_MARKERS`：列車モデル4台、目的地旗、貧乏神、3DサイコロのモデルがすべてSceneに保存されています。Playでは位置・向き・Active状態のみ制御します。
- UIをさらに変えるときは `Assets/Scripts/GameHud.cs`、カメラの追従方法は `Assets/Scripts/BoardView.cs`。

## データの保存・バックアップ

**SceneのTransform・Inspector編集がゲームの正本**です。Asset側に同期させたいときのみ `Rail Trip > 04 Save Scene Layout to Map Asset` を実行してください。`ActiveMap.asset` は再構築用スナップショットであり、普段のPlayで優先されません。

`Rail Trip > 02 Rebuild Editable Scene from Map Asset` を押すとSceneの建物・駅配置がすべて置き換わります。Gitで変更をcommitするかSceneを複製した後でのみ使用してください。

日本語駅名はWindowsなどで日本語フォントが利用可能なとき自動適用されます。WebGLの場合は適切なライセンスの日本語フォントを自分で `Assets/Resources/Fonts/GameUIFont` に追加しない限り、英語ラベルが使われます。フォント自体はGitには含めていません。

## WebGL・オンライン

オンラインサーバーはCloudflare Workers + Durable Objectsです。`cd Backend && npm install && npm test && npx wrangler login && npm run deploy` を実行し、Worker URLをONLINE設定に入力。最大4人・6桁コードの部屋に参加できます。

WebGL Build Supportを追加してUnityメニュー `Rail Trip > Build WebGL` を実行してください。実際の公開は未実施です。現在はHTTPポーリング方式で状態を共有します。

**他の参加者とSceneの配置が異なるとオンライン対戦で表示が一致しません。** 全員が同じコミット・Scene版を利用してください。

## テストと制限

- 自動テスト：C#構文、オンラインルール、Scene YAMLのファイルID参照、SceneRailNode/Segment、経路接続、静的ビューモデル構造を検査。
- 未検証：Unity 6.3エディタでの実機インポート、WebGLレンダリング、オンラインデプロイ。
- 既存商用作品の素材は含めず、シンプルな3Dモデルと独自デザインで雰囲気を表現。
