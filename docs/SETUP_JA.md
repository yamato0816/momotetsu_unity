# Rail Trip 3D – セットアップ & 編集ガイド

## 最短で遊ぶ
1. Git リポジトリをクローンして、Unity Hub からこのフォルダをプロジェクトとして追加。
2. **Unity 6.3 系**で開く。Built-in Render Pipeline を使用（追加のプラグイン不要）。
3. 初回コンパイル後、エディタが \`Assets/Scenes/Main.unity\` と \`Assets/Resources/Maps/ActiveMap.asset\` を自動生成。シーンを開いて **Play**。
4. 最初は YOU と CPU 2人でローカル対戦。Space でサイコロ、矢印/WASD で線路を1マスずつ移動。目的地は上部、最短経路は金色。
5. 物件で購入/スキップを選択。カードは上限3枚。1/2/3キーで使用。貧乏神は取り憑いているプレイヤーからターンごとにお金を奪います。
6. ミニマップは M、カメラズームはホイール。18ターン/人で終了し資産額で勝敗。

> Unity Editor で再生・WebGLビルドの実機検証はまだ行っていません。エディタの Console に問題があれば修正が必要です。

## 地図の編集
- \`Rail Trip > Edit Map\` を開く。アクティブマップは \`Assets/Resources/Maps/ActiveMap.asset\`。
- マスをクリックして選択し、**Station name / Tile kind / Property price / Income** を編集。
- **Shift+クリック**で空セルにマスを追加。\`CONNECT FROM THIS NODE\` を押して隣のマスをクリックすると線路を接続。
- 斜め・飛び越しレールは作れません。間に線路マスを置いてください。
- 物件以外に「通常駅、カード、プラス、マイナス」などを設定できます。必ず全ノードが線路でつながるようにしてください。
- \`Validate\` で孤立ノード等を確認。編集結果は Asset として保存され、次回 Play 時から使用されます。
- \`Rail Trip > Export Active Map JSON\` で \`MapExports/ActiveMap.json\` を出力できます。
- サンプルマップに戻す操作は \`Rail Trip > Reset to Sample Japan Map\`（上書き確認あり）。
- 路線の外観や駒・カードのスプライトは \`Assets/Scripts/BoardView.cs\` と \`PixelArt.cs\` で変更できます。

## オンライン対戦 (WebGL / Windows / macOS)
ブラウザ版のマッチホストにも対応するため、Unity 自身がホスト/サーバーになるのではなく Cloudflare Workers + SQLite-backed Durable Objects を使っています。ゲーム進行はサーバー側で判定する方式で、全クライアントが同じ状態を共有します。ネットワーク上のリアルタイム移動アニメーションは各クライアントで補間します。

1. Cloudflare アカウントで Workers を有効化（デプロイはユーザー自身の Cloudflare アカウントで実行）。
2. Node.js が入っている端末で次のコマンドを実行：
   \`\`\`bash
   cd Backend
   npm install
   npm test
   npx wrangler login
   npm run deploy
   \`\`\`
3. デプロイ後の HTTPS URL（例：\`https://railtrip-online.<アカウント>.workers.dev\`）をコピー。
4. Unity ゲームの **ONLINE** → Worker URL を入力 → **SAVE SERVER ADDRESS**。
5. 1人目は **CREATE ROOM** で6桁の部屋コードを発行。2〜4人目は同じ URL とコードで **JOIN**。
6. ホストが **START GAME** を押すと開始。ゲームはターン制で、ボタン操作と状態は Workers 経由で共有されます。
7. ゲーム中は 1.4 秒ごとに状態をポーリング。無料枠のリクエスト上限・継続性は Cloudflare 側で確認してください。

**WebGL ビルド:** Unity Hub で WebGL Build Support をインストールし、エディタの \`Rail Trip > Build WebGL\`。出力先は \`Builds/WebGL/\`。HTTPS 静的ホスティング（Cloudflare Pages など）へ配置してください。ローカルの \`file://\` からの起動は推奨しません。クラウドへの公開はこのリポジトリのコード追加だけでは自動実行されません。

## 主なコード
- \`RailMap.cs\`：グリッド・駅・線路データと BFS 最短経路
- \`SampleMap.cs\`：日本の主要都市をモデルにしたサンプル路線
- \`GameRules.cs\`：オフラインのターン制ゲームロジック
- \`BoardView.cs\`：3D 盤面・線路・駅・列車・目的地・ゴースト描画
- \`PixelArt.cs\`：2D スプライト生成（列車・カード・貧乏神・ゴール旗）
- \`GameHud.cs\`：画面 UI / 簡易地図 / 十字操作
- \`OnlineClient.cs\`：UnityWebRequest を使用した WebGL 対応通信
- \`Backend/src/worker.js\`：ゲームサーバー・対戦ルーム・状態保存・権限チェック
- \`Assets/Editor/RailTripEditor.cs\`：編集ウィンドウ・自動シーン生成・WebGL 出力

## 制限
- サンプル日本地図は鉄道路線をイメージしやすくした**グリッド上の概略図**です。正確な地理・駅数・実在路線とは異なります。
- 物件、収益、カード3種、ゴール、貧乏神、AI、ターン制は MVP の独自ルールです。経営・増資、数十種類のカード、航路や全都道府県規模の再現は未実装です。
- 保存/再接続用トークンの永続化、ルームの自動破棄、荒らし対策、本番レベルの不正・負荷対策は未実装。Worker の無料枠やリクエスト課金に留意してください。
- Runtime UI は WebGL の標準フォントを優先して英語表記。日本語表示へ切り替えるには日本語グリフを含むフォントを別途プロジェクトに同梱・適用してください。
- UI は既存商業ゲームを模写・複製したものではなく、鉄道すごろくのプレイ感を参考にした独自デザインです。

## 開発時チェック
- Unity メニューの \`Rail Trip > Run Map & Rules Sanity Test\`。
- \`cd Backend && npm test\` でサーバーの基本的な権限とルールを検証。

## 2026年11月：ジオラマ版への更新

参考画像の**近距離・斜め見下ろし画角**に合わせ、従来の広域カメラと常時表示ミニマップを廃止しました。現在地周辺を追尾し、必要なときだけ M で大きな全体マップを開きます。下部はサイコロ/方向操作を中心に、カード・成績表・設定は必要なときだけ表示します。

**駅と線路の編集**：Unity メニューの `Rail Trip > Edit Map` で駅をクリックし、次の変更ができます。
- 駅名・マス種別・物件価格・収益の編集
- Shift+クリックで線路マスの追加、不要なマスの削除
- 「TOGGLE RAIL FROM THIS NODE」で線路の追加・削除、または接続一覧から「REMOVE TRACK」
- 「Water / bridge square」で該当マスの陸地パッチを非表示にする。島の間の海上ルートに便利
- 「Validate」で孤立したマスなどをチェック

**注意**：すでに古い `ActiveMap.asset` がUnityで自動生成されている場合、更新後もそのファイルは上書きされません。最新版の島ルートの見本を見たいときは、必要ならバックアップを取り `Rail Trip > Reset to Sample Japan Map` を実行してください。自分で編集した地図を維持するならリセット不要です。

### 3D サイコロの挙動

- `Assets/Scripts/DiceRollView.cs` がシーン内に6面の立体サイコロを生成します。
- `GameRules.cs` と `Backend/src/worker.js` が実際の出目（1～6）とカードによる追加移動数を管理。見た目はその確定した出目に合わせます。
- サイコロは回転・ジャンプ・小さな着地・出目表示の順に動きます。演出中は手動移動・CPU行動を待機させます。
- オンラインはターン制の同期なので、通信状況によって他のプレイヤーのアニメーションが省略される場合があります。

### 日本語フォントとWebGL

日本語HUDが利用可能な日本語フォントを自動検出した場合は日本語表示になります。Windows Editorでは Yu Gothic / Meiryo などを、macOSでは存在する互換フォントを探します。WebGLのブラウザはローカルOSフォントを直接使えないため、ゲーム内に日本語フォントを入れる必要があります。

WebGLで日本語を表示するには、ライセンス上再配布可能な日本語フォントを**自分で用意して**Unityの `Assets/Resources/Fonts/GameUIFont` にFontアセットとして配置してください。フォントファイルはこのGitHubリポジトリに含めていません。フォントが無い環境では英語表示です。

### 描画品質について

モデルはUnityの標準Primitiveから自動生成したものです。既存ゲームの細かな建物モデル・山・海面シェーダー・車両アニメーションとは異なります。自分の3Dモデルや2D素材に差し替えるには `BoardView.cs` の `Part` / `CreateTrain` / `DecorateStation` を編集してください。現状のシェーダーとPrefab構成は軽量プロトタイプ向けです。

## 近距離カメラ・レール長さ・平面駅名（最新版）

- 車両追従カメラは初期 `orthographicSize = 6.1`。移動中の列車そのものを追跡し、向いている方向を少し先読みします。ホイールで拡大・縮小（5.3～9.3）。
- 駅名札は**盤面に水平に貼り付く白いプレート**になりました。北側と南側の線路接続・他駅札を考慮して位置を選びます。遠い札はプレートごと非表示にします。
- すべてのレール・駅・地形のワールド間隔はマップデータの `tileSpacing` を共有し、初期値は **2.2 Unity units**。線路の長さ・枕木配置・地形のサイズも連動します。
- `Rail Trip > Edit Map` → **Station / rail spacing** スライダーでマス間隔を 1.8～3.4 に変更可能。変更前のカスタムマップも保持され、値が未設定なら2.2を使います。
- 金色の最短経路は常時光らせず、下部の **最短ルート / ROUTE** ボタンで切り替える方式に変更しました。簡易地図側の最短経路は引き続き表示します。
- 建物・樹木を減らして視界の混雑を抑えています。アセットはコード生成の簡易版で、商用ゲームの画像・モデルの複製ではありません。
