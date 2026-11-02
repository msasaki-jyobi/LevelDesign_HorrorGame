using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EventKit.EditorTools
{
    public static class EventKitEditorUtil
    {
        const string FallbackRoot = "Assets/_Project/EventKit";

        /// <summary>EventKit フォルダの場所（このスクリプトの位置から求めるので、フォルダを移動しても追従する）。</summary>
        public static string KitRoot
        {
            get
            {
                foreach (var guid in AssetDatabase.FindAssets("EventKitEditorUtil t:MonoScript"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    int editor = path.LastIndexOf("/Editor/");
                    if (path.EndsWith("/EventKitEditorUtil.cs") && editor > 0) return path.Substring(0, editor);
                }
                return FallbackRoot;
            }
        }

        /// <summary>EventKit フォルダと同じ階層の Scenes フォルダ（例: Assets/_Project/Scenes）。</summary>
        public static string ScenesFolder
        {
            get
            {
                string root = KitRoot;
                int slash = root.LastIndexOf('/');
                return (slash > 0 ? root.Substring(0, slash) : "Assets") + "/Scenes";
            }
        }

        public static string DatabasePath => KitRoot + "/Resources/EventDatabase.asset";

        /// <summary>複製した UI プレハブの置き場所（EventKit の外。例: Assets/_Project/UI）</summary>
        public static string ProjectUIFolder
        {
            get
            {
                string root = KitRoot;
                int slash = root.LastIndexOf('/');
                return (slash > 0 ? root.Substring(0, slash) : "Assets") + "/UI";
            }
        }

        static EventDatabase cached;

        public static EventDatabase Database
        {
            get
            {
                if (cached != null) return cached;
                var guid = AssetDatabase.FindAssets("t:EventDatabase").FirstOrDefault();
                if (guid != null) cached = AssetDatabase.LoadAssetAtPath<EventDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                return cached;
            }
        }

        public static EventDatabase EnsureDatabase()
        {
            if (Database != null) return Database;
            EnsureFolder(KitRoot + "/Resources");
            var db = ScriptableObject.CreateInstance<EventDatabase>();
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
                case CommandType.JumpScare: return $"◆演出オブジェクトを再生：{Name(c.jumpScare)}";
                case CommandType.PlaySound: return $"◆効果音：{Name(c.clip)}" + (c.target != null ? $"（{c.target.name}から）" : "") + (c.boolValue ? "・終わるまで待つ" : "");
                case CommandType.LookAt: return $"◆視点を向ける：{Name(c.target)}（{c.duration:0.##}秒）";
                case CommandType.FlickerFlashlight: return $"◆懐中電灯の明滅：{c.duration:0.##}秒";
                case CommandType.FadeOut: return $"◆フェードアウト：{c.duration:0.##}秒（画面が暗くなる）";
                case CommandType.FadeIn: return $"◆フェードイン：{c.duration:0.##}秒（画面が明るくなる）";
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
                case CommandType.TransferScene:
                case CommandType.TransferSceneLoading:
                {
                    string scene = string.IsNullOrEmpty(c.scenePath) ? "（未設定）" : System.IO.Path.GetFileNameWithoutExtension(c.scenePath);
                    string spawn = string.IsNullOrEmpty(c.spawnId) ? "初期位置" : c.spawnId;
                    string head = c.type == CommandType.TransferScene ? "◆シーン遷移" : "◆ロード画面付きシーン遷移";
                    return $"{head}：{scene}［{spawn}］";
                }
                case CommandType.PlayAnimation:
                {
                    string what = c.animMode == AnimationPlayMode.Clip ? Name(c.animClip)
                        : (c.animMode == AnimationPlayMode.Trigger ? "トリガー " : "") + (string.IsNullOrEmpty(c.animName) ? "（未設定）" : c.animName);
                    return $"◆モーション再生：{Name(c.target)}「{what}」" + WaitText(c);
                }
                case CommandType.Custom:
                {
                    if (c.custom == null) return "◆拡張コマンド：（未設定）";
                    try { return "◆" + c.custom.Summary; }
                    catch (System.Exception) { return "◆" + c.custom.GetType().Name; }
                }
                case CommandType.Warp: return $"◆シーン内ワープ：{Name(c.target)}" + (c.boolValue ? "（フェードあり）" : "（瞬時）");
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
                case CommandType.TransferScene:
                case CommandType.TransferSceneLoading:
                case CommandType.Warp:
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
                case CommandType.PlayAnimation:
                case CommandType.ScaleObject: return pro ? new Color(0.95f, 0.8f, 0.6f) : new Color(0.45f, 0.3f, 0.1f);
                case CommandType.Custom: return pro ? new Color(0.85f, 0.7f, 1f) : new Color(0.4f, 0.15f, 0.6f);
                default: return pro ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.15f, 0.15f, 0.15f);
            }
        }

        // ───────── 拡張コマンド ─────────

        static List<(System.Type type, string path)> customTypes;

        /// <summary>プロジェクト内の拡張コマンド（CustomCommand の派生クラス）と挿入メニュー上の場所。</summary>
        public static List<(System.Type type, string path)> CustomCommandTypes
        {
            get
            {
                if (customTypes != null) return customTypes;
                customTypes = new List<(System.Type, string)>();
                foreach (var t in TypeCache.GetTypesDerivedFrom<CustomCommand>())
                {
                    if (t.IsAbstract || t.IsGenericType || t.GetConstructor(System.Type.EmptyTypes) == null) continue;
                    var attr = (EventCommandAttribute)System.Attribute.GetCustomAttribute(t, typeof(EventCommandAttribute));
                    string path = attr != null && !string.IsNullOrEmpty(attr.Path) ? attr.Path : "拡張/" + t.Name;
                    customTypes.Add((t, path));
                }
                customTypes.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
                return customTypes;
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

            foreach (var ev in FindAll<GameEvent>())
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
            // ドアなど、[ItemId]/[FlagId] の付いたフィールドを持つコンポーネント（追加パックの物も含む）
            foreach (var (component, field) in IdFields(items))
                Add(field.GetValue(component) as string, component.gameObject);
            return result;
        }

        /// <summary>シーン内のID参照を一括置換する。戻り値は置換した箇所数。</summary>
        public static int ReplaceIdInScene(string from, string to, bool items)
        {
            if (string.IsNullOrEmpty(from) || from == to) return 0;
            int count = 0;
            foreach (var ev in FindAll<GameEvent>())
            {
                Undo.RecordObject(ev, "EventKit ID置換");
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
            foreach (var (component, field) in IdFields(items))
            {
                if (field.GetValue(component) as string != from) continue;
                Undo.RecordObject(component, "EventKit ID置換");
                field.SetValue(component, to);
                count++;
                EditorUtility.SetDirty(component);
            }
            return count;
        }

        /// <summary>シーン内の MonoBehaviour で、[ItemId]（items=true）/[FlagId] が付いた string フィールドを列挙する。</summary>
        static IEnumerable<(MonoBehaviour, System.Reflection.FieldInfo)> IdFields(bool items)
        {
            var attr = items ? typeof(ItemIdAttribute) : typeof(FlagIdAttribute);
            var cache = new Dictionary<System.Type, List<System.Reflection.FieldInfo>>();
            foreach (var mb in FindAll<MonoBehaviour>())
            {
                if (mb == null || mb is GameEvent) continue;
                var type = mb.GetType();
                if (!cache.TryGetValue(type, out var fields))
                {
                    fields = new List<System.Reflection.FieldInfo>();
                    for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                        foreach (var fi in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                            if (fi.FieldType == typeof(string) && System.Attribute.IsDefined(fi, attr)) fields.Add(fi);
                    cache[type] = fields;
                }
                foreach (var fi in fields) yield return (mb, fi);
            }
        }
    }
}
