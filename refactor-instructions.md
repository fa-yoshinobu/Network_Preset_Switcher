# refactor-instructions.md — Network_Preset_Switcher

作成日: 2026-06-11。根拠: ソース・README.md・PROJECT_NOTES.md・ci.bat・git履歴の実読。
本リポジトリ単体で完結する指示書。本ファイル自体は untracked のままにし、コミットに含めない。

---

## 1. Objective

ユーザーが Excel で直接編集する CSV の読み書き仕様を1ビットも変えずに、
(1) 1860行の `MainViewModel` に埋まった CSV フォーマット処理・検証ロジックを特性テストで固定し、
(2) それらを移動のみでサービス層へ分離して、今後の変更コストを下げる。
ネットワーク適用（netsh/WMI）は実機・管理者権限でしか検証できないため**触らない**。

## 2. Project Understanding

- Windows WPF アプリ (net8.0-windows)。IP設定をプリセットから切り替える。管理者権限で netsh/WMI 実行。MIT License。
- `ViewModels/MainViewModel.cs`（1860行）が中核: CSV読み書き（手書きパーサ、UTF-8 BOM/CP932 fallback、
  タブ区切りヘッダーの自動カンマ変換、ヘッダー別名マッピング、レガシー形式の自動移行）、入力検証
  （IPv4/サブネットマスク連続ビット判定）、プリセットCRUD、適用、Ping、操作ログ（UI 500件 + `.log` 追記）、
  言語切替、自然順ソート（`PresetNaturalComparer`）。
- `Models/NetworkManager.cs`（static）: netsh 実行 → 失敗時 WMI fallback、アダプタ有効化（`Thread.Sleep(2000)`）。
- 多言語: `Resources/Strings.{en-US,ja-JP}.xaml` の ResourceDictionary 切替（`Infrastructure/Localization`）。
- 仕様根拠: `PROJECT_NOTES.md`（CSV仕様・UI仕様・既知の注意点）と `README.md`。
- テスト: `NetworkPresetSwitcher.Tests`（xUnit）に `NetworkPreset` モデルのテストのみ。CSV系は未テスト。
- 検証: `ci.bat` = restore → `dotnet format --verify-no-changes` → build(analyzers) → test → publish smoke test。

## 3. Behaviors To Preserve

1. **CSVファイル仕様一式**（ユーザーがExcelで直接編集する公開フォーマット）:
   ファイル名 `NetworkPresetSwitcher.csv`、exe同フォルダ保存、ヘッダー
   `Type,Name,Group,IP,Subnet,Gateway,DNS1,DNS2,Comment,Language`、Settings行/Preset行、
   UTF-8 BOM 書き出し（`File.Replace` による安全な置換、`.tmp` 後始末含む）。
2. 読み込みの許容動作: UTF-8(BOM無し)厳格デコード失敗時の CP932 fallback + 警告、
   タブ区切りヘッダーの自動カンマ変換（**引用符内タブは保持**）、ヘッダー別名
   （`MapHeaderKey`: ipaddress→IP, memo→Comment 等の全マッピング）、ヘッダー無しレガシー形式の
   自動移行と再保存、Language列欠落/Settings行欠落時の警告と再書き出し。
3. 検証仕様: 空Subnetは保存時 `255.255.255.0` 補完、DHCP時は検証スキップ、不正値は保存不可（赤枠）、
   サブネットマスクは連続ビットのみ許容（0 と全1は拒否）。
4. `IP="dhcp"` ⇔ `IsDhcp` の相互変換と直前静的IPの復元（既存テストが固定済み）。
5. 操作ログ: UI上限500件、`NetworkPresetSwitcher.log` への追記、書き込み失敗の黙殺。
6. グループ表示・自然順ソートの並び順、Ping入力欄の自動補完規則（手動入力中は上書きしない）。
7. csproj の `<Version>`、`.github/workflows/`（ci.yml / release.yml / VirusTotal）。

## 4. Non-Negotiables

- 開始前に `git status` 確認。本ファイル以外の差分があれば停止・報告。
- 編集前に `ci.bat` を実行し baseline（成否・テスト件数）を記録。失敗なら作業中止・報告。
- 1論点=1コミット。無関係な整形・ついでの変更をしない。`dotnet format` を通してからコミット。
- NuGet の追加・更新はしない（テスト用パッケージは既存 Tests プロジェクトにあるものを使う）。
- `NetworkManager` と `AdapterViewModel` のネットワーク系ロジックは変更しない。

## 5. Stop And Ask Conditions

1. テストを書いたら現実装と PROJECT_NOTES.md / README の記述が矛盾した場合。
2. 変更が CSV の読み書き結果（バイト列レベル）に影響しうる場合。
3. csproj / `.github/workflows/` / app.manifest に触れたくなった場合。
4. `NetworkManager` の挙動を変えたくなった場合（提案として報告のみ）。
5. 修正するとユーザーから見える挙動（警告ダイアログの出方等）が変わるバグを発見した場合。

## 6. Baseline Commands

```bat
cd /d D:\refactor\Network_Preset_Switcher
git status
ci.bat
```

## 7. Debt Map

凡例: ✅=実装可 / ⚠️=条件付き(指定フェーズ厳守) / ❌=提案・報告のみ

