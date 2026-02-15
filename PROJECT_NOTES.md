# NetworkPresetSwitcher - 仕様・履歴メモ

このドキュメントは、設計意図・変更履歴・運用上の注意点をまとめたメモです。

## 概要
- Windows で IP アドレス設定をプリセットから切り替える WPF アプリ
- 日本語/英語 UI 対応
- CSV でプリセット保存（Excel 編集想定）

## UI / UX
- 2ペイン構成（左: プリセット一覧 + Ping、右: 詳細 + 操作ログ）
- プリセット一覧は 2 行表示（名前 / IP）
- 操作ログはスクロール固定
- 右側の「Preset Details / Activity」は境界バーをドラッグして高さを変更可能
- タイトルとアイコンの縦位置を揃える調整済み

## ビルド / 配布
- `build.bat` で self-contained / single-file publish
- 出力: `NetworkPresetSwitcher\bin\Release\net8.0-windows\win-x64\publish\NetworkPresetSwitcher.exe`
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
- 不正値は赤枠・保存不可

## 操作ログ
- 最大 500 件
- 詳細は長文の場合ツールチップで全文表示

## Ping
- 入力欄は選択中アダプタの IPv4 を初期値にする
- アダプタ切替・IP変更時に反映（手動入力中は上書きしない）

## 既知の注意点
- exe フォルダ固定保存。権限不足時は保存失敗（エラーメッセージ表示）
- CSV を Excel で開いたままにすると保存失敗
