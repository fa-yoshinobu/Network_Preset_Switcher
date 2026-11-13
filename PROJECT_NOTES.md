# NetworkPresetSwitcher - 仕様・履歴メモ

このドキュメントは、設計意図・変更履歴・運用上の注意点をまとめたメモです。

## 概要
- Windows で IP アドレス設定をプリセットから切り替える WPF アプリ
- 日本語/英語 UI 対応
- CSV でプリセット保存（Excel 編集想定）

## UI / UX
- ヘッダー: アプリ名・言語切替（日本語 / English）・⚙ メニュー（プリセット保存先、バージョン情報）
- 状態カード: アダプタ選択、接続状態バッジ（接続中=緑 / 切断=赤 / 不明=灰）、GW への Ping と結果表示、任意宛先の Ping
- 2ペイン構成（左: プリセット一覧、右: 詳細 + 操作ログ）。幅 1000px 未満では縦に並べてページ全体をスクロール（最小幅 760px）
- プリセット一覧は 2 行表示（名前 / IP）。グループ名に件数、選択中アダプタの現在値と一致するプリセットに「適用中」
- 詳細は閲覧表示と編集表示を切替。閲覧時は「適用すると変わる項目」と適用ボタン、編集時は保存/キャンセルのバーを表示
- 適用前に現在値と適用後の差分を確認ダイアログで表示。有線は「アプリを閉じるまで確認しない」を選択可（保存しない）。Wi-Fi は毎回確認
- キー操作: Enter / Delete はプリセット一覧にフォーカスがあるときだけ適用 / 削除。編集欄の Enter は次の欄へ移動。Ctrl+S 保存、Esc キャンセル、Ctrl+N 追加、Ctrl+E 編集、F5 更新
- ダブルクリックでプリセットを適用（確認ダイアログ経由）

## ビルド / 配布
- `build.bat` で self-contained / single-file publish
- `ci.bat` で restore / format / build / test / publish smoke test
- 出力: `artifacts\release\NetworkPresetSwitcher.exe`
- exe アイコン: `AppIcon.ico`

## バージョン / ライセンス
- バージョン: 1.0.0
- GitHub: `https://github.com/fa-yoshinobu/Network_Preset_Switcher`
- ライセンス: MIT
- ライセンス条文表示は NetworkPresetSwitcher のみ
- Libraries 一覧は表示（Version タブ）

## CSV 仕様
- 保存ファイル名: `NetworkPresetSwitcher.csv`
- 保存場所: exe と同じフォルダ
- ヘッダー:
  `Type,Name,Group,IP,Subnet,Gateway,DNS1,DNS2,Comment,Language`
- Settings 行:
  `Settings,,,,,,,,,"en-US"` もしくは `"ja-JP"`
- Preset 行:
  `Preset,Name,Group,IP,Subnet,Gateway,DNS1,DNS2,Comment,`

### 読み書き仕様
- UTF-8 BOM 推奨。失敗時は CP932 で再読込
- ヘッダーがタブ区切りの場合は自動でカンマに変換（引用符内のタブは保持）
- JSON 形式は非対応

## 入力バリデーション
- IP / Subnet / Gateway / DNS1 / DNS2 の形式をチェック
- 空欄は許容（Subnet は保存時に `255.255.255.0` を補完）
- Subnet は `/24` や `24`（1〜31）も入力可。保存時にドット表記へ変換して CSV に書く
- 不正値は赤枠と欄の下のメッセージ・保存不可

## 操作ログ
- 最大 500 件
- 詳細は長文の場合ツールチップで全文表示

## Ping
- 入力欄は選択中アダプタの IPv4 を初期値にする
- アダプタ切替・IP変更時に反映（手動入力中は上書きしない）
- 「GW へ Ping」は選択中アダプタのゲートウェイ宛て。結果は状態カードと操作ログに表示

## 既知の注意点
- exe フォルダ固定保存。権限不足時は保存失敗（エラーメッセージ表示）
- CSV を Excel で開いたままにすると保存失敗