| # | 負債 | 根拠 | 改善案 | 可否 |
|---|---|---|---|---|
| N1 | `MainViewModel.cs` 1860行: CSVパーサ/ライタ/エンコーディングfallback/ヘッダー別名/レガシー移行/検証/適用/Ping/ログ/自然順ソートが同居 | 同ファイル実読 | 純粋static群（`ReadCsvRows`/`NormalizeCsvDelimiters`/`ReplaceTabsOutsideQuotes`/`ReadCsvTextWithFallback`/`LooksLikeHeader`/`BuildHeaderMap`/`MapHeaderKey`/`NormalizeHeaderKey`/`ToCsvLine`/`EscapeCsv`/`TrimBom`/`SafeGet`/`IsTypeRow`/`CsvEncodingMode`）を `Services/PresetCsvFormat.cs` へ、`IsValidIpv4`/`IsValidIpv4Optional`/`IsValidSubnetMask` を `Services/Ipv4Validation.cs` へ、`PresetNaturalComparer` を `Services/` へ**機械的移動のみ**。Load/Save 本体の統合は次段の提案 | ⚠️ |
| N2 | CSV読み書き・検証のテストが無い（既存テストは `NetworkPreset` のみ） | `Tests/NetworkPresetTests.cs` | N1 の前に `internal` 化 + `InternalsVisibleTo` で特性テスト追加: round-trip、CP932 fallback、タブ→カンマ（引用符内保持）、別名ヘッダー、レガシー移行、サブネット検証の境界値、自然順ソート | ✅ |
| N3 | `NetworkManager`: `Thread.Sleep(2000)`、UIスレッドでの同期 netsh 実行（適用中フリーズ）、`throw new Exception`（非具体型） | `Models/NetworkManager.cs:17-114` | 実OS変更・自動テスト不能のため**変更しない**。問題点の記録のみ | ❌ |
| N4 | VM 内に `MessageBox.Show` が13箇所直書き（テスト阻害） | `MainViewModel.cs` 全域 | `IDialogService` 抽出は中規模変更。設計案として報告のみ | ❌ |
| N5 | netsh コマンド文字列に `adapter.Name` を補間（引用符内・エスケープなし） | `NetworkManager.cs:84` | セキュリティ境界として記録のみ。変更しない | ❌ |
| N6 | `PingCommand` が `async _ =>` ラムダで fire-and-forget | `MainViewModel.cs:123` | `PingTargetAsync` は内部 try/catch/finally 済みで実害なし。記録のみ | ❌ |

## 8. Implementation Phases

1. **Phase 0 — baseline**: `git status` / `ci.bat` 実行・記録。失敗なら停止。
2. **Phase 1 — 安全網 (N2)**: 対象 static メソッドを `internal` 化（ロジック・シグネチャ不変）、
   csproj に `InternalsVisibleTo("NetworkPresetSwitcher.Tests")` を追加し、特性テストを追加。
   テストは**変更前の挙動をそのまま固定**する。PROJECT_NOTES.md と矛盾したら §5-1 で停止。
3. **Phase 2 — 責務分離 (N1)**: テストが通る状態を維持したまま、§7-N1 記載のメンバーを
   `Services/` 配下へ機械的移動（アクセス修飾子と呼び出し元の参照変更のみ可）。
   1移動先=1コミット。移動後にアプリを起動しメイン画面表示・プリセット一覧表示を確認。
4. **Phase 3 — 提案のみ (N3/N4/N5)**: NetworkManager の非同期化案・例外型整理案・IDialogService 案を
   最終レポートに設計提案として記載。実装禁止。

## 9. Verification Requirements

- 各フェーズで `ci.bat` 完走（format / build(analyzers) / test / publish smoke test）。
- Phase 1 のテストは変更前コードで全件パスすることを確認してから Phase 2 へ。
- Phase 2 の移動前後で `dotnet test` の件数・成否が同一であること。
- 手動確認（Phase 2 後）: アプリ起動 → プリセット一覧とグループ表示 → 既存 CSV があれば読み込み確認。
  ネットワーク適用ボタンは**押さない**（実環境を変更するため）。

## 10. Reporting Format

1. 実施フェーズ / 追加・変更ファイル / コミット一覧
2. baseline と最終の `ci.bat` 結果対比（テスト件数 before → after）
3. 最後に実行した検証コマンドと生出力
4. Stop And Ask 該当事項一覧（新規発見含む）
5. Phase 3 の設計提案（実装していないことを明記）
6. スキップした確認とその理由

## 11. Out-of-scope

- `NetworkManager` / `AdapterViewModel` のネットワーク適用・取得ロジック変更
- `.github/workflows/`、バージョン番号、app.manifest（requireAdministrator）の変更
- CSV フォーマット・検証仕様の「改善」、UI/XAML/文言の変更
- NuGet 追加・更新、MVVMライブラリ導入、新機能追加、網羅的整形

---

## 12. Implementation Status

実装済み（2026-06-12）。

実施済みフェーズ:

- Phase 0: baseline 確認済み。
- Phase 1: CSV/IPv4/自然順ソートの特性テストを追加済み。
- Phase 2: CSV format helpers、IPv4 validation helpers、natural comparer を `Services/` へ分離済み。
- Phase 3: `NetworkManager` 非同期化、`IDialogService`、netsh コマンド周辺は提案・記録のみで、実装対象外のまま維持。

実装コミット:

- `d683a8c` Add CSV characterization tests
- `4d7dda7` Move CSV format helpers to service
- `ba6284c` Move IPv4 validation helpers to service
- `16a9a8f` Move preset natural comparer to service

検証:

- `ci.bat` 完走。
- 最終テスト結果: 24 passed。
- publish smoke test passed。

補足:

- 実環境のネットワーク設定変更を避けるため、ネットワーク適用ボタンの手動実行は行っていない。
