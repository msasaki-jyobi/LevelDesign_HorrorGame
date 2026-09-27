# HorrorKit

一人称ホラー用の基本セット。Unity 6 / URP / Input System / Cinemachine 3。

## メニュー

| メニュー | 内容 |
|---|---|
| HorrorKit > マネージャー (Ctrl+Shift+H) | アイテム・フラグ・イベント・会話テキストの一括管理、再生中のデバッグ |
| HorrorKit > セットアップ > ライティング・フォグ・ポストプロセスを適用 | 現在のシーンを暗い雰囲気にする |
| HorrorKit > セットアップ > プレイヤー一式を配置 | Player / Cinemachineカメラ / 懐中電灯 / HUD / EventRunner |
| HorrorKit > セットアップ > デモシーンを新規作成 | `Assets/_Project/Scenes/HorrorDemo.unity` を作り直す |
| Hierarchy 右クリック > HorrorKit | 調べるイベント / エリアイベント / ドア / ジャンプスケア / ちらつくライト |

## 操作

WASD 移動 / マウス 視点 / E・左クリック 調べる・会話送り / F 懐中電灯 / Shift 走る / C しゃがむ / Tab 所持品

## イベント (HorrorEvent)

- ページ制：番号の大きいページから条件判定し、最初に条件を満たしたページが有効になる。
- 起動方法：調べる（通常のColliderが必要）/ エリアに入る（Is Trigger の Collider）/ 自動実行。
- 出現条件：フラグ ON/OFF、アイテム所持/未所持（すべて満たすと有効）。
- 「一度だけ実行」したページは以後スキップされ、下のページに移る。
- コマンド：会話、アイテム入手/使用、フラグ設定、ドア開閉・施錠・解錠、ジャンプスケア、効果音、懐中電灯明滅、フェード、待機、表示切替、終了。

実行済み判定はシーン名＋階層パスで記録するため、同じ階層に同名のイベントを置かないこと。

## データ

キーアイテムとフラグは `Assets/_Project/HorrorKit/Resources/HorrorDatabase.asset` に定義する（マネージャーから編集）。
ID を変えたときはマネージャー下部の「一括置換」でシーン内の参照も直せる。
