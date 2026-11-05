# Rail Trip 3D — Unity 編集可能な鉄道すごろく

Unity 6.3（Built-in Render Pipeline）用のオリジナル鉄道ボードゲームです。ローカルCPU対戦、Cloudflare Workersを利用するWebGL対応オンライン対戦、物件・カード・貧乏神・最短経路表示を備えています。

## 重要：マップは Scene 内に実物として保存済み

**[Assets/Scenes/Main.unity](Assets/Scenes/Main.unity)** に **131個の駅・線路マス / 133本の線路オブジェクト**、地形、建物、木、車両4台、3Dサイコロ、目的地マーカーを配置済みです。モデルはHierarchyに存在し、**Play時に駅・線路・地形を生成しません**。

- [Scene編集と起動の日本語ガイド](docs/SETUP_JA.md)
- [Unity用スクリプト](Assets/Scripts)
- [Editor用Scene作成・編集ツール](Assets/Editor/EditableSceneBaker.cs)
- [オンラインサーバー](Backend/src/worker.js)

### 起動する

1. `git clone https://github.com/yamato0816/momotetsu_unity.git` または既存フォルダで `git pull`。
2. Unity Hubでリポジトリのルートを **Unity 6.3** で開く。
3. **`Assets/Scenes/Main.unity`** を開き、Hierarchyの駅・線路・土地を確認。
4. PlayでYOU対CPU2人のゲーム開始。Spaceでサイコロ、矢印/WASDで十字移動、Mで全体マップ。
5. **`Rail Trip > Edit Map`**（Ctrl/Cmd+Shift+M）から編集可能なSceneを開けます。

### Unity上で直接編集する

| Hierarchy | 編集できるもの |
| --- | --- |
| `01_LAND_AND_WATER` | 海・島・地形のCube |
| `02_TRACKS` | 線路1本ずつ（白い路盤・黒い枕木・銀色のレール・最短経路表示） |
| `03_STATIONS` | 駅を選択してTransformを移動。`SceneRailNode` Inspectorから駅名/価格/収益/種別を編集 |
| `FLAT NAMEPLATE` | 駅マスの上下に貼り付く名前札。位置・文字を編集 |
| `04_TREES_AND_BUILDINGS` | 建物・屋根・樹木をSceneで直接移動・変更 |
| `05_TRAINS_DICE_AND_MARKERS` | 列車4台、貧乏神、目的地旗、サイコロ |
| `FOLLOW CAMERA` | 近距離斜め見下ろしカメラ（初期Orthographic Size 6.1） |

マップはSceneから自動で読まれます。駅オブジェクトを移動したら `Rail Trip > 08 Snap Station Roots to Grid` → `Rail Trip > 05 Refresh Saved Rail Geometry` → Scene保存（Ctrl/Cmd+S）。線路接続を増やすには隣接駅2つを選択して `Rail Trip > 06 Link Selected Stations`。削除後はIDの振り直しが必要になることがあります。

**重要:** `Rail Trip > 02 Rebuild Editable Scene from Map Asset` は現在のSceneを丸ごと置き換えます。普段の編集では押さないでください。

### オンラインとWebGL

`cd Backend && npm install && npm test && npx wrangler login && npm run deploy` を実行し、表示されたHTTPS Workers URLをゲームのONLINE設定に入力。部屋コードで最大4人参加できます。WebGL Build SupportをUnity Hubに入れて `Rail Trip > Build WebGL` を実行。

**オンラインでは全参加者が同じSceneの駅配置・線路データを持つ必要があります。**違うSceneだとマップ不一致エラーを表示します。

### 検証範囲

GitHub Actionsでサーバーのルールテスト、C#構文検査、Sceneに保存された駅・線路・モデル参照・経路接続の検査を実行します。Unity Editorでの実際のインポート・コンパイル・描画、およびWebGLビルド・公開はこのチャットからは未実施です。問題があればUnity Consoleを確認してください。

第三者の商用ゲームの素材をそのまま配布せず、オリジナルの簡易モデルとゲームロジックを利用しています。
