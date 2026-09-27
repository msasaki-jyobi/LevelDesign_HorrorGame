using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorKit
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
        [InspectorName("演出/ジャンプスケア")] JumpScare = 8,
        [InspectorName("演出/効果音")] PlaySound = 9,
        [InspectorName("演出/懐中電灯を明滅")] FlickerFlashlight = 10,
        [InspectorName("演出/フェードアウト")] FadeOut = 11,
        [InspectorName("演出/フェードイン")] FadeIn = 12,
        [InspectorName("制御/待機")] Wait = 13,
        [InspectorName("制御/オブジェクト表示切替")] SetActive = 14,
        [InspectorName("制御/イベント終了")] EndEvent = 15,
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
        public JumpScare jumpScare;
        public AudioClip clip;
        [Min(0f)] public float duration = 1f;
        public GameObject target;

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
