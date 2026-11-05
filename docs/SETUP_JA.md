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
