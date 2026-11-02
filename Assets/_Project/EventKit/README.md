# EventKit

RPGツクール風のイベントシステム（ジャンル非依存）。Unity 6 / Input System / Cinemachine 3 / uGUI。

> 移植・UIの差し替え・追加パックの作り方は **Docs/EventKit_マニュアル（移植・追加パック制作）.pdf**。
> PDF の元データは Docs/Manual~/ にあります。

## メニュー

| メニュー | 内容 |
|---|---|
| EventKit > マネージャー (Ctrl+Shift+H) | アイテム・フラグ・イベント・会話の一括管理 |
| EventKit > イベントエディタ (Ctrl+Shift+E) | ページ・出現条件・実行内容（挿入・並び替え・条件分岐・選択肢） |
| EventKit > デバッグ | フラグ ON/OFF（緑・赤）、所持品、イベント実行・実行済みリセット、変化の記録。再生前は「開始時の状態」を設定。ゲーム画面の下に変化を通知 |
| EventKit > UIプレハブ管理 | 会話・選択肢・プロンプト・所持品・フェード・ロード画面のプレハブを差し替え |
| EventKit > セットアップ > シーンにイベント基盤を配置 | EventRunner と EventHUD を置く |
| EventKit > 書き出し | .unitypackage（EventKit だけ／追加パック込み） |
| イベントエディタ > テンプレート | ページ（1ページ／全ページ）をテンプレートに保存・テンプレートから追加。保存先は EventKit の外の EventTemplates |
| ドアのインスペクター > イベントページで管理する | 調べたときの動き（条件・会話・解錠）をイベントのページで管理。今の鍵の設定からページを自動で作る |
| イベントをダブルクリック | Hierarchy・マネージャーのイベント一覧からイベントエディタで開く |
| Hierarchy 右クリック > EventKit | 調べるイベント・エリアイベント・振り返り会話・ドア・引き戸・出現地点 |

## 他のプロジェクトで使うとき

1. EventKit フォルダを .meta ごとコピー（または書き出した .unitypackage を読み込む）
2. シーンに基盤を配置
3. プレイヤーに `IEventPlayer` を実装して `EventPlayer.Register(this)`、調べる用に `PlayerInteractor`
4. プレイヤー操作は `InputLock.IsLocked` の間止める

## 拡張

- 拡張コマンド：`CustomCommand` を継承し `[Serializable, EventCommand("グループ/名前")]` を付ける → 挿入メニューに自動で並ぶ
- 演出オブジェクト：コンポーネントに `IEventPerformance` を実装
- 調べられる物：`IInteractable` を実装
- アイテム・フラグの欄：string に `[ItemId]` / `[FlagId]`（マネージャーの参照検索・一括置換の対象になる）

見本は HorrorKit（`Assets/_Project/HorrorKit/Scripts/Commands/FlashlightCommands.cs` など）。
