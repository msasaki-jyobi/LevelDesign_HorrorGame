using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EventKit.EditorTools
{
    /// <summary>
    /// デバッグ用ウィンドウ。フラグの ON/OFF（緑・赤）、所持品の出し入れ、イベントの実行、変化の記録。
    /// 再生前は「開始時の状態」を設定でき、再生を始めるとその状態から始まる。
    /// </summary>
    [InitializeOnLoad]
    public class EventDebugWindow : EditorWindow
    {
        [MenuItem("EventKit/デバッグ", priority = 3)]
        public static void Open()
        {
            var w = GetWindow<EventDebugWindow>();
            w.titleContent = new GUIContent("EventKit デバッグ");
            w.minSize = new Vector2(360, 300);
            w.Show();
        }

        // ───────── 開始時の状態（EditorPrefs に保存） ─────────

        [Serializable]
        class StartState
        {
            public List<string> flagsOn = new List<string>();
            public List<string> flagsOff = new List<string>();
            public List<string> items = new List<string>();
            public bool IsEmpty => flagsOn.Count == 0 && flagsOff.Count == 0 && items.Count == 0;
        }

        static string PrefKey => "EventKit.DebugStart." + Application.dataPath.GetHashCode();
        const string NotifyPref = "EventKit.DebugNotify";

        static StartState start;
        static StartState Start
        {
            get
            {
                if (start != null) return start;
                start = new StartState();
                var json = EditorPrefs.GetString(PrefKey, "");
                if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, start);
                return start;
            }
        }
        static void SaveStart() => EditorPrefs.SetString(PrefKey, JsonUtility.ToJson(Start));

        // ───────── 変化の記録 ─────────

        struct LogEntry { public string time, text; public Color color; }
        static readonly List<LogEntry> log = new List<LogEntry>();
        const int MaxLog = 200;

        static readonly Color On = new Color(0.25f, 0.72f, 0.35f);
        static readonly Color Off = new Color(0.82f, 0.3f, 0.28f);
        static readonly Color Gray = new Color(0.45f, 0.45f, 0.45f);
        static readonly Color ItemColor = new Color(0.9f, 0.72f, 0.2f);

        static EventDebugWindow()
        {
            EventDebugStart.Provider = ApplyStart;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.ExitingEditMode) EventDebugStart.Provider = ApplyStart;
                if (s == PlayModeStateChange.EnteredPlayMode)
                {
                    log.Clear();
                    GameState.FlagChanged += (id, v) => AddLog($"フラグ {id} → {(v ? "ON" : "OFF")}", v ? On : Off);
                    GameState.ItemAdded += id => AddLog($"アイテム入手：{EventDatabase.ItemName(id)}", ItemColor);
                    GameState.ItemRemoved += id => AddLog($"アイテム消失：{EventDatabase.ItemName(id)}", Gray);
                }
            };
        }

        static void ApplyStart()
        {
            EventDebugNotifier.Enabled = EditorPrefs.GetBool(NotifyPref, true);
            var s = Start;
            if (s.IsEmpty) return;
            foreach (var f in s.flagsOn) GameState.SetFlag(f, true);
            foreach (var f in s.flagsOff) GameState.SetFlag(f, false);
            foreach (var i in s.items) GameState.AddItem(i);
            Debug.Log($"[EventKit デバッグ] 開始時の状態を適用：フラグON {s.flagsOn.Count}・OFF {s.flagsOff.Count}・アイテム {s.items.Count}");
        }

        static void AddLog(string text, Color color)
        {
            log.Add(new LogEntry { time = Time.time.ToString("0.0") + "s", text = text, color = color });
            if (log.Count > MaxLog) log.RemoveAt(0);
            foreach (var w in Resources.FindObjectsOfTypeAll<EventDebugWindow>()) w.Repaint();
        }

        // ───────── 画面 ─────────

        enum Tab { Flags, Items, Events, Log }
        static readonly string[] TabNames = { "フラグ", "アイテム", "イベント", "記録" };
        [SerializeField] Tab tab;
        [SerializeField] string search = "";
        [SerializeField] int flagFilter; // 0=すべて 1=ONのみ 2=OFFのみ
        Vector2 scroll;
        static GUIStyle pill, rowLabel, desc;

        void OnEnable() => EditorApplication.playModeStateChanged += Repaint;
        void OnDisable() => EditorApplication.playModeStateChanged -= Repaint;
        void Repaint(PlayModeStateChange _) => Repaint();
        void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

        void OnGUI()
        {
            pill ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, hover = { textColor = Color.white }, active = { textColor = Color.white } };
            rowLabel ??= new GUIStyle(EditorStyles.label) { richText = true };
            desc ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.55f, 0.55f, 0.55f) } };

            var db = EventKitEditorUtil.Database;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                tab = (Tab)GUILayout.Toolbar((int)tab, TabNames, EditorStyles.toolbarButton, GUILayout.Width(260));
                GUILayout.FlexibleSpace();
                bool notify = EditorPrefs.GetBool(NotifyPref, true);
                bool nn = GUILayout.Toggle(notify, new GUIContent("画面に通知", "フラグ・所持品が変わったとき、ゲーム画面の下に通知を出す"), EditorStyles.toolbarButton);
                if (nn != notify) { EditorPrefs.SetBool(NotifyPref, nn); EventDebugNotifier.Enabled = nn; }
            }

            bool playing = Application.isPlaying;
            if (tab == Tab.Flags || tab == Tab.Items)
            {
                if (playing) EditorGUILayout.HelpBox("再生中：クリックでその場で切り替わります。", MessageType.None);
                else EditorGUILayout.HelpBox("再生前：ここで決めた「開始時の状態」で再生が始まります（灰色＝既定のまま）。クリックで 既定 → ON → OFF と切り替わります。", MessageType.None);
            }
            if (tab != Tab.Log) search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);

            if (db == null && tab != Tab.Log && tab != Tab.Events)
            {
                EditorGUILayout.HelpBox("EventDatabase がありません（EventKit > マネージャー で作成）。", MessageType.Warning);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case Tab.Flags: DrawFlags(db, playing); break;
                case Tab.Items: DrawItems(db, playing); break;
                case Tab.Events: DrawEvents(playing); break;
                case Tab.Log: DrawLog(); break;
            }
            EditorGUILayout.EndScrollView();

            DrawFooter(playing);
        }

        bool Match(params string[] texts)
        {
            if (string.IsNullOrEmpty(search)) return true;
            return texts.Any(t => !string.IsNullOrEmpty(t) && t.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static bool Pill(string text, Color color, float width)
        {
            var r = GUILayoutUtility.GetRect(width, 20, GUILayout.Width(width), GUILayout.Height(20));
            EditorGUI.DrawRect(r, color);
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            return GUI.Button(r, text, pill);
        }

        // ───────── フラグ ─────────

        void DrawFlags(EventDatabase db, bool playing)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                flagFilter = GUILayout.Toolbar(flagFilter, new[] { "すべて", "ONのみ", "OFFのみ" }, EditorStyles.miniButton, GUILayout.Width(200));
                GUILayout.FlexibleSpace();
                if (playing)
                {
                    if (GUILayout.Button("全部ON", EditorStyles.miniButtonLeft)) foreach (var f in db.flags) GameState.SetFlag(f.id, true);
                    if (GUILayout.Button("全部OFF", EditorStyles.miniButtonRight)) foreach (var f in db.flags) GameState.SetFlag(f.id, false);
                }
                else if (GUILayout.Button("すべて既定に戻す", EditorStyles.miniButton))
                {
                    Start.flagsOn.Clear(); Start.flagsOff.Clear(); SaveStart();
                }
            }
            EditorGUILayout.Space(2);

            var ids = db.flags.Select(f => f.id).ToList();
            if (playing) ids.AddRange(GameState.Flags.Keys.Where(k => !ids.Contains(k)));
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var def = db.GetFlag(id);
                string description = def != null ? EventKitEditorUtil.FirstLine(def.description) : "（未登録）";
                if (!Match(id, description)) continue;

                bool value;
                int state = 0; // 再生前：0=既定 1=ON 2=OFF
                if (playing) value = GameState.GetFlag(id);
                else
                {
                    state = Start.flagsOn.Contains(id) ? 1 : Start.flagsOff.Contains(id) ? 2 : 0;
                    value = state == 1 || (state == 0 && def != null && def.defaultValue);
                }
                if (flagFilter == 1 && !value || flagFilter == 2 && value) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    Color color = !playing && state == 0 ? Gray : value ? On : Off;
                    string text = value ? "ON" : "OFF";
                    if (Pill(text, color, 48))
                    {
                        if (playing) GameState.SetFlag(id, !value);
                        else
                        {
                            Start.flagsOn.Remove(id);
                            Start.flagsOff.Remove(id);
                            if (state == 0) Start.flagsOn.Add(id);
                            else if (state == 1) Start.flagsOff.Add(id);
                            SaveStart();
                        }
                    }
                    GUILayout.Space(4);
                    GUILayout.Label(id, rowLabel, GUILayout.MinWidth(80));
                    GUILayout.Label(description, desc);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        // ───────── アイテム ─────────

        void DrawItems(EventDatabase db, bool playing)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("全部所持", EditorStyles.miniButtonLeft))
                {
                    if (playing) foreach (var i in db.items) GameState.AddItem(i.id);
                    else { Start.items = db.items.Select(i => i.id).ToList(); SaveStart(); }
                }
                if (GUILayout.Button("全部外す", EditorStyles.miniButtonRight))
                {
                    if (playing) foreach (var i in db.items) GameState.RemoveItem(i.id);
                    else { Start.items.Clear(); SaveStart(); }
                }
            }
            EditorGUILayout.Space(2);

            foreach (var item in db.items)
            {
                if (!Match(item.id, item.displayName)) continue;
                bool has = playing ? GameState.HasItem(item.id) : Start.items.Contains(item.id);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (Pill(has ? "所持" : "未所持", has ? On : Gray, 56))
                    {
                        if (playing) { if (has) GameState.RemoveItem(item.id); else GameState.AddItem(item.id); }
                        else { if (has) Start.items.Remove(item.id); else Start.items.Add(item.id); SaveStart(); }
                    }
                    GUILayout.Space(4);
                    if (item.icon != null)
                    {
                        var r = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
                        GUI.DrawTexture(r, item.icon.texture, ScaleMode.ScaleToFit);
                    }
                    GUILayout.Label(item.displayName, rowLabel, GUILayout.MinWidth(80));
                    GUILayout.Label(item.id, desc);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        // ───────── イベント ─────────

        void DrawEvents(bool playing)
        {
            foreach (var ev in EventKitEditorUtil.FindAll<GameEvent>().OrderBy(e => EventKitEditorUtil.HierarchyPath(e.transform)))
            {
                if (!Match(ev.DisplayName, ev.name)) continue;
                int a = ev.ActivePageIndex;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Color c = a >= 0 ? On : Gray;
                        var r = GUILayoutUtility.GetRect(8, 18, GUILayout.Width(8));
                        EditorGUI.DrawRect(new Rect(r.x, r.y + 5, 8, 8), c);
                        GUILayout.Label(ev.DisplayName, EditorStyles.boldLabel);
                        GUILayout.Label(a >= 0 ? $"有効：ページ {a + 1}" : "有効なページなし", desc);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("編集", EditorStyles.miniButtonLeft, GUILayout.Width(40))) EventEditorWindow.Open(ev, Mathf.Max(0, a));
                        using (new EditorGUI.DisabledScope(!playing || a < 0 || EventRunner.IsBusy))
                            if (GUILayout.Button("実行", EditorStyles.miniButtonRight, GUILayout.Width(40))) ev.RunActivePage();
                    }
                    if (!playing) continue;
                    // 1回だけのページの実行済みを戻す
                    for (int i = 0; i < ev.pages.Count; i++)
                    {
                        var p = ev.pages[i];
                        bool once = p.runOnce || p.trigger == EventTrigger.AutoStart;
                        if (!once || !GameState.IsPageFinished(ev.PageKey(i))) continue;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label($"   ページ {i + 1}：実行済み（1回だけ）", desc);
                            if (GUILayout.Button("未実行に戻す", EditorStyles.miniButton, GUILayout.Width(90))) GameState.UnmarkPageFinished(ev.PageKey(i));
                        }
                    }
                }
            }
        }

        // ───────── 記録 ─────────

        void DrawLog()
        {
            if (log.Count == 0)
            {
                EditorGUILayout.LabelField(Application.isPlaying ? "まだ変化はありません" : "再生中のフラグ・所持品の変化がここに記録されます", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            for (int i = log.Count - 1; i >= 0; i--)
            {
                var e = log[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    var r = GUILayoutUtility.GetRect(4, 16, GUILayout.Width(4));
                    EditorGUI.DrawRect(r, e.color);
                    GUILayout.Label(e.time, desc, GUILayout.Width(48));
                    GUILayout.Label(e.text);
                }
            }
        }

        void DrawFooter(bool playing)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (playing)
                {
                    GUILayout.Label(EventRunner.IsBusy ? "● イベント実行中" : "○ 待機中", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (tab == Tab.Log && GUILayout.Button("記録を消す", EditorStyles.toolbarButton)) log.Clear();
                    if (GUILayout.Button("フラグ・所持品・実行済みをリセット", EditorStyles.toolbarButton)) GameState.ResetAll();
                }
                else
                {
                    var s = Start;
                    GUILayout.Label(s.IsEmpty ? "開始時の状態：既定のまま" : $"開始時の状態：フラグON {s.flagsOn.Count}・OFF {s.flagsOff.Count}・アイテム {s.items.Count}", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(s.IsEmpty))
                        if (GUILayout.Button("開始時の状態をクリア", EditorStyles.toolbarButton))
                        {
                            s.flagsOn.Clear(); s.flagsOff.Clear(); s.items.Clear(); SaveStart();
                        }
                }
            }
        }
    }
}
