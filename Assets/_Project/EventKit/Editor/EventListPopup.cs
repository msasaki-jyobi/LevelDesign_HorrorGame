using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EventKit.EditorTools
{
    /// <summary>
    /// イベントエディタの「イベント一覧」ポップアップ。
    /// アクティブシーン：開いているシーンの GameEvent をそのまま並べる。
    /// 全体：Assets 内の全シーンを対象にする（開いていないシーンはシーンファイルを読んで一覧化し、選ぶとそのシーンを開く）。
    /// </summary>
    public class EventListPopup : PopupWindowContent
    {
        enum Scope { ActiveScene, All }

        class Entry
        {
            public string scenePath;
            public string label;       // イベント名
            public string objectName;  // GameObject 名
            public int pages;
            public GameEvent live;   // 読み込み済みシーンのとき
            public ulong fileId;       // 未読み込みシーンのとき（GlobalObjectId で開いた後に探す）
        }

        const string ScopePref = "EventKit.EventList.Scope";

        readonly GameEvent current;
        readonly Action<GameEvent> onPick;
        Scope scope;
        string search = "";
        Vector2 scroll;
        List<Entry> entries;
        readonly HashSet<string> collapsed = new HashSet<string>();

        static GUIStyle itemStyle, itemSelectedStyle;

        public EventListPopup(GameEvent current, Action<GameEvent> onPick)
        {
            this.current = current;
            this.onPick = onPick;
            scope = (Scope)EditorPrefs.GetInt(ScopePref, 0);
        }

        public override Vector2 GetWindowSize() => new Vector2(440, 480);

        public override void OnOpen() => Reload();

        void Reload() => entries = scope == Scope.ActiveScene ? CollectActiveScene() : CollectAll();

        public override void OnGUI(Rect rect)
        {
            itemStyle ??= new GUIStyle(EditorStyles.label) { richText = true, padding = new RectOffset(18, 4, 2, 2) };
            itemSelectedStyle ??= new GUIStyle(itemStyle) { fontStyle = FontStyle.Bold };

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var newScope = (Scope)GUILayout.Toolbar((int)scope, new[] { "アクティブシーン", "全体" }, EditorStyles.toolbarButton, GUILayout.Width(220));
                if (newScope != scope)
                {
                    scope = newScope;
                    EditorPrefs.SetInt(ScopePref, (int)scope);
                    Reload();
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("再読み込み", EditorStyles.toolbarButton)) Reload();
            }
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            string lastScene = null;
            int shown = 0;
            foreach (var e in entries)
            {
                if (!Matches(e)) continue;
                if (e.scenePath != lastScene)
                {
                    lastScene = e.scenePath;
                    bool open = !collapsed.Contains(e.scenePath);
                    bool loaded = SceneManager.GetSceneByPath(e.scenePath).isLoaded;
                    string title = Path.GetFileNameWithoutExtension(e.scenePath) + (loaded ? "" : "（未読み込み）");
                    bool newOpen = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
                    if (newOpen != open) { if (newOpen) collapsed.Remove(e.scenePath); else collapsed.Add(e.scenePath); }
                }
                if (collapsed.Contains(e.scenePath)) continue;

                shown++;
                bool isCurrent = e.live != null && e.live == current;
                string text = $"{(isCurrent ? "▶ " : "")}{e.label}  <color=#888888>{e.objectName}・{e.pages}ページ</color>";
                var r = GUILayoutUtility.GetRect(new GUIContent(text), itemStyle, GUILayout.ExpandWidth(true));
                if (isCurrent) EditorGUI.DrawRect(r, new Color(0.24f, 0.48f, 0.9f, 0.35f));
                else if (r.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(r, new Color(1f, 1f, 1f, 0.06f));
                GUI.Label(r, text, isCurrent ? itemSelectedStyle : itemStyle);
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    Pick(e);
                    GUIUtility.ExitGUI();
                }
            }
            if (shown == 0 && string.IsNullOrEmpty(lastScene))
                EditorGUILayout.LabelField(entries.Count == 0 ? "イベントがありません" : "一致するイベントがありません", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndScrollView();

            if (scope == Scope.All)
                EditorGUILayout.LabelField("未読み込みのシーンのイベントを選ぶと、そのシーンを開きます。", EditorStyles.miniLabel);

            if (Event.current.type == EventType.MouseMove) editorWindow.Repaint();
        }

        bool Matches(Entry e)
        {
            if (string.IsNullOrEmpty(search)) return true;
            return e.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || e.objectName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void Pick(Entry e)
        {
            var ev = e.live;
            if (ev == null)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                var scene = EditorSceneManager.OpenScene(e.scenePath, OpenSceneMode.Single);
                var id = $"GlobalObjectId_V1-2-{AssetDatabase.AssetPathToGUID(e.scenePath)}-{e.fileId}-0";
                if (GlobalObjectId.TryParse(id, out var gid))
                    ev = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as GameEvent;
                if (ev == null) ev = FindByName(scene, e.objectName);
            }
            editorWindow.Close();
            if (ev == null) return;
            Selection.activeGameObject = ev.gameObject;
            EditorGUIUtility.PingObject(ev.gameObject);
            onPick(ev);
        }

        static GameEvent FindByName(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var ev in root.GetComponentsInChildren<GameEvent>(true))
                    if (ev.name == objectName) return ev;
            return null;
        }

        // ───────── 収集 ─────────

        static Entry FromLive(GameEvent ev) => new Entry
        {
            scenePath = ev.gameObject.scene.path,
            label = ev.DisplayName,
            objectName = ev.name,
            pages = ev.pages.Count,
            live = ev,
        };

        static List<Entry> CollectLoaded(Func<Scene, bool> filter)
        {
            var list = new List<Entry>();
            foreach (var ev in UnityEngine.Object.FindObjectsByType<GameEvent>(FindObjectsInactive.Include))
            {
                if (EditorUtility.IsPersistent(ev) || !filter(ev.gameObject.scene)) continue;
                list.Add(FromLive(ev));
            }
            list.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.Ordinal));
            return list;
        }

        static List<Entry> CollectActiveScene()
        {
            var active = SceneManager.GetActiveScene();
            return CollectLoaded(s => s == active);
        }

        static List<Entry> CollectAll()
        {
            // 読み込み済みのシーンは実物から、それ以外はシーンファイルから
            var result = CollectLoaded(s => true);
            var scriptGuid = GameEventScriptGuid();
            if (string.IsNullOrEmpty(scriptGuid)) return result;

            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);

            foreach (var path in paths)
            {
                if (SceneManager.GetSceneByPath(path).isLoaded) continue;
                var found = ParseScene(path, scriptGuid);
                found.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.Ordinal));
                result.AddRange(found);
            }
            result.Sort((a, b) =>
            {
                int c = string.Compare(a.scenePath, b.scenePath, StringComparison.Ordinal);
                return c != 0 ? c : string.Compare(a.label, b.label, StringComparison.Ordinal);
            });
            return result;
        }

        static string GameEventScriptGuid()
        {
            foreach (var guid in AssetDatabase.FindAssets("GameEvent t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == typeof(GameEvent)) return guid;
            }
            return null;
        }

        static readonly Regex DocHeader = new Regex(@"^--- !u!(\d+) &(-?\d+)", RegexOptions.Multiline);

        /// <summary>シーンファイル(YAML)から GameEvent を探す。プレハブ内のイベントは対象外。</summary>
        static List<Entry> ParseScene(string path, string scriptGuid)
        {
            var list = new List<Entry>();
            string text;
            try { text = File.ReadAllText(path); }
            catch { return list; }
            if (!text.Contains(scriptGuid)) return list;

            var goNames = new Dictionary<string, string>();
            var events = new List<(string fileId, string goId, string name, int pages)>();
            var headers = DocHeader.Matches(text);
            for (int i = 0; i < headers.Count; i++)
            {
                int start = headers[i].Index;
                int end = i + 1 < headers.Count ? headers[i + 1].Index : text.Length;
                var body = text.Substring(start, end - start);
                string classId = headers[i].Groups[1].Value, fileId = headers[i].Groups[2].Value;

                if (classId == "1")
                {
                    var m = Regex.Match(body, @"^  m_Name: (.*)$", RegexOptions.Multiline);
                    if (m.Success) goNames[fileId] = Unquote(m.Groups[1].Value);
                }
                else if (classId == "114" && body.Contains("guid: " + scriptGuid))
                {
                    var go = Regex.Match(body, @"m_GameObject: \{fileID: (-?\d+)\}");
                    var name = Regex.Match(body, @"^  eventName: (.*)$", RegexOptions.Multiline);
                    int pages = Regex.Matches(body, @"^  - memo:", RegexOptions.Multiline).Count;
                    events.Add((fileId, go.Success ? go.Groups[1].Value : "", name.Success ? Unquote(name.Groups[1].Value) : "", pages));
                }
            }

            foreach (var e in events)
            {
                goNames.TryGetValue(e.goId, out var goName);
                goName ??= "?";
                ulong.TryParse(e.fileId, out var fid);
                list.Add(new Entry
                {
                    scenePath = path,
                    label = string.IsNullOrEmpty(e.name) ? goName : e.name,
                    objectName = goName,
                    pages = e.pages,
                    fileId = fid,
                });
            }
            return list;
        }

        static string Unquote(string v)
        {
            v = v.Trim();
            if (v.Length >= 2 && v[0] == '"' && v[v.Length - 1] == '"')
            {
                v = v.Substring(1, v.Length - 2);
                v = Regex.Replace(v, @"\\u([0-9A-Fa-f]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
                v = v.Replace("\\\"", "\"").Replace("\\\\", "\\");
            }
            else if (v.Length >= 2 && v[0] == '\'' && v[v.Length - 1] == '\'')
                v = v.Substring(1, v.Length - 2).Replace("''", "'");
            return v;
        }
    }
}
