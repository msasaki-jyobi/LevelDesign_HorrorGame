using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventKit
{
    public enum EventTrigger
    {
        [InspectorName("調べる（決定キー）")] Interact = 0,
        [InspectorName("エリアに入る")] EnterArea = 1,
        [InspectorName("自動実行（条件を満たしたら1回）")] AutoStart = 2,
    }

    public enum ConditionType
    {
        [InspectorName("フラグがON")] FlagOn = 0,
        [InspectorName("フラグがOFF")] FlagOff = 1,
        [InspectorName("アイテムを所持")] HasItem = 2,
        [InspectorName("アイテムを未所持")] NotHasItem = 3,
    }

    public enum CommandType
    {
        [InspectorName("会話/メッセージ")] Message = 0,
        [InspectorName("アイテム/入手")] GiveItem = 1,
        [InspectorName("アイテム/失う・使う")] RemoveItem = 2,
        [InspectorName("フラグ/設定")] SetFlag = 3,
        [InspectorName("ドア/開ける")] OpenDoor = 4,
        [InspectorName("ドア/閉める")] CloseDoor = 5,
        [InspectorName("ドア/施錠")] LockDoor = 6,
        [InspectorName("ドア/解錠")] UnlockDoor = 7,
        [InspectorName("演出/演出オブジェクトを再生（ジャンプスケア等）")] JumpScare = 8,
        [InspectorName("演出/効果音")] PlaySound = 9,
        [InspectorName("演出/視点を向ける（振り返る）")] LookAt = 16,
        [InspectorName("演出/懐中電灯を明滅（追加パック）")] FlickerFlashlight = 10,
        [InspectorName("演出/フェードアウト（画面が暗くなる）")] FadeOut = 11,
        [InspectorName("演出/フェードイン（画面が明るくなる）")] FadeIn = 12,
        [InspectorName("制御/待機")] Wait = 13,
        [InspectorName("制御/オブジェクト表示切替（ON/OFF指定）")] SetActive = 14,
        [InspectorName("制御/イベント終了")] EndEvent = 15,

        [InspectorName("カメラ/視点変更")] CameraView = 24,
        [InspectorName("カメラ/視点変更解除")] CameraReset = 25,
        [InspectorName("カメラ/カメラシェイク")] CameraShake = 31,
        [InspectorName("オブジェクト/アクティブにする")] ActivateObject = 26,
        [InspectorName("オブジェクト/非アクティブにする")] DeactivateObject = 27,
        [InspectorName("オブジェクト/移動")] MoveObject = 28,
        [InspectorName("オブジェクト/回転")] RotateObject = 29,
        [InspectorName("オブジェクト/スケール変更")] ScaleObject = 30,
        [InspectorName("オブジェクト/モーション再生")] PlayAnimation = 36,
        [InspectorName("制御/UnityEventの実行")] InvokeUnityEvent = 32,
        [InspectorName("シーン/シーン遷移")] TransferScene = 33,
        [InspectorName("シーン/ロード画面付きシーン遷移")] TransferSceneLoading = 34,
        [InspectorName("シーン/シーン内ワープ")] Warp = 35,

        /// <summary>追加パックの拡張コマンド（<see cref="CustomCommand"/>）</summary>
        [InspectorName("拡張コマンド")] Custom = 40,

        // ── フロー制御（イベントエディタから挿入する。ブロックの印は自動で管理される）──
        [InspectorName("フロー/条件分岐")] If = 17,
        [InspectorName("フロー/それ以外のとき")] Else = 18,
        [InspectorName("フロー/分岐終了")] EndIf = 19,
        [InspectorName("会話/選択肢の表示")] ShowChoices = 20,
        [InspectorName("会話/［選択肢］のとき")] When = 21,
        [InspectorName("会話/選択肢終了")] EndChoices = 22,
        [InspectorName("フロー/別のイベントを実行")] CallEvent = 23,
    }

    /// <summary>時間のかかるコマンドの「次に進める条件」</summary>
    public enum WaitMode
    {
        [InspectorName("そのまま次へ")] Continue = 0,
        [InspectorName("終了するまで待機")] WaitForEnd = 1,
    }

    /// <summary>オブジェクトの回転の指定方法</summary>
    public enum RotateMode
    {
        [InspectorName("ローカル回転値を指定")] Local = 0,
        [InspectorName("ワールド回転値を指定")] World = 1,
        [InspectorName("今の回転に加算")] Add = 2,
    }

    /// <summary>モーション再生の指定方法</summary>
    public enum AnimationPlayMode
    {
        [InspectorName("Animator のステートを再生")] State = 0,
        [InspectorName("Animator のトリガーを送る")] Trigger = 1,
        [InspectorName("アニメーションクリップを直接再生")] Clip = 2,
    }

    public static class CommandTypes
    {
        /// <summary>「次に進める条件」を持つコマンド</summary>
        public static bool HasWaitMode(CommandType t) =>
            t == CommandType.CameraView || t == CommandType.CameraReset || t == CommandType.CameraShake
            || t == CommandType.MoveObject || t == CommandType.RotateObject || t == CommandType.ScaleObject
            || t == CommandType.PlayAnimation;

        /// <summary>ブロックの開始（条件分岐・選択肢）</summary>
        public static bool IsOpener(CommandType t) => t == CommandType.If || t == CommandType.ShowChoices;
        /// <summary>ブロックの終了</summary>
        public static bool IsCloser(CommandType t) => t == CommandType.EndIf || t == CommandType.EndChoices;
        /// <summary>ブロック内の区切り（それ以外のとき・［選択肢］のとき）</summary>
        public static bool IsDivider(CommandType t) => t == CommandType.Else || t == CommandType.When;
        /// <summary>開始・区切り・終了のいずれか（自動管理される行）</summary>
        public static bool IsStructural(CommandType t) => IsOpener(t) || IsCloser(t) || IsDivider(t);

        /// <summary>
        /// from から同じ階層を順に見て、isTarget に当たる行か、このブロックの終了行の位置を返す。
        /// 入れ子のブロックは飛ばす。見つからなければ cmds.Count。
        /// </summary>
        public static int ScanLevel(IList<EventCommand> cmds, int from, Func<CommandType, bool> isTarget)
        {
            int depth = 0;
            for (int i = from; i < cmds.Count; i++)
            {
                var t = cmds[i] != null ? cmds[i].type : CommandType.Wait;
                if (depth == 0 && (IsCloser(t) || (isTarget != null && isTarget(t)))) return i;
                if (IsOpener(t)) depth++;
                else if (IsCloser(t)) depth--;
            }
            return cmds.Count;
        }

        /// <summary>start が開始行のとき、対応する終了行の位置を返す。開始行でなければ start。</summary>
        public static int BlockEnd(IList<EventCommand> cmds, int start)
        {
            if (start < 0 || start >= cmds.Count || cmds[start] == null || !IsOpener(cmds[start].type)) return start;
            return Math.Min(ScanLevel(cmds, start + 1, null), cmds.Count - 1);
        }
    }

    [Serializable]
    public class EventCondition
    {
        public ConditionType type;
        public string id;

        public bool IsItemCondition => type == ConditionType.HasItem || type == ConditionType.NotHasItem;

        public bool IsMet()
        {
            switch (type)
            {
                case ConditionType.FlagOn: return GameState.GetFlag(id);
                case ConditionType.FlagOff: return !GameState.GetFlag(id);
                case ConditionType.HasItem: return GameState.HasItem(id);
                case ConditionType.NotHasItem: return !GameState.HasItem(id);
                default: return true;
            }
        }
    }

    [Serializable]
    public class EventCommand
    {
        public CommandType type;
        public string speaker;
        [TextArea(2, 6)] public string text;
        public string itemId;
        public string flagId;
        public bool boolValue = true;
        [Tooltip("アイテム入手/使用時の自動メッセージを出さない")]
        public bool silent;
        public Door door;
        [Tooltip("演出オブジェクトを再生：IEventPerformance を持つコンポーネント（JumpScare など）")]
        public MonoBehaviour jumpScare;
        [Tooltip("会話：表示と同時に鳴らす音（声・効果音）／効果音：鳴らす音")]
        public AudioClip clip;
        [Min(0f)] public float duration = 1f;
        [Tooltip("会話：振り向く先（音もここから鳴る）／効果音：鳴らす場所／視点を向ける：向く先／表示切替：対象")]
        public GameObject target;
        [Tooltip("条件分岐の条件")]
        public EventCondition condition = new EventCondition();
        [Tooltip("選択肢の文言（2〜4個）")]
        public List<string> choices = new List<string> { "はい", "いいえ" };
        [Tooltip("［選択肢］のとき：何番目の選択肢か（0始まり）")]
        public int choiceIndex;
        [Tooltip("別のイベントを実行：実行するイベント")]
        public GameEvent callEvent;

        [Tooltip("時間のかかるコマンド：次に進める条件")]
        public WaitMode waitMode = WaitMode.WaitForEnd;
        [Tooltip("移動：移動量(m)／回転：角度／スケール：大きさ")]
        public Vector3 vector;
        [Tooltip("移動：オブジェクトの向きを基準にする")]
        public bool localSpace;
        public RotateMode rotateMode;
        [Tooltip("視点変更：カメラの位置")]
        public Vector3 viewPosition;
        [Tooltip("視点変更：カメラの向き")]
        public Vector3 viewRotation;
        [Tooltip("視点変更：画角（0 ならプレイヤーと同じ）")]
        public float viewFov;
        [Tooltip("カメラシェイク：強さ")]
        public float strength = 1f;
        public UnityEngine.Events.UnityEvent unityEvent = new UnityEngine.Events.UnityEvent();

        [Tooltip("シーン遷移：遷移先シーンのパス（Assets/…/○○.unity）")]
        public string scenePath;
        [Tooltip("シーン遷移：遷移先シーンの GUID（シーンを移動・改名しても追えるように）")]
        public string sceneGuid;
        [Tooltip("シーン遷移：到着する出現地点の名前（空ならシーンの初期位置）")]
        public string spawnId;
        [Tooltip("シーン遷移：フェードイン（画面が明るくなる）の秒数（フェードアウト＝暗くなる は duration）")]
        [Min(0f)] public float fadeInDuration = 1f;
        [Tooltip("ロード画面付きシーン遷移：読み込み中に表示する UI プレハブ")]
        public GameObject loadingPrefab;

        [Tooltip("拡張コマンド：追加パックのコマンド本体")]
        [SerializeReference] public CustomCommand custom;

        [Tooltip("モーション再生：指定方法")]
        public AnimationPlayMode animMode;
        [Tooltip("モーション再生：ステート名／トリガー名")]
        public string animName;
        [Tooltip("モーション再生：直接再生するクリップ")]
        public AnimationClip animClip;

        public bool IsItemCommand => type == CommandType.GiveItem || type == CommandType.RemoveItem;
    }

    [Serializable]
    public class EventPage
    {
        [Tooltip("エディタ上でのメモ（ゲームには表示されない）")]
        public string memo;
        public EventTrigger trigger = EventTrigger.Interact;
        [Tooltip("調べる時に画面に出る行動名")]
        public string prompt = "調べる";
        [Tooltip("一度実行したら、このページは以後スキップされる")]
        public bool runOnce;
        public List<EventCondition> conditions = new List<EventCondition>();
        public List<EventCommand> commands = new List<EventCommand>();

        public bool ConditionsMet()
        {
            if (conditions == null) return true;
            foreach (var c in conditions)
                if (c != null && !c.IsMet()) return false;
            return true;
        }
    }
}
