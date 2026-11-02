# HorrorKit（EventKit 用ホラー追加パック）

一人称ホラー用の追加パック。イベント・会話・フラグ等の共通部分は **EventKit**（`Assets/_Project/EventKit`）にあります。

> 初めての人は **Docs/HorrorKit_ギミック制作ガイド.pdf**（画像付きの入門書。一部のメニュー名は旧名 HorrorKit のまま）。
> クラス・関数の詳細は Docs/HorrorKit_解説書.md、移植や追加パックの作り方は EventKit/Docs のマニュアル。

## 中身

| 種類 | 内容 |
|---|---|
| プレイヤー | FirstPersonController（IEventPlayer 実装）、Flashlight、HorrorInput（視点・ダッシュ・しゃがみ・懐中電灯） |
| 演出 | JumpScare（IEventPerformance 実装）、FlickerLight |
| 拡張コマンド | ホラー/懐中電灯の明滅・ON/OFF・電池を補充 |
| セットアップ | HorrorKit > セットアップ（ライティング・フォグ・ポストプロセス／プレイヤー一式／デモシーン） |
| Hierarchy | 右クリック > HorrorKit > ジャンプスケア／ちらつくライト |

## 操作

WASD 移動 / マウス 視点 / E・左クリック 調べる・会話送り / F 懐中電灯 / Shift 走る / C しゃがむ / Tab 所持品
