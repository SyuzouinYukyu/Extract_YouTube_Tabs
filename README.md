# 🔐 プライバシーと透明性 / 📢 ソースコード公開について

🛡️ 本ソフトウェアは、ユーザーのプライバシーを尊重し、機能上不要な情報収集を行わないことを基本方針としています。

📢 本リポジトリは、**ソフトウェアの透明性確保、実装内容の確認、および第三者による安全性検証を可能にすること**を目的として、ソースコードを公開しています。

📡 **アプリケーション本体には、外部ネットワークへ接続する処理を実装していません。** ソースコード上で `HttpClient`、`WebClient`、ソケット通信、Web API送信などを使用しておらず、処理はローカルPC上で完結します。

📊 **テレメトリ、アクセス解析、広告、利用状況の収集、ユーザー追跡機能は実装していません。**

🔎 本ソフトウェアが読み取る対象は、ユーザーが指定した Firefox の `recovery.jsonlz4` と、ユーザー操作により参照するローカルの Firefox プロファイルフォルダです。

💾 前回使用した入力パス・出力先・ファイル名などは、利便性のため `extract_youtube_tabs_v1.0.0_settings.json` に**ローカル保存**されます。これらの情報を外部へ送信する処理はありません。

🧹 GitHub公開版からは、Visual Studio の `.vs`、`bin`、`obj`、`*.user`、ローカル発行プロファイルなど、開発環境固有の情報や絶対パスを含み得るファイルを除外しています。

🔍 公開ソースコードを確認することで、ファイルの読み取り、URL抽出、設定保存、出力処理の実装内容を利用者自身で検証できます。

---

# 🎬 Extract_YouTube_Tabs

🧰 `Extract_YouTube_Tabs` は、Firefox のセッション保存ファイル `recovery.jsonlz4` から、現在開かれている YouTube の通常動画および Shorts のURLを抽出し、`yt-dlp` などで利用しやすい `https://youtu.be/VIDEO_ID` 形式のテキスト一覧として保存する Windows 向けツールです。

## ✨ 主な機能

📂 Firefox の `recovery.jsonlz4` をファイル選択またはドラッグ＆ドロップで指定できます。

🦊 Firefox プロファイルフォルダを指定すると、`sessionstore-backups\recovery.jsonlz4` を自動設定できます。

🎞️ `youtube.com/watch?v=...` と `youtube.com/shorts/...` を抽出し、`https://youtu.be/...` 形式へ統一します。

🧹 同一動画URLは重複除去し、文字列順に並べて保存します。

📝 出力ファイル名は任意に指定でき、拡張子を省略した場合は `.txt` が付加されます。

🖱️ 入力ファイル、Firefoxプロファイル、出力先フォルダの各欄でドラッグ＆ドロップを利用できます。

💾 前回の入力パス、出力先、出力ファイル名、Firefoxプロファイル情報をローカル設定ファイルへ保存できます。

## 🖥️ 対応環境

🪟 Windows 10 / Windows 11 の 64bit 環境を対象としています。

⚙️ ソースコードは `.NET 8` / Windows Forms を使用しています。

📦 Release版は `win-x64` の自己完結型・単一EXEとして発行できます。

## 🚀 使い方

1. 📥 Release から `extract_youtube_tabs_v1.0.0.exe` を取得して任意の書き込み可能なフォルダへ配置します。
2. 🦊 Firefox のプロファイル内にある `sessionstore-backups\recovery.jsonlz4` を指定します。
3. 📁 必要に応じて出力先フォルダと出力ファイル名を指定します。
4. ▶️ **「抽出して保存」** を実行します。
5. ✅ 抽出されたURLが指定したテキストファイルへ保存されます。

## 🦊 recovery.jsonlz4 の場所

📍 一般的な保存場所は次の形式です。

```text
%APPDATA%\Mozilla\Firefox\Profiles\<プロファイル名>\sessionstore-backups\recovery.jsonlz4
```

👁️ `%APPDATA%\Mozilla\Firefox\Profiles` をエクスプローラーのアドレスバーへ入力すると、Firefox のプロファイル一覧を開けます。

🔐 `recovery.jsonlz4` にはブラウザのセッション情報が含まれるため、第三者へ不用意に共有しないでください。

## 📝 出力形式

🔗 抽出結果は1行につき1URLの形式で保存されます。

```text
https://youtu.be/XXXXXXXXXXX
https://youtu.be/YYYYYYYYYYY
```

📦 生成したファイルは `yt-dlp -a list.txt` などのバッチ入力に利用できます。

## 🔧 ソースコードからのビルド

🧑‍💻 Visual Studio 2022以降、または .NET 8 SDK を利用できます。

```powershell
dotnet restore .\src\extract_youtube_tabs_v1.0.0\extract_youtube_tabs_v1.0.0.csproj
dotnet publish .\src\extract_youtube_tabs_v1.0.0\extract_youtube_tabs_v1.0.0.csproj -c Release -r win-x64 --self-contained true
```

📦 プロジェクトファイル側で `PublishSingleFile=true` を指定しているため、Release発行では単一EXEを生成できます。

## 🔍 実装上のデータ処理

📥 指定された `recovery.jsonlz4` をローカルファイルとして読み込みます。

🗜️ Mozilla JSONLZ4 のヘッダーを確認し、LZ4圧縮されたJSONをローカルで展開します。

🧭 各Firefoxタブの現在選択されている履歴エントリからURLを読み取ります。

🎯 通常動画および Shorts の動画IDだけを抽出します。

💾 抽出結果を指定されたローカルテキストファイルへ保存します。

🚫 この一連の処理に外部サーバーへの送信処理はありません。

## 🧩 外部ライブラリ

📚 `K4os.Compression.LZ4` v1.3.8 を Mozilla JSONLZ4 の展開処理に使用しています。

⚖️ `K4os.Compression.LZ4` は MIT License で公開されています。詳細は [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) を参照してください。

## 🛡️ セキュリティ上の注意

🔐 Firefox のセッションファイルには閲覧中タブのURLなどが含まれるため、元ファイルそのものはプライベートデータとして扱ってください。

📁 本ツールは元の Firefox セッションファイルを変更せず、読み取り専用の入力データとして扱います。

💾 設定JSONにはローカルのファイルパスが保存されるため、設定JSONを第三者へ共有する場合は内容を確認してください。

🧪 不審な挙動やセキュリティ上の問題を発見した場合は、GitHub Issues から報告できます。

## ⚠️ 現行 v1.0.0 の対象URL

🎞️ 現行版が動画URLとして認識する対象は、`youtube.com/watch?v=VIDEO_ID` と `youtube.com/shorts/VIDEO_ID` です。

ℹ️ `youtu.be`、`youtube.com/live/`、`music.youtube.com`、モバイル用サブドメインなどは現行 v1.0.0 の抽出対象には含まれていません。

## 📜 本リポジトリの利用条件

👀 本リポジトリ独自コードは、現時点では個別のオープンソースライセンスを付与せず、透明性確保・内容確認・安全性検証を主目的として公開しています。

⚖️ 第三者ライブラリおよび各コンポーネントには、それぞれのライセンス条件が適用されます。

## 📌 バージョン

🏷️ 現行公開版: **v1.0.0**
