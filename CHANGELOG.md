# Changelog

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

Git のタグ・コミット差分から、利用者向けの変更と主要な開発・配布変更をまとめています。
過去の日付は対象コミットの日付です。公開日を示すものではありません。

## [Unreleased]

### Changed

- Standardized fixed-name Windows ZIP assets, ZIP SHA-256 checksums and English release notes.

### Fixed

- Unified VirusTotal completion checks and preserved unrelated release notes during rescans.

## 1.0.4 — 2026-09-20

- DHCP の WMI 代替処理の対象を説明文からアダプター固有の ID に変更し、1件に特定できない場合は変更を中止。
- 適用開始時にプリセットをコピーし、適用中の編集が設定内容や結果表示へ混入する問題を修正。
- 新規プリセットで選択中の既存プリセットと同名を保存できる問題を修正。既存プリセットの編集時は名前の維持が可能。
- 起動時のアダプター一覧で、内部・仮想アダプターより物理 Ethernet / Wi-Fi を優先。仮想アダプターは手動選択でき、一覧更新時は選択中のアダプターを維持。
  - [ysnst の実装 c83deb6](https://github.com/ysnst/Network_Preset_Switcher/commit/c83deb6) と名前判定テストを取り込み。
- プリセットの新規作成・編集・削除・複製は、CSV 保存成功後に一覧と成功ログを更新。保存失敗時は既存一覧と編集中の内容を保持し、再試行できるよう修正。
- DHCP 切り替えの WMI 代替処理で、IP に加えて DNS も自動取得へ変更。DNS 設定失敗や戻り値欠落を成功扱いしないよう修正。
- 保存失敗と再試行、DHCP 代替処理、アダプターの並び順・選択維持の回帰テストを追加。
- 製品バージョンを `1.0.4`、アセンブリ・ファイルバージョンを `1.0.4.0` に更新。
- この変更履歴と README からのリンクを追加。
- CI を整理。文書・スクリーンショット・LICENSE のみの main への push は CI 対象外とし、PR の検証は維持。依存キャッシュの判定対象にテストプロジェクトも追加。
- Release の全 Git 履歴取得とテスト用依存の復元、ローカル CI の重複復元を省略。通常 CI の EXE 生成チェックは維持。

## 1.0.3 — 2026-06-15

対象: [v1.0.2...v1.0.3](https://github.com/fa-yoshinobu/Network_Preset_Switcher/compare/v1.0.2...v1.0.3)

- ネットワーク設定適用と外部コマンドの待機を非同期化。
- `netsh` の引数を文字列連結から `ArgumentList` に変更し、空白・引用符を含むアダプター名を個別の引数として渡すよう改善。
- 設定適用専用の例外型とダイアログサービスを導入し、コマンド引数・例外のテストを追加。
- CSV 読み書き・IPv4 検証・プリセット自然順ソートをサービスへ整理。CSV と入力検証・ソートのテストを追加。
- VirusTotal の手動再実行と `repository_dispatch` による再実行を追加。
- 脆弱性報告方針を `SECURITY.md` に追加し、README・開発メモを更新。

## 1.0.2 — 2026-03-28

対象: [ad2b12a...v1.0.2](https://github.com/fa-yoshinobu/Network_Preset_Switcher/compare/ad2b12a...v1.0.2)

- タグを使った GitHub Actions のリリース処理を追加。単一 EXE と SHA-256 チェックサムを配布する構成へ変更。
- ローカルで復元・整形確認・ビルド・テスト・配布 EXE 生成を行う `ci.bat` を追加。
- `make.bat` を `build.bat` に変更し、実行環境・出力先の指定と任意のクリーンビルドに対応。
- CI の解析・配布ファイル生成チェックを整備。

## 1.0.1 — 2026-03-18（タグなし）

対象: [ad2b12a — Pre-release](https://github.com/fa-yoshinobu/Network_Preset_Switcher/commit/ad2b12a)

- プロジェクトのバージョンを `1.0.1` に更新。対応するリリースタグは確認できないため、開発履歴として記載。
- xUnit のテストプロジェクトとプリセットモデルのテストを追加。
- GitHub Actions の CI を追加し、既存コードの整形と README の開発情報を更新。
- 1.0.0 以降、このコミットまでに MIT の `LICENSE` を追加し、README の表題・言語表記を整理。

## 1.0.0 — 2026-02-15

対象: [Ver.1.0.0](https://github.com/fa-yoshinobu/Network_Preset_Switcher/tree/Ver.1.0.0)

- Windows / WPF アプリとして DHCP・固定 IP の切り替えを提供。
- プリセットの作成・編集・複製・削除、グループ表示、CSV 保存に対応。
- 操作ログ、Ping、日本語・英語 UI 切り替えを提供。
- UTF-8 BOM / CP932 の CSV 読み込みと、タブ区切りヘッダーの変換に対応。
- 配布用ビルドスクリプトを追加。
