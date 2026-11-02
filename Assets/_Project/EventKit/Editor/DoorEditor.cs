using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EventKit.EditorTools
{
    /// <summary>
    /// ドアのインスペクター。項目名を日本語で表示し、開き戸／引き戸に合わせて関係する項目だけを出す。
    /// 「イベントページで管理する」で、調べたときの動きをイベントのページ（条件・会話・解錠）に置き換えられる。
    /// </summary>
    [CustomEditor(typeof(Door))]
    [CanEditMultipleObjects]
    public class DoorEditor : Editor
    {
        static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "motion", "動き方" },
            { "hinge", "動く部分" },
            { "openAngle", "開く角度" },
            { "slideOffset", "スライド量" },
            { "openSpeed", "開く速さ" },
            { "isOpen", "最初から開いている" },
            { "locked", "施錠中" },
            { "keyItemId", "鍵アイテム" },
            { "consumeKey", "解錠時に鍵を消費" },
            { "lockedMessage", "施錠時のメッセージ" },
            { "unlockMessage", "解錠時のメッセージ" },
            { "requiredFlag", "解錠フラグ" },
            { "unlockFlag", "解錠時にONにするフラグ" },
            { "pageEvent", "調べたときのイベント" },
            { "playerCanUse", "プレイヤーが開閉できる" },
            { "openSound", "開く音" },
            { "closeSound", "閉じる音" },
            { "lockedSound", "施錠時の音" },
            { "unlockSound", "解錠の音" },
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var motion = serializedObject.FindProperty("motion");
            bool sliding = !motion.hasMultipleDifferentValues && (DoorMotion)motion.intValue == DoorMotion.Sliding;
            bool hinged = !motion.hasMultipleDifferentValues && (DoorMotion)motion.intValue == DoorMotion.Hinged;
            bool managed = serializedObject.FindProperty("pageEvent").objectReferenceValue != null;

            var it = serializedObject.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                string n = it.name;
                if (n == "m_Script") continue;
                if (sliding && n == "openAngle") continue;
                if (hinged && n == "slideOffset") continue;
                string label = Labels.TryGetValue(n, out var l) ? l : it.displayName;
                EditorGUILayout.PropertyField(it, new GUIContent(label, it.tooltip), true);

                if (n == "pageEvent" && targets.Length == 1) DrawPageEventButtons((Door)target, managed);
            }

            if (sliding)
                EditorGUILayout.HelpBox("引き戸：「動く部分」に指定した物（扉の板の親）がスライド量だけ動きます。扉の幅と同じだけ横（X）にずらすのが基本です。", MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }

        void DrawPageEventButtons(Door door, bool managed)
        {
            if (managed)
            {
                EditorGUILayout.HelpBox("閉じたドアを調べると、このイベントの有効なページが実行されます（有効なページが無いときは上の鍵の設定で動きます）。開いているドアを調べると閉まります。", MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("▶ イベントエディタで開く")) EventEditorWindow.Open(door.pageEvent);
                    if (GUILayout.Button("ページ管理をやめる", GUILayout.Width(130))
                        && EditorUtility.DisplayDialog("ページ管理をやめる", "ドアとイベントのつながりを外します（イベントのオブジェクトは残ります）。", "外す", "キャンセル"))
                    {
                        Undo.RecordObject(door, "ページ管理をやめる");
                        door.pageEvent = null;
                        EditorUtility.SetDirty(door);
                    }
                }
            }
            else if (GUILayout.Button("イベントページで管理する（今の鍵の設定からページを作る）", GUILayout.Height(24)))
            {
                CreatePageEvent(door);
                GUIUtility.ExitGUI();
            }
        }

        /// <summary>今の鍵・解錠フラグの設定から、同じ動きをするイベントページを作ってドアにつなぐ。</summary>
        public static GameEvent CreatePageEvent(Door door)
        {
            var go = new GameObject("ドアのイベント");
            Undo.RegisterCreatedObjectUndo(go, "ドアのイベントを作る");
            go.transform.SetParent(door.transform, false);
            var ev = go.AddComponent<GameEvent>();
            ev.eventName = door.name + "（ドア）";
            ev.pages.Clear();

            Undo.RecordObject(door, "ドアのイベントを作る");
            bool locked = door.locked;
            bool hasKey = locked && !string.IsNullOrEmpty(door.keyItemId);
            bool hasFlag = locked && !string.IsNullOrEmpty(door.requiredFlag);

            // 鍵で開けたことを覚えておくフラグ
            string unlockedFlag = door.unlockFlag;
            if (hasKey && string.IsNullOrEmpty(unlockedFlag))
            {
                unlockedFlag = "door_" + door.name.Replace(" ", "_") + "_unlocked";
                var db = EventKitEditorUtil.EnsureDatabase();
                if (db.GetFlag(unlockedFlag) == null)
                {
                    Undo.RecordObject(db, "ドアのフラグを追加");
                    db.flags.Add(new FlagDefinition { id = unlockedFlag, description = door.name + " を鍵で開けた" });
                    EditorUtility.SetDirty(db);
                }
            }

            // ページ1：条件なし（施錠中ならメッセージ、そうでなければ開く）
            var first = new EventPage { memo = locked ? "施錠中" : "開ける", prompt = locked ? "開ける（施錠）" : "開ける" };
            if (locked)
            {
                if (door.lockedSound != null) first.commands.Add(new EventCommand { type = CommandType.PlaySound, clip = door.lockedSound, target = door.gameObject });
                first.commands.Add(new EventCommand { type = CommandType.Message, text = door.lockedMessage });
            }
            else first.commands.Add(new EventCommand { type = CommandType.OpenDoor, door = door });
            ev.pages.Add(first);

            if (hasKey)
            {
                var key = new EventPage { memo = "鍵を持っている", prompt = "開ける" };
                key.conditions.Add(new EventCondition { type = ConditionType.HasItem, id = door.keyItemId });
                if (door.unlockSound != null) key.commands.Add(new EventCommand { type = CommandType.PlaySound, clip = door.unlockSound, target = door.gameObject });
                if (door.consumeKey) key.commands.Add(new EventCommand { type = CommandType.RemoveItem, itemId = door.keyItemId, silent = true });
                key.commands.Add(new EventCommand { type = CommandType.SetFlag, flagId = unlockedFlag, boolValue = true });
                key.commands.Add(new EventCommand { type = CommandType.OpenDoor, door = door });
                key.commands.Add(new EventCommand { type = CommandType.Message, text = SafeFormat(door.unlockMessage, EventDatabase.ItemName(door.keyItemId)) });
                ev.pages.Add(key);
            }
            if (hasFlag)
            {
                var flag = new EventPage { memo = "解錠フラグがON", prompt = "開ける" };
                flag.conditions.Add(new EventCondition { type = ConditionType.FlagOn, id = door.requiredFlag });
                flag.commands.Add(new EventCommand { type = CommandType.OpenDoor, door = door });
                ev.pages.Add(flag);
            }
            if (hasKey)
            {
                var opened = new EventPage { memo = "解錠済み", prompt = "開ける" };
                opened.conditions.Add(new EventCondition { type = ConditionType.FlagOn, id = unlockedFlag });
                opened.commands.Add(new EventCommand { type = CommandType.OpenDoor, door = door });
                ev.pages.Add(opened);
            }

            // 施錠の判定はページ側で行うので、ドア自体は解錠しておく（「ドア/開ける」がそのまま効くように）
            door.locked = false;
            door.requiredFlag = "";
            door.pageEvent = ev;
            EditorUtility.SetDirty(door);
            EditorUtility.SetDirty(ev);

            EventEditorWindow.Open(ev);
            return ev;
        }

        static string SafeFormat(string format, string arg)
        {
            try { return string.Format(format ?? "", arg); }
            catch (System.FormatException) { return format; }
        }
    }
}
