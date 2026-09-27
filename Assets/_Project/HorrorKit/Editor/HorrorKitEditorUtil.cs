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
