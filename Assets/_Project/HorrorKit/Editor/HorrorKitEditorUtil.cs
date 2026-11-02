using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HorrorKit.EditorTools
{
    public static class HorrorKitEditorUtil
    {
        const string FallbackRoot = "Assets/_Project/HorrorKit";

        /// <summary>HorrorKit フォルダの場所（このスクリプトの位置から求めるので、フォルダを移動しても追従する）。</summary>
        public static string KitRoot
        {
            get
            {
                foreach (var guid in AssetDatabase.FindAssets("HorrorKitEditorUtil t:MonoScript"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    int editor = path.LastIndexOf("/Editor/");
                    if (path.EndsWith("/HorrorKitEditorUtil.cs") && editor > 0) return path.Substring(0, editor);
                }
                return FallbackRoot;
            }
        }

        /// <summary>HorrorKit フォルダと同じ階層の Scenes フォルダ（例: Assets/_Project/Scenes）。</summary>
        public static string ScenesFolder
        {
            get
            {
                string root = KitRoot;
                int slash = root.LastIndexOf('/');
                return (slash > 0 ? root.Substring(0, slash) : "Assets") + "/Scenes";
            }
        }

        public static string DatabasePath => KitRoot + "/Resources/HorrorDatabase.asset";

        static HorrorDatabase cached;

        public static HorrorDatabase Database
        {
            get
            {
                if (cached != null) return cached;
                var guid = AssetDatabase.FindAssets("t:HorrorDatabase").FirstOrDefault();
                if (guid != null) cached = AssetDatabase.LoadAssetAtPath<HorrorDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                return cached;
            }
        }

        public static HorrorDatabase EnsureDatabase()
        {
            if (Database != null) return Database;
            EnsureFolder(KitRoot + "/Resources");
            var db = ScriptableObject.CreateInstance<HorrorDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            AssetDatabase.SaveAssets();
            cached = db;
            return db;
        }

        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        public static T[] FindAll<T>() where T : Object =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include)
                .Where(o => !EditorUtility.IsPersistent(o))
                .ToArray();

        public static string HierarchyPath(Transform t)
        {
            var path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        // ───────── ID ドロップダウン ─────────

        public static string IdPopup(Rect rect, GUIContent label, string value, bool isItem)
        {
            var ids = new List<string> { "" };
            var names = new List<GUIContent> { new GUIContent("（未選択）") };
            var db = Database;
            if (db != null)
            {
                if (isItem)
                {
                    foreach (var i in db.items)
                    {
                        ids.Add(i.id);
                        names.Add(new GUIContent($"{i.id}  -  {i.displayName}".Replace("/", "／")));
                    }
                }
                else
                {
                    foreach (var f in db.flags)
                    {
                        ids.Add(f.id);
                        string desc = string.IsNullOrEmpty(f.description) ? "" : "  -  " + FirstLine(f.description);
                        names.Add(new GUIContent((f.id + desc).Replace("/", "／")));
                    }
                }
            }

            value = value ?? "";
            int index = ids.IndexOf(value);
            if (index < 0)
            {
                ids.Add(value);
                names.Add(new GUIContent($"⚠ {value}（未登録）"));
                index = ids.Count - 1;
            }

            if (label != null && label != GUIContent.none) rect = EditorGUI.PrefixLabel(rect, label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var old = GUI.color;
            if (string.IsNullOrEmpty(value) || names[index].text.StartsWith("⚠"))
                GUI.color = new Color(1f, 0.75f, 0.6f);
            int chosen = EditorGUI.Popup(rect, index, names.ToArray());
            GUI.color = old;
            EditorGUI.indentLevel = indent;
            return ids[chosen];
        }

        public static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int n = s.IndexOf('\n');
            return n >= 0 ? s.Substring(0, n) : s;
        }

        // ───────── 要約 ─────────

        public static string TriggerLabel(EventTrigger t)
        {
            switch (t)
            {
                case EventTrigger.Interact: return "調べる";
                case EventTrigger.EnterArea: return "エリア";
                default: return "自動";
            }
        }

        public static string ConditionLabel(EventCondition c)
        {
            switch (c.type)
            {
                case ConditionType.FlagOn: return $"{c.id}=ON";
                case ConditionType.FlagOff: return $"{c.id}=OFF";
                case ConditionType.HasItem: return $"所持:{ItemName(c.id)}";
                default: return $"未所持:{ItemName(c.id)}";
            }
        }

        public static string ItemName(string id)
        {
            var item = Database != null ? Database.GetItem(id) : null;
            return item != null ? item.displayName : id;
        }

        public static string PageSummary(EventPage p)
        {
            if (p == null) return "";
            string conds = p.conditions == null || p.conditions.Count == 0
                ? "条件なし"
                : string.Join(" & ", p.conditions.Select(ConditionLabel));
            string once = p.runOnce ? " ・1回" : "";
            string memo = string.IsNullOrEmpty(p.memo) ? "" : $"「{p.memo}」";
            return $"[{TriggerLabel(p.trigger)}] {conds} → {p.commands.Count}コマンド{once} {memo}";
        }

        // ───────── コマンドの1行表示（イベントエディタ用） ─────────

        static string Short(string s, int max = 34)
        {
            s = FirstLine(s ?? "").Trim();
            if (s.Length == 0) return "（空）";
            return s.Length > max ? s.Substring(0, max) + "…" : s;
        }

        static string Name(Object o) => o != null ? o.name : "（未設定）";

        /// <summary>start より前にある、この行を含むブロックの開始行の位置。無ければ -1。</summary>
        public static int ParentOpener(IList<EventCommand> list, int index)
        {
            int depth = 0;
            for (int i = index - 1; i >= 0; i--)
            {
                var t = list[i].type;
                if (CommandTypes.IsCloser(t)) depth++;
                else if (CommandTypes.IsOpener(t))
                {
                    if (depth == 0) return i;
                    depth--;
                }
            }
            return -1;
        }

        /// <summary>各行の字下げ（入れ子の深さ）を計算する。</summary>
        public static int[] Indents(IList<EventCommand> list)
        {
            var result = new int[list.Count];
            int depth = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i] != null ? list[i].type : CommandType.Wait;
                if (CommandTypes.IsCloser(t)) { depth = Mathf.Max(0, depth - 1); result[i] = depth; }
                else if (CommandTypes.IsDivider(t)) result[i] = Mathf.Max(0, depth - 1);
                else if (CommandTypes.IsOpener(t)) { result[i] = depth; depth++; }
                else result[i] = depth;
            }
            return result;
        }

        /// <summary>RPGツクール風の1行表示（例：「◆文章：ラジオ「……聞こえるか」」）。</summary>
        public static string CommandLine(IList<EventCommand> list, int index)
        {
            var c = list[index];
            if (c == null) return "◆";
            switch (c.type)
            {
                case CommandType.Message:
                {
                    string who = string.IsNullOrEmpty(c.speaker) ? "" : c.speaker + "：";
                    string extra = (c.clip != null ? $"　♪{c.clip.name}" : "") + (c.target != null ? $"　◎{c.target.name}へ振り向く" : "");
                    return $"◆文章：{who}{Short(c.text)}{extra}";
                }
                case CommandType.ShowChoices:
                    return "◆選択肢の表示：" + string.Join(" ／ ", c.choices ?? new List<string>());
                case CommandType.When:
                {
                    int p = ParentOpener(list, index);
                    string label = p >= 0 && list[p].choices != null && c.choiceIndex < list[p].choices.Count ? list[p].choices[c.choiceIndex] : "?";
                    return $"：［{label}］のとき";
                }
                case CommandType.EndChoices: return "：選択肢終了";
                case CommandType.If: return "◆条件分岐：" + (c.condition != null ? ConditionText(c.condition) : "?");
                case CommandType.Else: return "：それ以外のとき";
                case CommandType.EndIf: return "：分岐終了";
                case CommandType.GiveItem: return $"◆アイテム入手：{ItemName(c.itemId)}" + (c.silent ? "（通知なし）" : "");
                case CommandType.RemoveItem: return $"◆アイテムを失う：{ItemName(c.itemId)}" + (c.silent ? "（通知なし）" : "");
                case CommandType.SetFlag: return $"◆フラグ操作：{c.flagId} = {(c.boolValue ? "ON" : "OFF")}";
                case CommandType.OpenDoor: return $"◆ドアを開ける：{Name(c.door)}";
                case CommandType.CloseDoor: return $"◆ドアを閉める：{Name(c.door)}";
                case CommandType.LockDoor: return $"◆ドアを施錠：{Name(c.door)}";
                case CommandType.UnlockDoor: return $"◆ドアを解錠：{Name(c.door)}";
                case CommandType.JumpScare: return $"◆ジャンプスケア：{Name(c.jumpScare)}";
                case CommandType.PlaySound: return $"◆効果音：{Name(c.clip)}" + (c.target != null ? $"（{c.target.name}から）" : "") + (c.boolValue ? "・終わるまで待つ" : "");
                case CommandType.LookAt: return $"◆視点を向ける：{Name(c.target)}（{c.duration:0.##}秒）";
                case CommandType.FlickerFlashlight: return $"◆懐中電灯の明滅：{c.duration:0.##}秒";
                case CommandType.FadeOut: return $"◆フェードアウト：{c.duration:0.##}秒";
                case CommandType.FadeIn: return $"◆フェードイン：{c.duration:0.##}秒";
                case CommandType.Wait: return $"◆ウェイト：{c.duration:0.##}秒";
                case CommandType.SetActive: return $"◆表示切替：{Name(c.target)} を{(c.boolValue ? "表示" : "非表示")}";
                case CommandType.EndEvent: return "◆イベント処理の中断";
                case CommandType.CallEvent: return $"◆イベントの呼び出し：{(c.callEvent != null ? c.callEvent.DisplayName : "（未設定）")}";
                case CommandType.CameraView: return $"◆視点変更：{c.duration:0.##}秒で切り替え" + (c.viewFov > 0f ? $"・画角{c.viewFov:0}" : "") + WaitText(c);
                case CommandType.CameraReset: return $"◆視点変更解除：{c.duration:0.##}秒で通常視点へ" + WaitText(c);
                case CommandType.CameraShake: return $"◆カメラシェイク：{c.duration:0.##}秒・強さ{c.strength:0.#}" + WaitText(c);
                case CommandType.ActivateObject: return $"◆オブジェクトをアクティブ：{Name(c.target)}";
                case CommandType.DeactivateObject: return $"◆オブジェクトを非アクティブ：{Name(c.target)}";
                case CommandType.MoveObject: return $"◆オブジェクトを移動：{Name(c.target)} {Vec(c.vector)}m" + (c.localSpace ? "（自身の向き基準）" : "") + $"・{c.duration:0.##}秒" + WaitText(c);
                case CommandType.RotateObject:
                {
                    string mode = c.rotateMode == RotateMode.Local ? "ローカル" : c.rotateMode == RotateMode.World ? "ワールド" : "加算";
                    return $"◆オブジェクトを回転：{Name(c.target)} {mode}{Vec(c.vector)}・{c.duration:0.##}秒" + WaitText(c);
                }
                case CommandType.ScaleObject: return $"◆スケール変更：{Name(c.target)} → {Vec(c.vector)}・{c.duration:0.##}秒" + WaitText(c);
                case CommandType.InvokeUnityEvent:
                {
                    int n = c.unityEvent != null ? c.unityEvent.GetPersistentEventCount() : 0;
                    if (n == 0) return "◆UnityEventの実行：（未設定）";
                    var first = c.unityEvent.GetPersistentTarget(0);
                    string head = (first != null ? first.name : "?") + "." + c.unityEvent.GetPersistentMethodName(0);
                    return $"◆UnityEventの実行：{head}" + (n > 1 ? $" ほか{n - 1}件" : "");
                }
                default: return "◆" + c.type;
            }
        }

        static string WaitText(EventCommand c) => c.waitMode == WaitMode.Continue ? "（そのまま次へ）" : "（終了まで待機）";
        static string Vec(Vector3 v) => $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";

        public static string ConditionText(EventCondition c)
        {
            switch (c.type)
            {
                case ConditionType.FlagOn: return $"フラグ {c.id} が ON";
                case ConditionType.FlagOff: return $"フラグ {c.id} が OFF";
                case ConditionType.HasItem: return $"{ItemName(c.id)} を持っている";
                default: return $"{ItemName(c.id)} を持っていない";
            }
        }

        public static Color CommandColor(CommandType t)
        {
            bool pro = EditorGUIUtility.isProSkin;
            switch (t)
            {
                case CommandType.Message:
                case CommandType.ShowChoices:
                case CommandType.When:
                case CommandType.EndChoices: return pro ? new Color(0.55f, 0.8f, 1f) : new Color(0.05f, 0.3f, 0.65f);
                case CommandType.If:
                case CommandType.Else:
                case CommandType.EndIf:
                case CommandType.CallEvent:
                case CommandType.EndEvent:
                case CommandType.InvokeUnityEvent:
                case CommandType.Wait: return pro ? new Color(1f, 0.75f, 0.45f) : new Color(0.6f, 0.3f, 0f);
                case CommandType.GiveItem:
                case CommandType.RemoveItem:
                case CommandType.SetFlag: return pro ? new Color(0.6f, 0.95f, 0.6f) : new Color(0.1f, 0.45f, 0.1f);
                case CommandType.JumpScare:
                case CommandType.PlaySound:
                case CommandType.LookAt:
                case CommandType.FlickerFlashlight:
                case CommandType.FadeIn:
                case CommandType.FadeOut:
                case CommandType.CameraView:
                case CommandType.CameraReset:
                case CommandType.CameraShake: return pro ? new Color(1f, 0.55f, 0.55f) : new Color(0.65f, 0.1f, 0.1f);
                case CommandType.ActivateObject:
                case CommandType.DeactivateObject:
                case CommandType.MoveObject:
                case CommandType.RotateObject:
                case CommandType.ScaleObject: return pro ? new Color(0.95f, 0.8f, 0.6f) : new Color(0.45f, 0.3f, 0.1f);
                default: return pro ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.15f, 0.15f, 0.15f);
            }
        }

        // ───────── 参照検索 ─────────

        /// <summary>シーン内でIDを参照している箇所（オブジェクト）を集める。</summary>
        public static Dictionary<string, List<Object>> CollectReferences(bool items)
        {
            var result = new Dictionary<string, List<Object>>();
            void Add(string id, Object o)
            {
                if (string.IsNullOrEmpty(id)) return;
                if (!result.TryGetValue(id, out var list)) result[id] = list = new List<Object>();
                if (!list.Contains(o)) list.Add(o);
            }

            foreach (var ev in FindAll<HorrorEvent>())
            {
                foreach (var p in ev.pages)
                {
                    if (p == null) continue;
                    foreach (var c in p.conditions)
                        if (c != null && c.IsItemCondition == items) Add(c.id, ev.gameObject);
                    foreach (var c in p.commands)
                    {
                        if (c == null) continue;
                        if (items && c.IsItemCommand) Add(c.itemId, ev.gameObject);
                        if (!items && c.type == CommandType.SetFlag) Add(c.flagId, ev.gameObject);
                        if (c.type == CommandType.If && c.condition != null && c.condition.IsItemCondition == items)
                            Add(c.condition.id, ev.gameObject);
                    }
                }
            }
            foreach (var d in FindAll<Door>())
                Add(items ? d.keyItemId : d.unlockFlag, d.gameObject);
            if (items)
                foreach (var f in FindAll<Flashlight>())
                    Add(f.requireItem ? f.requiredItemId : null, f.gameObject);
            return result;
        }

        /// <summary>シーン内のID参照を一括置換する。戻り値は置換した箇所数。</summary>
        public static int ReplaceIdInScene(string from, string to, bool items)
        {
            if (string.IsNullOrEmpty(from) || from == to) return 0;
            int count = 0;
            foreach (var ev in FindAll<HorrorEvent>())
            {
                Undo.RecordObject(ev, "HorrorKit ID置換");
                bool changed = false;
                foreach (var p in ev.pages)
                {
                    foreach (var c in p.conditions)
                        if (c.IsItemCondition == items && c.id == from) { c.id = to; count++; changed = true; }
                    foreach (var c in p.commands)
                    {
                        if (items && c.IsItemCommand && c.itemId == from) { c.itemId = to; count++; changed = true; }
                        if (!items && c.type == CommandType.SetFlag && c.flagId == from) { c.flagId = to; count++; changed = true; }
                        if (c.type == CommandType.If && c.condition != null && c.condition.IsItemCondition == items && c.condition.id == from)
                        { c.condition.id = to; count++; changed = true; }
                    }
                }
                if (changed) EditorUtility.SetDirty(ev);
            }
            foreach (var d in FindAll<Door>())
            {
                Undo.RecordObject(d, "HorrorKit ID置換");
                if (items && d.keyItemId == from) { d.keyItemId = to; count++; EditorUtility.SetDirty(d); }
                if (!items && d.unlockFlag == from) { d.unlockFlag = to; count++; EditorUtility.SetDirty(d); }
            }
            if (items)
            {
                foreach (var f in FindAll<Flashlight>())
                {
                    if (f.requiredItemId != from) continue;
                    Undo.RecordObject(f, "HorrorKit ID置換");
                    f.requiredItemId = to;
                    count++;
                    EditorUtility.SetDirty(f);
                }
            }
            return count;
        }
    }
}
