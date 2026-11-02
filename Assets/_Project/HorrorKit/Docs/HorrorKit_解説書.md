# HorrorKit 解説書

> **構成の変更（EventKit への分離）**
> イベント・イベントエディタ・フラグ/アイテム・会話UI・ドア・シーン遷移などの共通部分は `Assets/_Project/EventKit`（汎用パック）に移りました。
> HorrorKit は一人称操作・懐中電灯・ジャンプスケアなどの「ホラー用追加パック」です。
> クラス名の変更：HorrorEvent → **GameEvent**、HorrorDatabase → **EventDatabase**、HorrorHUD → **EventHUD**（見た目は「EventKit > UIプレハブ管理」で差し替え）。
> 移植・UIの差し替え・追加パックの作り方は **EventKit/Docs/EventKit_マニュアル（移植・追加パック制作）.pdf** を参照。

Unity 6 / URP / Input System / Cinemachine 3 向けの一人称ホラー基盤。
この文書は「3Dオブジェクトを HorrorKit の要素にする方法」「各クラスの役割」「関数リファレンス」をまとめたもの。

---

## 目次

1. [全体の仕組み](#1-全体の仕組み)
2. [3Dオブジェクトを HorrorKit の要素にする](#2-3dオブジェクトを-horrorkit-の要素にする)
3. [クラス解説](#3-クラス解説)
4. [関数リファレンス](#4-関数リファレンス)
5. [自作スクリプトから使う](#5-自作スクリプトから使う)
6. [拡張する（コマンドの追加）](#6-拡張するコマンドの追加)
7. [ファイル構成](#7-ファイル構成)

---

## 1. 全体の仕組み

```
                ┌──────────────────────────┐
  入力           │ HorrorInput (Input System) │
                └────────────┬─────────────┘
                             │
   ┌─────────────────────────┼──────────────────────────┐
   ▼                         ▼                          ▼
FirstPersonController   PlayerInteractor            Flashlight / EventHUD
 移動・視点・しゃがみ     視線レイで IInteractable を探す   懐中電灯 / 所持品(Tab)
 Cinemachineの揺れ          │
                             │ Interact()
               ┌─────────────┴──────────────┐
               ▼                            ▼
          GameEvent                     Door
   ページ判定（条件: GameState）         開閉・施錠・鍵
               │ Run()
               ▼
          EventRunner ── コマンドを順に実行 ──┐
               │                             │
     ┌─────────┼──────────┬──────────┬───────┴──────┐
     ▼         ▼          ▼          ▼              ▼
 EventHUD   GameState   Door     JumpScare     Flashlight
 会話・フェード フラグ/所持品 開閉    演出          明滅

 EventDatabase (Resources) … アイテム・フラグの「定義」
 GameState (static)          … 実行中の「状態」（フラグ値・所持品・実行済みページ）
```

- **定義と状態を分けている。** `EventDatabase` はアセット（編集時のデータ）、`GameState` は実行中の値。再生を止めると `GameState` は初期化される。
- **イベントは同時に1つだけ実行される。** 実行中は `InputLock` によりプレイヤー操作が止まる。
- **調べられるもの**はすべて `IInteractable` を実装している（`GameEvent` と `Door`）。

---

## 2. 3Dオブジェクトを HorrorKit の要素にする

HorrorKit の要素は「ただの GameObject + コンポーネント」なので、**どんな3Dモデル（FBX / ProBuilder / プリミティブ）でも同じ手順で使える。**

### 2-1. 共通ルール

| ルール | 理由 |
|---|---|
| 調べる対象には **通常の Collider**（Is Trigger OFF）が必要 | 視線レイは Trigger を無視するため |
| エリアイベントには **Is Trigger の Collider** が必要 | `OnTriggerEnter` でプレイヤーを検知するため |
| Collider は **子オブジェクトに付いていてもよい** | `GetComponentInParent<IInteractable>()` で親を探すため |
| 小さい物は Collider を実物より大きめにする | 狙いやすくするため（例：メモ・鍵） |
| 調べられる距離は `PlayerInteractor.distance`（初期 2.2m） | |

### 2-2. 調べられるオブジェクト（机・メモ・ラジオ・絵画など）

1. モデルをシーンに置く（FBX をドラッグ等）。
2. Collider が無ければ追加する（`BoxCollider` 推奨。複雑な形なら `MeshCollider`）。
3. **Add Component → GameEvent**（またはインスペクターに出る警告ボタンで Collider 追加）。
4. ページ1の「起動方法」を **調べる** にし、実行内容にコマンドを並べる。

> モデルが複数パーツの親子構造なら、**親**に `GameEvent` を付け、Collider はどのパーツに付いていても良い。

### 2-3. 拾えるアイテム（鍵・メモ・写真など）

`GameEvent` 1つで作れる：

| 設定 | 値 |
|---|---|
| 起動方法 | 調べる |
| 表示する行動名 | 拾う |
| 一度だけ実行 | ON |
| 実行内容 | ①会話（任意）②アイテム/入手 → `item_id` ③制御/オブジェクト表示切替 → **自分自身**・表示しない |

アイテムIDは先に **マネージャー → アイテム** で登録しておく（表示名・説明文が所持品画面に出る）。

> 「棚の奥にある鍵」のように、**調べる対象（棚）と見た目（鍵）が別**の場合は、棚に `GameEvent` を付け、表示切替の対象を鍵モデルにする（デモの「廊下の棚」がこの形）。

### 2-4. 自作モデルのドア

`Door` は **ヒンジ（回転軸）の位置にあるオブジェクト** に付ける。

```
Door_MyModel        ← 空のGameObject。ドアの蝶番の位置に置く。Door + AudioSource を付ける
 └ DoorModel (FBX)  ← ドアの見た目。回転軸がヒンジに来るように位置をずらす。Collider を付ける
```

1. 空の GameObject を作り、ドアの**蝶番の位置**に置く。
2. ドアモデルをその子にし、板の端がヒンジ位置に来るようにずらす。
3. 親に `Door` と `AudioSource` を追加（`hinge` は空欄なら自分自身）。
4. `openAngle` を設定（開く向きが逆ならマイナス値）。
5. 施錠するなら `locked` ON、`keyItemId` に鍵アイテムを選ぶ。

> Hierarchy 右クリック → **HorrorKit → ドア** で同じ構造のひな形が作れるので、`Panel` を自作モデルに差し替えるのが一番簡単。

#### 引き戸（横開きドア）

```
SlidingDoor         ← Door（Motion = 横スライド）+ AudioSource。扉の左端の位置に置く
 └ Slider           ← Door の Hinge に指定。開くとここが Slide Offset だけ動く
     ├ Panel        ← 扉の板（Collider 付き）
     └ Handle       ← 引き手
```

Hierarchy 右クリック → **HorrorKit → 引き戸（横開きドア）** で作れる。開く向きを逆にするなら Slide Offset の X をマイナスにする。壁の中に引き込む場合は、壁を扉より厚くしておくと板が隠れる。

#### イベントエディタ（RPGツクール風）

メニュー **HorrorKit → イベントエディタ**（Ctrl+Shift+E）、またはイベントのインスペクターの「イベントエディタで編集する」で開く。

- 上：ページのタブ（新規・コピー・貼り付け・並び替え・削除）
- 左：出現条件・起動方法・一度だけ実行
- 右：実行内容。`◆文章：…` のように1行ずつ並ぶ。条件分岐・選択肢の中は字下げされる
- 下：選んだ行の設定

| 操作 | やり方 |
|---|---|
| 挿入 | 行をダブルクリック／Enter／「＋ コマンドを挿入」。選んだ行の**上**に入る（末尾の ◆ を選ぶと最後に追加） |
| コピー・切り取り・貼り付け | Ctrl+C / Ctrl+X / Ctrl+V（右クリックメニューにもある） |
| 削除 | Delete |
| 並び替え | Alt+↑↓ または ▲▼ ボタン |

条件分岐・選択肢は **ブロック単位** で扱う。開始行（◆条件分岐／◆選択肢の表示）を選ぶとブロック全体が選択され、まとめてコピー・移動・削除できる。
「：それ以外のとき」「：［はい］のとき」「：分岐終了」の行は自動管理（選択肢の数を変えると［○○］のときの行も増減する）。

追加したコマンド：

| コマンド | 内容 |
|---|---|
| 会話/選択肢の表示 | 選択肢（最大6個）を出し、選んだものの「［○○］のとき」の中を実行 |
| フロー/条件分岐 | フラグ・所持品で分岐。「それ以外のとき」は有無を選べる |
| フロー/別のイベントを実行 | 指定したイベントの有効ページをその場で実行（共通処理の使い回しに） |
| カメラ/視点変更 | 指定の位置・向きのカメラへブレンドで切り替える。挿入するとカメラ操作ウィンドウが開く |
| カメラ/視点変更解除 | 指定秒数で通常（プレイヤー）の視点へ戻る |
| カメラ/カメラシェイク | 指定秒数・強さでカメラを揺らす（だんだん弱まる） |
| オブジェクト/アクティブにする・非アクティブにする | 対象の GameObject を ON / OFF するだけ |
| オブジェクト/移動 | 今の位置から X左右・Y上下・Z前後 に何m、何秒かけて動かす（オブジェクトの向き基準も選べる） |
| オブジェクト/回転 | ローカル回転値／ワールド回転値／今の回転に加算 のどれかで、何秒かけて回す（加算は一回転などもそのまま回る） |
| オブジェクト/スケール変更 | 指定のスケールに何秒かけて変える |
| 制御/UnityEventの実行 | インスペクターと同じ要領で登録した UnityEvent を呼ぶ |
| 制御/待機 | 指定秒数たつまで次へ進まない |

時間のかかるコマンド（視点変更・解除・シェイク・移動・回転・スケール）には **次に進める条件** がある。

- **そのまま次へ**：動き始めたらすぐ次のコマンドへ（例：扉を回しながら会話を出す）
- **終了するまで待機**：動き終わってから次へ

#### 視点変更（カメラ操作ウィンドウ）

1. イベントエディタで「＋ コマンドを挿入」→ カメラ →「視点変更」。今のシーンビューの位置が入った状態で **視点変更（カメラ）** ウィンドウが開く。
2. シーンビューで見せたい構図にしてから **「今のシーンビューを視点に設定」**。または **「シーンビューに追従」** を押すと、シーンビューを動かした分がそのまま入る。
3. 上のプレビューでゲーム画面での見え方を確認する（暗い場面は「懐中電灯で照らして見る」で照らして確認できる。プレビュー用のライト・カメラは保存されない隠しオブジェクト）。
4. **ブレンド秒数**（何秒かけて切り替えるか）と **次に進める条件** を決める。
5. 「シーンビューをこの視点に合わせる」で、設定した視点をシーンビューで見直せる（データは変わらない）。

- イベントを選ぶと、シーンに黄色い枠（▣ 視点1, 2 …）でカメラの位置と向きが出る。
- 視点変更中に次の「視点変更」を実行すると、その視点へブレンドで移る。
- **解除し忘れても、イベントが終わると 0.5 秒で通常視点に戻る。**

#### 音を鳴らして振り返る会話

会話/メッセージ コマンドの **振り向く先** に空のオブジェクト（音の発生源）を指定すると、
「その位置から **音** が鳴る → プレイヤーがそちらへ **振り向く** → 会話が出る」の順に動く。
Hierarchy 右クリック → **HorrorKit → 振り返り会話イベント（音＋振り向き）** でひな形（エリアイベント＋SoundPoint）が作れる。
SoundPoint をプレイヤーの背後に置き、会話コマンドの「音」に効果音を入れて使う。
会話を出さずに振り向かせるだけなら **演出/視点を向ける** コマンドを使う。

### 2-5. ジャンプスケアに自作モデルを使う

```
JumpScare_XXX       ← JumpScare + CinemachineImpulseSource
 ├ Ghost (モデル)    ← 非アクティブにしておく。scareObject に指定
 │   └ Head         ← lookPoint に指定（顔を見させる）
 └ LungeTarget      ← 突進先。lungeTarget に指定
```

1. Hierarchy 右クリック → **HorrorKit → ジャンプスケア**。
2. 子の `ScareObject（差し替えてください）` を消し、自作モデルを子に入れて **非アクティブ** にする。
3. `JumpScare` の各欄を設定：`scareObject` / `lookPoint` / `lungeTarget` / `scareSound`。
4. モデルの Collider は外す（プレイヤーとぶつかって押し戻されるのを防ぐ）。
5. イベントのコマンド「演出/ジャンプスケア」に、この `JumpScare` を指定。

アニメーション付きモデルなら、`scareObject` が有効になった瞬間に Animator の初期ステートが再生される。

### 2-6. エリアで起きるイベント（通ると何か起きる）

1. Hierarchy 右クリック → **HorrorKit → エリアイベント**（Trigger の箱が付く）。
2. 箱の大きさを通路に合わせる（シーン上ではオレンジ色で表示）。
3. 起動方法：**エリアに入る**、多くの場合「一度だけ実行」ON。
4. 条件（例：鍵を持っている）とコマンド（例：ジャンプスケア）を設定。

### 2-7. ちらつく照明を自作ランプに付ける

1. ランプモデルの子に Point / Spot Light を置く。
2. Light に `FlickerLight` を追加。
3. 電球メッシュ（Emission ありのマテリアル）を `emissiveRenderer` に指定すると、光と一緒に明滅する。

### 2-8. プレハブ化するときの注意

- `GameEvent` のコマンドが **シーン内の別オブジェクト**（Door / JumpScare / 表示切替対象）を参照している場合、その参照は**同じプレハブ内**に収まっていないと失われる。
- 「拾えるアイテム」（自分自身を消すだけ）や「調べるだけのオブジェクト」はそのままプレハブ化できる。
- プレハブを複数置くときは **オブジェクト名を変える**（→ 3-4 の「実行済み」判定参照）。

### 2-9. プレイヤーを別シーンに置く

**HorrorKit → セットアップ → プレイヤー一式を配置** で以下がまとめて作られる。

| 生成物 | 役割 |
|---|---|
| `Player` | CharacterController + FirstPersonController + PlayerInteractor |
| `Player/CameraRoot` | 目の位置。ここを Cinemachine が追従 |
| `Player/Flashlight` | Spot Light + Flashlight |
| `Main Camera` | CinemachineBrain 付き（既存があれば流用） |
| `PlayerCamera (Cinemachine)` | 一人称カメラ（揺れ・Impulse 受信） |
| `[HorrorKit HUD]` | 画面UI |
| `[HorrorKit Systems]` | EventRunner |

雰囲気は **HorrorKit → セットアップ → ライティング・フォグ・ポストプロセスを適用**。

---

## 3. クラス解説

### 3-1. データ・状態（Scripts/Core）

#### `EventDatabase`（ScriptableObject）
キーアイテムとフラグの **定義** を持つアセット。`Assets/_Project/EventKit/Resources/EventDatabase.asset` に置き、実行時は `Resources.Load` で自動取得される。編集はマネージャーから行う。

- `ItemDefinition`：`id` / `displayName`（表示名）/ `description`（所持品画面の説明）/ `icon`
- `FlagDefinition`：`id` / `description`（メモ）/ `defaultValue`（初期値）

#### `GameState`（static クラス）
実行中の **状態**。フラグの現在値、所持品、実行済みイベントページを保持する。
再生開始時に自動リセットされ、`EventDatabase` のフラグ初期値が入る。値が変わると `Changed` イベントが飛ぶ（自動実行イベントや所持品画面の更新に使われる）。

#### `HorrorInput`（static クラス）
Input System のアクションを **コードで定義** している（.inputactions アセット不要）。キー割り当てを変えたい場合はこのファイルの `Ensure()` を編集する。

| アクション | キーボード/マウス | ゲームパッド |
|---|---|---|
| Move | WASD / 矢印 | 左スティック |
| Look / LookStick | マウス移動 | 右スティック |
| Sprint | 左Shift | 左スティック押し込み |
| Crouch | C / 左Ctrl | B(東) |
| Interact | E / 左クリック | A(南) |
| Flashlight | F | Y(北) |
| Inventory | Tab / I | Select |
| Submit（会話送り） | E / Space / Enter / 左クリック | A(南) |

#### `InputLock`（static クラス）
プレイヤー操作を止めるカウンタ。イベント実行中や所持品画面表示中に `Push()`、終わったら `Pop()`。カウンタ方式なので複数の要因が重なっても正しく戻る。

#### `IInteractable`（interface）
「調べられるもの」の共通窓口。`GameEvent` と `Door` が実装している。自作の仕掛け（スイッチ等）もこれを実装すれば `PlayerInteractor` から調べられる（→ 5章）。

#### `ItemIdAttribute` / `FlagIdAttribute`
`string` フィールドに付けると、インスペクターで **データベースのIDをドロップダウン選択** できるようになる。

```csharp
[ItemId] public string requiredKey;
[FlagId] public string flagToSet;
```

### 3-2. イベント（Scripts/Events）

#### `GameEvent`（MonoBehaviour, IInteractable）
会話・アイテム・フラグ・演出をまとめる中心コンポーネント。**ページ** のリストを持つ。

**ページ選択ルール**
1. 最後のページから順に見ていく
2. 出現条件をすべて満たし、かつ「実行済みでスキップ」でないページを探す
3. 最初に見つかったページが **有効ページ**。その起動方法に一致したときに実行される

**起動方法**
| 起動方法 | 実行されるタイミング |
|---|---|
| 調べる | プレイヤーが視線を合わせて Interact キーを押したとき |
| エリアに入る | プレイヤーが Trigger Collider に入ったとき（イベント実行中なら終わってから） |
| 自動実行 | ページが有効になった瞬間（開始時・フラグ変化時）。常に1回だけ |

#### `EventPage` / `EventCondition` / `EventCommand`（データクラス）
- `EventPage`：`memo` / `trigger` / `prompt` / `runOnce` / `conditions` / `commands`
- `EventCondition`：`type`（フラグON・OFF / アイテム所持・未所持）+ `id`
- `EventCommand`：`type` によって使うフィールドが変わる汎用データ

| コマンド | 使うフィールド |
|---|---|
| 会話/メッセージ | `speaker`, `text`, `clip`（同時に鳴らす音・任意）, `target`（振り向く先・任意）, `duration`（振り向く秒数） |
| アイテム/入手・失う | `itemId`, `silent` |
| フラグ/設定 | `flagId`, `boolValue` |
| ドア/開ける・閉める・施錠・解錠 | `door` |
| 演出/ジャンプスケア | `jumpScare` |
| 演出/効果音 | `clip`, `target`（鳴らす場所・任意。指定すると立体音響）, `boolValue`（終わるまで待つ） |
| 演出/視点を向ける（振り返る） | `target`（向く先）, `duration`（振り向く秒数） |
| フロー/条件分岐 → それ以外のとき → 分岐終了 | `condition` |
| 会話/選択肢の表示 → ［選択肢］のとき → 選択肢終了 | `choices`（開始行）, `choiceIndex`（［選択肢］のとき） |
| フロー/別のイベントを実行 | `callEvent` |
| 演出/懐中電灯を明滅・フェード、制御/待機 | `duration` |
| 制御/オブジェクト表示切替 | `target`, `boolValue` |
| オブジェクト/アクティブにする・非アクティブにする | `target` |
| カメラ/視点変更 | `viewPosition`, `viewRotation`, `viewFov`（0=プレイヤーと同じ）, `duration`（ブレンド秒）, `waitMode` |
| カメラ/視点変更解除 | `duration`（戻る秒）, `waitMode` |
| カメラ/カメラシェイク | `duration`, `strength`, `waitMode` |
| オブジェクト/移動 | `target`, `vector`（移動量m）, `localSpace`, `duration`, `waitMode` |
| オブジェクト/回転 | `target`, `rotateMode`, `vector`（角度）, `duration`, `waitMode` |
| オブジェクト/スケール変更 | `target`, `vector`（スケール）, `duration`, `waitMode` |
| 制御/UnityEventの実行 | `unityEvent` |
| 制御/イベント終了 | なし（以降のコマンドを打ち切る） |

#### `EventRunner`（MonoBehaviour, シングルトン）
コマンド列をコルーチンで **上から順に実行** する。実行中は `IsBusy == true`、プレイヤー操作はロックされる。シーンに無ければ自動生成される。`itemGetSound` にアイテム入手音を設定できる。

### 3-3. プレイヤー（Scripts/Player）

#### `FirstPersonController`（MonoBehaviour）
- 本体（Player）を **ヨー回転**、`cameraRoot` を **ピッチ回転** させる。カメラ本体は Cinemachine が `cameraRoot` に追従する構造。
- 移動は `CharacterController`。加速・減速は `acceleration` で滑らかに補間。
- しゃがみはトグル。頭上に障害物があると立ち上がらない。
- 移動速度に応じて Cinemachine の Noise（手ブレ）の強さを変える。
- 足音：`footstepClips` を設定すると歩幅間隔で鳴る。
- 演出用に **強制注視**（`LookAtRoutine`）と **画角キック**（`KickFov`）を持つ。

#### `PlayerInteractor`（MonoBehaviour）
毎フレーム `origin`（CameraRoot）から前方にレイを飛ばし、当たった Collider の親から `IInteractable` を探す。見つかれば HUD にプロンプトを出し、Interact キーで `Interact()` を呼ぶ。プレイヤー自身の Collider は無視する。

#### `Flashlight`（MonoBehaviour）
- F キーで点灯/消灯。
- `follow`（CameraRoot）に **少し遅れて追従**（`followSharpness` が小さいほど遅れる＝揺れる光）。
- 電池：`useBattery` ON で減る。`lowBatteryThreshold` 以下で暗くなり、ランダムに明滅。
- `requireItem` ON にすると、`requiredItemId` を所持するまで点かない（「懐中電灯を拾う」演出用）。

### 3-4. ワールド（Scripts/World）

#### `Door`（MonoBehaviour, IInteractable）
開き戸（`motion` = 回転：ヒンジで回る）または引き戸（`motion` = 横スライド：`hinge` に指定した物が `slideOffset` だけ動く）。調べると開閉。施錠時は `keyItemId` を所持していれば自動で解錠→開く（`consumeKey` で鍵を消費、`unlockFlag` を ON）。持っていなければ `lockedMessage` を表示。
`playerCanUse` を OFF にすると、プレイヤーは触れず **イベントからのみ** 開閉できる（勝手に閉まる扉など）。

#### `JumpScare`（MonoBehaviour）
`Play()` で次を同時に行う：
1. `scareObject` を表示
2. `scareSound` 再生
3. `impulse`（CinemachineImpulseSource）で画面を揺らす
4. 懐中電灯を明滅
5. プレイヤーの画角を `fovKick` だけ変える
6. `lookPoint` へ強制的に視点を向ける（`forceLook`）
7. `lungeTarget` へ `lungeTime` 秒で突進
8. `focusCamera` があれば優先度を上げて切り替え（固定カメラ演出用）
9. `duration` 秒後に元に戻す（`hideAfter` で非表示）

#### `FlickerLight`（MonoBehaviour）
Perlin ノイズで光を揺らし、`blackoutChance`（1秒あたりの確率）で一瞬消灯する。`emissiveRenderer` を指定すると電球の発光も連動。

### 3-5. UI（Scripts/UI）

#### `EventHUD`（MonoBehaviour, シングルトン）
UIを **実行時にコードで生成** する（プレハブ不要）。

| 要素 | 内容 |
|---|---|
| 照準 | 画面中央の点。調べられる物を見ると大きくなる |
| プロンプト | `[E] 調べる` |
| 会話ウィンドウ | 話者名ボックス + 本文（文字送り）+ ▼ |
| 所持品画面 | Tab で開閉、W/S で選択、説明文表示 |
| フェード | 全画面の黒。会話ウィンドウより下に描画されるので暗転中も文字は読める |

フォントは未設定なら OS の日本語フォント（Yu Gothic UI → Meiryo …）を使う。ビルド配布時に確実に表示したい場合は、ライセンス上問題のない日本語フォント（例：Noto Sans JP）を `font` に設定する。

### 3-6. エディタ拡張（Editor）

| クラス | 役割 |
|---|---|
| `EventKitWindow` | マネージャーウィンドウ（アイテム / フラグ / イベント / 会話テキスト / デバッグ） |
| `GameEventEditor` | `GameEvent` のインスペクター（ページタブ、ページ操作、Collider警告） |
| `EventCommandDrawer` | コマンドをタイプ別に描画（色帯付き） |
| `EventConditionDrawer` | 条件を1行で描画（IDはドロップダウン） |
| `ItemIdDrawer` / `FlagIdDrawer` | `[ItemId]` / `[FlagId]` のドロップダウン |
| `HorrorKitMenus` | Hierarchy 右クリック → HorrorKit の生成メニュー |
| `HorrorKitSetup` | 雰囲気適用・プレイヤー配置・デモシーン生成 |
| `EventKitEditorUtil` | 共通処理（DB取得、ID選択UI、参照検索、ID一括置換、要約文生成） |

---

## 4. 関数リファレンス

※ `public` のみ。`static` は明記。

### GameState（static）

| 関数 / プロパティ | 説明 |
|---|---|
| `bool GetFlag(string id)` | フラグの値。未定義なら false |
| `void SetFlag(string id, bool value)` | フラグを設定。値が変わったときだけ `Changed` 発火 |
| `bool HasItem(string id)` | 所持しているか |
| `void AddItem(string id)` | 所持品に追加（重複は無視）。`ItemAdded` と `Changed` 発火 |
| `void RemoveItem(string id)` | 所持品から削除 |
| `bool IsPageFinished(string key)` | ページが実行済みか |
| `void MarkPageFinished(string key)` | ページを実行済みにする |
| `void ResetAll()` | フラグ・所持品・実行済みを初期化 |
| `IReadOnlyList<string> Inventory` | 所持アイテムID一覧（入手順） |
| `IReadOnlyDictionary<string,bool> Flags` | 全フラグの現在値 |
| `event Action Changed` | 状態が変わったとき |
| `event Action<string> ItemAdded` | アイテム入手時（引数はID） |

### EventDatabase

| 関数 / プロパティ | 説明 |
|---|---|
| `static EventDatabase Instance` | Resources から読み込んだデータベース |
| `ItemDefinition GetItem(string id)` | アイテム定義を取得（無ければ null） |
| `FlagDefinition GetFlag(string id)` | フラグ定義を取得 |
| `static string ItemName(string id)` | 表示名を取得（未定義ならIDを返す） |

### HorrorInput / InputLock（static）

| 関数 / プロパティ | 説明 |
|---|---|
| `HorrorInput.Ensure()` | アクションを生成・有効化（何度呼んでも1回だけ実行） |
| `HorrorInput.Move` など | 各 `InputAction`。`ReadValue` / `WasPressedThisFrame` で使う |
| `InputLock.IsLocked` | 操作ロック中か |
| `InputLock.Push()` / `Pop()` | ロックを1段かける / 外す |

### GameEvent

| 関数 / プロパティ | 説明 |
|---|---|
| `string eventName` | マネージャー等に出る名前 |
| `List<EventPage> pages` | ページ一覧 |
| `string DisplayName` | `eventName`（空ならオブジェクト名） |
| `int ActivePageIndex` | 現在有効なページ番号（0始まり、無ければ -1） |
| `EventPage ActivePage` | 現在有効なページ |
| `string InteractPrompt` | プロンプトに出す行動名 |
| `bool CanInteract` | 今調べられるか（有効ページが「調べる」かつイベント非実行中） |
| `void Interact(PlayerInteractor)` | 調べる（有効ページが「調べる」なら実行） |
| `void RunActivePage()` | 起動方法に関係なく有効ページを実行 |
| `string PageKey(int index)` | 実行済み判定用のキー（`シーン名:階層パス#番号`） |

### EventPage / EventCondition

| 関数 | 説明 |
|---|---|
| `bool EventPage.ConditionsMet()` | すべての条件を満たすか（条件なしなら true） |
| `bool EventCondition.IsMet()` | この条件を満たすか |
| `bool EventCondition.IsItemCondition` | アイテム系の条件か |
| `bool EventCommand.IsItemCommand` | アイテム系のコマンドか |

### EventRunner

| 関数 / プロパティ | 説明 |
|---|---|
| `static EventRunner Instance` | 実行器（無ければ自動生成） |
| `static bool IsBusy` | イベント実行中か |
| `static int LastEndFrame` | 最後にイベントが終わったフレーム（決定キーの二重入力防止） |
| `void Run(GameEvent ev, int pageIndex)` | 指定ページを実行（必要なら実行済みとして記録） |
| `void RunCommands(IList<EventCommand> commands)` | 任意のコマンド列を実行 |
| `void ShowMessages(params string[] lines)` | メッセージだけの簡易イベント |

### FirstPersonController

| 関数 / プロパティ | 説明 |
|---|---|
| `static FirstPersonController Instance` | 現在のプレイヤー |
| `bool IsCrouching` / `bool IsSprinting` / `float Speed` | 状態 |
| `static void SetCursorLocked(bool locked)` | マウスカーソルのロック/解除 |
| `IEnumerator LookAtRoutine(Transform target, float blendTime, float holdTime)` | 対象へ視点を向け、holdTime の間追い続ける。`StartCoroutine` で使う |
| `void KickFov(float delta, float duration)` | 画角を一瞬 delta だけ変えて戻す（マイナスでズームイン） |
| `void Teleport(Vector3 position, float yaw)` | 位置と向きを瞬時に変える（場面転換用） |

### PlayerInteractor

| 関数 / プロパティ | 説明 |
|---|---|
| `IInteractable Current` | 現在見ている調べられる対象（無ければ null） |

### Flashlight

| 関数 / プロパティ | 説明 |
|---|---|
| `static Flashlight Instance` | 現在の懐中電灯 |
| `bool CanUse` | 使用可能か（`requireItem` の判定込み） |
| `void Toggle()` | 点灯/消灯 |
| `void Flicker(float duration)` | duration 秒間明滅させる |
| `void AddBattery(float amount)` | 電池を回復（0〜1） |

### Door

| 関数 / プロパティ | 説明 |
|---|---|
| `void Open()` / `void Close()` | 開ける / 閉める（施錠中は開かない） |
| `void Lock()` / `void Unlock()` | 施錠（開いていれば閉じる）/ 解錠（`unlockFlag` を ON） |
| `void Interact(PlayerInteractor)` | 調べたときの処理（鍵判定込み） |
| `string InteractPrompt` / `bool CanInteract` | IInteractable 実装 |

### JumpScare

| 関数 | 説明 |
|---|---|
| `void Trigger()` | 演出を開始（投げっぱなし） |
| `IEnumerator Play()` | 演出本体。終わるまで待ちたいときは `yield return` で使う |

### EventHUD

| 関数 / プロパティ | 説明 |
|---|---|
| `static EventHUD Instance` | 現在のHUD |
| `IEnumerator ShowMessage(string speaker, string text)` | 文字送り表示し、決定入力まで待つ |
| `void RequestAdvance()` | 会話を1つ送る（UIボタン・タッチ操作用） |
| `void HideDialogue()` | 会話ウィンドウを閉じる |
| `IEnumerator Fade(float targetAlpha, float duration)` | 暗転（1）/ 明転（0） |
| `void SetPrompt(string action)` | プロンプト表示（null で非表示） |
| `void ToggleInventory()` | 所持品画面の開閉 |
| `bool IsDialogueOpen` / `bool IsInventoryOpen` | 表示状態 |

### エディタ用（EventKitEditorUtil ほか）

| 関数 | 説明 |
|---|---|
| `EventKitEditorUtil.Database` | データベースアセット |
| `EventKitEditorUtil.EnsureDatabase()` | 無ければ作成 |
| `EventKitEditorUtil.IdPopup(...)` | ID選択ドロップダウン（自作エディタでも使える） |
| `EventKitEditorUtil.CollectReferences(bool items)` | シーン内のID参照を収集 |
| `EventKitEditorUtil.ReplaceIdInScene(from, to, items)` | シーン内のID参照を一括置換（Undo対応） |
| `EventKitEditorUtil.PageSummary(EventPage)` | ページの1行要約 |
| `HorrorKitMenus.CreateInteractEvent(cmd)` / `CreateAreaEvent(cmd)` | イベントの生成 |
| `HorrorKitSetup.ApplyAtmosphere()` | 雰囲気設定を現在のシーンに適用 |
| `HorrorKitSetup.CreatePlayerRig(pos, yaw)` | プレイヤー一式を生成 |
| `HorrorKitSetup.BuildDemoScene()` | デモシーンを生成（上書き） |

---

## 5. 自作スクリプトから使う

### フラグ・アイテムを操作する

```csharp
using HorrorKit;

GameState.SetFlag("power_on", true);
if (GameState.HasItem("rusty_key")) { ... }
GameState.AddItem("battery");
```

### メッセージだけ出す

```csharp
EventRunner.Instance.ShowMessages("電気がついた。", "……誰かいる？");
```

### 状態変化に反応する

```csharp
void OnEnable()  => GameState.Changed += Refresh;
void OnDisable() => GameState.Changed -= Refresh;
void Refresh()   => lamp.enabled = GameState.GetFlag("power_on");
```

### 自作の「調べられる仕掛け」を作る（IInteractable）

```csharp
using HorrorKit;
using UnityEngine;

public class LightSwitch : MonoBehaviour, IInteractable
{
    public Light[] lights;
    [FlagId] public string powerFlag;

    public string InteractPrompt => "スイッチを押す";
    public bool CanInteract => !EventRunner.IsBusy;

    public void Interact(PlayerInteractor interactor)
    {
        if (!GameState.GetFlag(powerFlag))
        {
            EventRunner.Instance.ShowMessages("……反応しない。電気が来ていないようだ。");
            return;
        }
        foreach (var l in lights) l.enabled = !l.enabled;
    }
}
```

Collider を付けたオブジェクトに追加すれば、そのまま `[E] スイッチを押す` と表示されて使える。

### スクリプトからジャンプスケア

```csharp
public JumpScare scare;
void OnTriggerEnter(Collider other)
{
    if (other.GetComponentInParent<FirstPersonController>()) scare.Trigger();
}
```

（通常は GameEvent のエリアイベントで十分）

---

## 6. 拡張する（コマンドの追加）

例として「懐中電灯の電池を回復する」コマンドを追加する手順。

1. **`EventData.cs`** の `CommandType` に値を追加（既存の番号は変えない）
   ```csharp
   [InspectorName("アイテム/電池を回復")] AddBattery = 16,
   ```
2. **`EventRunner.cs`** の `Execute()` に処理を追加
   ```csharp
   case CommandType.AddBattery:
       if (Flashlight.Instance != null) Flashlight.Instance.AddBattery(c.duration);
       break;
   ```
3. **`EventKitDrawers.cs`** の `EventCommandDrawer` に表示を追加
   - `ExtraLines()` に行数（この例では 1）
   - `OnGUI()` の switch に `duration` を「回復量」として描画する case

既存フィールド（`text` / `itemId` / `flagId` / `boolValue` / `duration` / `target` など）を流用すれば、`EventCommand` 自体を変更する必要はない。新しい参照型が必要な場合のみ `EventCommand` にフィールドを追加する。

> 注意：enum の **番号** でシーンに保存されるため、既存の値の番号を変えたり削除したりすると、配置済みイベントのコマンドが別物になる。

---

## 7. ファイル構成

```
Assets/_Project/
├ Scenes/HorrorDemo.unity   デモシーン
└ HorrorKit/
   ├ Scripts/
   │ ├ Core/     EventDatabase.cs, GameState.cs, HorrorInput.cs（InputLock / IInteractable / 属性も同梱）
   │ ├ Events/   EventData.cs, GameEvent.cs, EventRunner.cs
   │ ├ Player/   FirstPersonController.cs, PlayerInteractor.cs, Flashlight.cs
   │ ├ World/    Door.cs, JumpScare.cs, FlickerLight.cs
   │ └ UI/       EventHUD.cs
   ├ Editor/     EventKitWindow.cs, GameEventEditor.cs, EventKitDrawers.cs,
   │             HorrorKitMenus.cs, HorrorKitSetup.cs, EventKitEditorUtil.cs
   ├ Resources/  EventDatabase.asset
   ├ Settings/   HorrorVolumeProfile.asset（ポストプロセス）
   ├ Demo/Materials/
   ├ Docs/       この解説書
   └ README.md   概要
```

HorrorKit フォルダはどこに移動しても動く（エディタ拡張は自分の場所から各パスを求める）。
ただし `Editor` / `Resources` フォルダ名と、`HorrorKit` 直下の構成は変えないこと。

### 既知の注意点

- **実行済み判定** はシーン名＋階層パスで記録する。同じ階層に同名のイベントを置くと、実行済みが共有されてしまう。複製したら名前を変えること。
- **Cinemachine の「Save During Play」** が有効だと、再生終了時に「変更を保存しますか」と聞かれる（揺れや画角をスクリプトが動かしているため）。**Don't Keep** を選ぶか、`PlayerCamera (Cinemachine)` のインスペクターでチェックを外す。
- **セーブ/ロード** は未実装。`GameState` の `Flags` / `Inventory` と実行済みページを保存すれば実装できる（実行済みページの一覧は現在 private なので、取得用プロパティの追加が必要）。
