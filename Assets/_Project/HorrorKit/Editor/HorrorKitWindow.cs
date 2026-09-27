using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace HorrorKit.EditorTools
{
    /// <summary>
    /// アイテム・フラグ・イベント・会話テキストを一括管理するウィンドウ。
    /// メニュー: HorrorKit > マネージャー（Ctrl+Shift+H）
    /// </summary>
    public class HorrorKitWindow : EditorWindow
    {
        [MenuItem("HorrorKit/マネージャー %#h", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<HorrorKitWindow>();
            w.titleContent = new GUIContent("HorrorKit");
            w.minSize = new Vector2(640, 420);
        }

        static readonly string[] Tabs = { "アイテム", "フラグ", "イベント", "会話テキスト", "デバッグ" };

        [SerializeField] int tab;
        HorrorDatabase db;
        SerializedObject dbSO;
        ReorderableList itemList, flagList;
        Vector2 scrollA, scrollB;
        string search = "";
        int filterIndex;
        HorrorEvent selected;
        Editor selectedEditor;
        string replaceFrom = "", replaceTo = "";
        Dictionary<string, List<Object>> refs;
        readonly Dictionary<Object, SerializedObject> textSOs = new Dictionary<Object, SerializedObject>();

        static GUIStyle wrapArea, selectedBox;
        static GUIStyle WrapArea => wrapArea ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        static GUIStyle SelectedBox
        {
            get
            {
                if (selectedBox == null)
                {
                    selectedBox = new GUIStyle(EditorStyles.helpBox);
                    var tex = new Texture2D(1, 1);
                    tex.SetPixel(0, 0, EditorGUIUtility.isProSkin ? new Color(0.17f, 0.36f, 0.53f) : new Color(0.6f, 0.78f, 1f));
                    tex.Apply();
                    selectedBox.normal.background = tex;
                }
                return selectedBox;
            }
        }

        void OnEnable()
        {
            EditorApplication.hierarchyChanged += Repaint;
            Undo.undoRedoPerformed += Repaint;
        }

        void OnDisable()
        {
            EditorApplication.hierarchyChanged -= Repaint;
            Undo.undoRedoPerformed -= Repaint;
            if (selectedEditor != null) DestroyImmediate(selectedEditor);
        }

        void OnInspectorUpdate()
        {
            if (Application.isPlaying) Repaint();
        }

        void OnSelectionChange()
        {
            var go = Selection.activeGameObject;
            if (go != null && go.TryGetComponent<HorrorEvent>(out var ev))
            {
                selected = ev;
                Repaint();
            }
        }

        void OnGUI()
        {
            db = HorrorKitEditorUtil.Database;
            if (db == null)
            {
                EditorGUILayout.HelpBox("HorrorDatabase が見つかりません。", MessageType.Warning);
                if (GUILayout.Button("データベースを作成")) HorrorKitEditorUtil.EnsureDatabase();
                return;
            }
            if (dbSO == null || dbSO.targetObject != db) SetupLists();

            tab = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(26));
            EditorGUILayout.Space(4);

            switch (tab)
            {
                case 0: DrawDatabaseTab(itemList, true); break;
                case 1: DrawDatabaseTab(flagList, false); break;
                case 2: DrawEventsTab(); break;
                case 3: DrawTextsTab(); break;
                case 4: DrawDebugTab(); break;
            }
        }

        // ───────── アイテム / フラグ ─────────

        void SetupLists()
        {
            dbSO = new SerializedObject(db);
            itemList = MakeList(dbSO.FindProperty("items"), "キーアイテム", true);
            flagList = MakeList(dbSO.FindProperty("flags"), "フラグ", false);
        }

        ReorderableList MakeList(SerializedProperty prop, string header, bool isItem)
        {
            float L = EditorGUIUtility.singleLineHeight, S = EditorGUIUtility.standardVerticalSpacing;
            var list = new ReorderableList(dbSO, prop, true, true, true, true);
            list.drawHeaderCallback = r => EditorGUI.LabelField(r, $"{header}（{prop.arraySize} 件）");
            list.elementHeightCallback = i => isItem ? L * 5 + S * 6 : L * 3 + S * 5;
            list.drawElementCallback = (rect, i, active, focused) =>
            {
                var el = prop.GetArrayElementAtIndex(i);
                var idProp = el.FindPropertyRelative("id");
                string id = idProp.stringValue;
                bool bad = string.IsNullOrEmpty(id) || IsDuplicate(prop, i, id);
                if (bad) EditorGUI.DrawRect(new Rect(rect.x - 4, rect.y + 1, rect.width + 8, rect.height - 2), new Color(1f, 0f, 0f, 0.15f));

                float oldLW = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 56;
                float half = (rect.width - 8) / 2f;
                var r = new Rect(rect.x, rect.y + S * 2, rect.width, L);

                EditorGUI.PropertyField(new Rect(r.x, r.y, half, L), idProp, new GUIContent("ID", bad ? "IDが空、または重複しています" : "イベントから参照される識別子"));
                if (isItem) EditorGUI.PropertyField(new Rect(r.x + half + 8, r.y, half, L), el.FindPropertyRelative("displayName"), new GUIContent("表示名"));
                else EditorGUI.PropertyField(new Rect(r.x + half + 8, r.y, half, L), el.FindPropertyRelative("defaultValue"), new GUIContent("初期値"));
                r.y += L + S;

                var desc = el.FindPropertyRelative("description");
                float dh = isItem ? L * 2 + 4 : L;
                EditorGUI.LabelField(new Rect(r.x, r.y, 56, L), "説明");
                EditorGUI.BeginChangeCheck();
                string d = EditorGUI.TextArea(new Rect(r.x + 56, r.y, r.width - 56, dh), desc.stringValue, WrapArea);
                if (EditorGUI.EndChangeCheck()) desc.stringValue = d;
                r.y += dh + S;

                Rect refRect;
                if (isItem)
                {
                    EditorGUI.PropertyField(new Rect(r.x, r.y, half, L), el.FindPropertyRelative("icon"), new GUIContent("アイコン"));
                    refRect = new Rect(r.x + half + 8, r.y, half, L);
                }
                else refRect = new Rect(r.x + 56, r.y, r.width - 56, L);

                int count = refs != null && refs.TryGetValue(id, out var objs) ? objs.Count : 0;
                using (new EditorGUI.DisabledScope(count == 0))
                    if (GUI.Button(refRect, count > 0 ? $"シーン内の参照 {count} 件（選択する）" : "シーン内の参照なし", EditorStyles.miniButton))
                        Selection.objects = refs[id].ToArray();

                EditorGUIUtility.labelWidth = oldLW;
            };
            list.onAddCallback = l =>
            {
                int n = prop.arraySize;
                prop.arraySize++;
                var el = prop.GetArrayElementAtIndex(n);
                el.FindPropertyRelative("id").stringValue = UniqueId(prop, isItem ? "item" : "flag");
                el.FindPropertyRelative("description").stringValue = "";
                if (isItem)
                {
                    el.FindPropertyRelative("displayName").stringValue = "新しいアイテム";
                    el.FindPropertyRelative("icon").objectReferenceValue = null;
                }
                else el.FindPropertyRelative("defaultValue").boolValue = false;
                l.index = n;
            };
            return list;
        }

        static bool IsDuplicate(SerializedProperty list, int index, string id)
        {
            for (int i = 0; i < list.arraySize; i++)
                if (i != index && list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id) return true;
            return false;
        }

        static string UniqueId(SerializedProperty list, string prefix)
        {
            for (int n = 1; ; n++)
            {
                string id = $"{prefix}_{n:00}";
                bool used = false;
                for (int i = 0; i < list.arraySize && !used; i++)
                    used = list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id;
                if (!used) return id;
            }
        }

        void DrawDatabaseTab(ReorderableList list, bool isItem)
        {
            refs = HorrorKitEditorUtil.CollectReferences(isItem);
            dbSO.Update();

            EditorGUILayout.HelpBox(isItem
                ? "キーアイテムの定義です。IDはイベントの条件・コマンドやドアの鍵から参照されます。赤い行はIDの空欄・重複です。"
                : "フラグの定義です。イベントの出現条件・「フラグ/設定」コマンド・ドアの解錠フラグで使います。",
                MessageType.None);

            scrollA = EditorGUILayout.BeginScrollView(scrollA);
            list.DoLayoutList();

            // 定義されていないのに参照されているID
            var defined = new HashSet<string>(isItem ? db.items.Select(i => i.id) : db.flags.Select(f => f.id));
            var missing = refs.Keys.Where(k => !defined.Contains(k)).ToList();
            if (missing.Count > 0)
            {
                EditorGUILayout.HelpBox("未登録のIDがシーンで使われています: " + string.Join(", ", missing), MessageType.Warning);
                if (GUILayout.Button("未登録IDをすべて登録"))
                {
                    Undo.RecordObject(db, "HorrorKit 登録");
                    foreach (var id in missing)
                    {
                        if (isItem) db.items.Add(new ItemDefinition { id = id, displayName = id });
                        else db.flags.Add(new FlagDefinition { id = id });
                    }
                    EditorUtility.SetDirty(db);
                    dbSO.Update();
                }
            }
            EditorGUILayout.EndScrollView();
            dbSO.ApplyModifiedProperties();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("シーン内のID参照を一括置換（IDを変更したとき用）", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    replaceFrom = EditorGUILayout.TextField(replaceFrom);
                    GUILayout.Label("→", GUILayout.Width(18));
                    replaceTo = EditorGUILayout.TextField(replaceTo);
                    if (GUILayout.Button("置換", GUILayout.Width(60)))
                    {
                        int n = HorrorKitEditorUtil.ReplaceIdInScene(replaceFrom, replaceTo, isItem);
                        ShowNotification(new GUIContent($"{n} 箇所を置換しました"));
                    }
                }
            }
        }

        // ───────── イベント ─────────

        void DrawEventsTab()
        {
            var events = HorrorKitEditorUtil.FindAll<HorrorEvent>()
                .OrderBy(e => HorrorKitEditorUtil.HierarchyPath(e.transform)).ToArray();

            var filterIds = new List<string> { null };
            var filterLabels = new List<string> { "（すべて）" };
            foreach (var i in db.items) { filterIds.Add("i:" + i.id); filterLabels.Add("アイテム/" + i.id + " " + i.displayName); }
            foreach (var f in db.flags) { filterIds.Add("f:" + f.id); filterLabels.Add("フラグ/" + f.id); }
            filterIndex = Mathf.Clamp(filterIndex, 0, filterIds.Count - 1);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(320)))
                {
                    search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                    filterIndex = EditorGUILayout.Popup("参照で絞り込み", filterIndex, filterLabels.ToArray());
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("＋ 調べるイベント")) SelectEvent(HorrorKitMenus.CreateInteractEvent(null));
                        if (GUILayout.Button("＋ エリアイベント")) SelectEvent(HorrorKitMenus.CreateAreaEvent(null));
                    }

                    scrollA = EditorGUILayout.BeginScrollView(scrollA);
                    int shown = 0;
                    foreach (var ev in events)
                    {
                        if (!MatchesSearch(ev) || !MatchesFilter(ev, filterIds[filterIndex])) continue;
                        DrawEventRow(ev);
                        shown++;
                    }
                    if (shown == 0) EditorGUILayout.HelpBox("該当するイベントがありません。", MessageType.None);
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.LabelField($"{events.Length} 件のイベント", EditorStyles.miniLabel);
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    if (selected == null)
                    {
                        EditorGUILayout.HelpBox("左の一覧からイベントを選んでください。\nシーン上で HorrorEvent を持つオブジェクトを選択しても表示されます。", MessageType.Info);
                    }
                    else
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(selected.DisplayName, EditorStyles.boldLabel);
                            if (GUILayout.Button("シーンで選択", GUILayout.Width(90))) Selection.activeGameObject = selected.gameObject;
                            if (GUILayout.Button("フォーカス", GUILayout.Width(80)))
                            {
                                Selection.activeGameObject = selected.gameObject;
                                SceneView.lastActiveSceneView?.FrameSelected();
                            }
                        }
                        scrollB = EditorGUILayout.BeginScrollView(scrollB);
                        Editor.CreateCachedEditor(selected, typeof(HorrorEventEditor), ref selectedEditor);
                        selectedEditor.OnInspectorGUI();
                        EditorGUILayout.EndScrollView();
                    }
                }
            }
        }

        void SelectEvent(HorrorEvent ev)
        {
            selected = ev;
            if (ev != null) Selection.activeGameObject = ev.gameObject;
        }

        bool MatchesSearch(HorrorEvent ev)
        {
            if (string.IsNullOrEmpty(search)) return true;
            string s = search.ToLowerInvariant();
            if (ev.DisplayName.ToLowerInvariant().Contains(s) || ev.name.ToLowerInvariant().Contains(s)) return true;
            foreach (var p in ev.pages)
            {
                if (!string.IsNullOrEmpty(p.memo) && p.memo.ToLowerInvariant().Contains(s)) return true;
                foreach (var c in p.commands)
                    if (c.type == CommandType.Message && ((c.text ?? "").ToLowerInvariant().Contains(s) || (c.speaker ?? "").ToLowerInvariant().Contains(s)))
                        return true;
            }
            return false;
        }

        static bool MatchesFilter(HorrorEvent ev, string filter)
        {
            if (filter == null) return true;
            bool item = filter.StartsWith("i:");
            string id = filter.Substring(2);
            foreach (var p in ev.pages)
            {
                foreach (var c in p.conditions)
                    if (c.IsItemCondition == item && c.id == id) return true;
                foreach (var c in p.commands)
                {
                    if (item && c.IsItemCommand && c.itemId == id) return true;
                    if (!item && c.type == CommandType.SetFlag && c.flagId == id) return true;
                }
            }
            return false;
        }

        void DrawEventRow(HorrorEvent ev)
        {
            using (new EditorGUILayout.VerticalScope(ev == selected ? SelectedBox : EditorStyles.helpBox))
            {
                string title = ev.DisplayName;
                if (!ev.gameObject.activeInHierarchy) title += "（非アクティブ）";
                if (Application.isPlaying)
                {
                    int a = ev.ActivePageIndex;
                    title += a >= 0 ? $"  ▶P{a + 1}" : "  ―";
                }
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                for (int i = 0; i < ev.pages.Count; i++)
                    EditorGUILayout.LabelField($"P{i + 1} {HorrorKitEditorUtil.PageSummary(ev.pages[i])}", EditorStyles.miniLabel);
            }
            var rect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                SelectEvent(ev);
                EditorGUIUtility.PingObject(ev.gameObject);
                Event.current.Use();
            }
        }

        // ───────── 会話テキスト ─────────

        SerializedObject GetSO(Object o)
        {
            if (!textSOs.TryGetValue(o, out var so) || so.targetObject == null)
                textSOs[o] = so = new SerializedObject(o);
            return so;
        }

        void DrawTextsTab()
        {
            EditorGUILayout.HelpBox("シーン内のすべての会話・メッセージを一覧で編集できます（ドアの施錠メッセージ含む）。上の検索欄で絞り込めます。", MessageType.None);
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            string s = (search ?? "").ToLowerInvariant();

            scrollA = EditorGUILayout.BeginScrollView(scrollA);
            foreach (var ev in HorrorKitEditorUtil.FindAll<HorrorEvent>().OrderBy(e => HorrorKitEditorUtil.HierarchyPath(e.transform)))
            {
                var so = GetSO(ev);
                so.Update();
                var pages = so.FindProperty("pages");
                bool header = false;
                for (int p = 0; p < pages.arraySize; p++)
                {
                    var cmds = pages.GetArrayElementAtIndex(p).FindPropertyRelative("commands");
                    for (int c = 0; c < cmds.arraySize; c++)
                    {
                        var cmd = cmds.GetArrayElementAtIndex(c);
                        if ((CommandType)cmd.FindPropertyRelative("type").intValue != CommandType.Message) continue;
                        var speaker = cmd.FindPropertyRelative("speaker");
                        var text = cmd.FindPropertyRelative("text");
                        if (s.Length > 0 && !(text.stringValue ?? "").ToLowerInvariant().Contains(s) &&
                            !(speaker.stringValue ?? "").ToLowerInvariant().Contains(s) && !ev.DisplayName.ToLowerInvariant().Contains(s))
                            continue;

                        if (!header)
                        {
                            header = true;
                            EditorGUILayout.Space(6);
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.LabelField("◆ " + ev.DisplayName, EditorStyles.boldLabel);
                                if (GUILayout.Button("開く", GUILayout.Width(50)))
                                {
                                    tab = 2;
                                    SelectEvent(ev);
                                }
                            }
                        }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label($"P{p + 1}-{c + 1}", GUILayout.Width(44));
                            speaker.stringValue = EditorGUILayout.TextField(speaker.stringValue, GUILayout.Width(110));
                            text.stringValue = EditorGUILayout.TextArea(text.stringValue, WrapArea, GUILayout.MinHeight(38));
                        }
                    }
                }
                so.ApplyModifiedProperties();
            }

            var doors = HorrorKitEditorUtil.FindAll<Door>().Where(d => d.locked || !string.IsNullOrEmpty(d.keyItemId)).ToArray();
            if (doors.Length > 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("ドアのメッセージ", EditorStyles.boldLabel);
                foreach (var d in doors)
                {
                    var so = GetSO(d);
                    so.Update();
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.ObjectField(d, typeof(Door), true);
                        var lockedMsg = so.FindProperty("lockedMessage");
                        var unlockMsg = so.FindProperty("unlockMessage");
                        lockedMsg.stringValue = EditorGUILayout.TextArea(lockedMsg.stringValue, WrapArea);
                        unlockMsg.stringValue = EditorGUILayout.TextArea(unlockMsg.stringValue, WrapArea);
                    }
                    so.ApplyModifiedProperties();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        // ───────── デバッグ ─────────

        void DrawDebugTab()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("再生中に、フラグ・所持品の確認と変更、イベントの強制実行ができます。", MessageType.Info);
                return;
            }

            scrollA = EditorGUILayout.BeginScrollView(scrollA);

            EditorGUILayout.LabelField("フラグ", EditorStyles.boldLabel);
            var shownFlags = new HashSet<string>();
            foreach (var f in db.flags)
            {
                shownFlags.Add(f.id);
                bool v = GameState.GetFlag(f.id);
                bool nv = EditorGUILayout.ToggleLeft($"{f.id}    {HorrorKitEditorUtil.FirstLine(f.description)}", v);
                if (nv != v) GameState.SetFlag(f.id, nv);
            }
            foreach (var kv in GameState.Flags.ToList())
            {
                if (shownFlags.Contains(kv.Key)) continue;
                bool nv = EditorGUILayout.ToggleLeft($"{kv.Key}    （未登録）", kv.Value);
                if (nv != kv.Value) GameState.SetFlag(kv.Key, nv);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("所持品", EditorStyles.boldLabel);
            foreach (var i in db.items)
            {
                bool has = GameState.HasItem(i.id);
                bool nh = EditorGUILayout.ToggleLeft($"{i.displayName}  ({i.id})", has);
                if (nh && !has) GameState.AddItem(i.id);
                if (!nh && has) GameState.RemoveItem(i.id);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("イベント", EditorStyles.boldLabel);
            foreach (var ev in HorrorKitEditorUtil.FindAll<HorrorEvent>())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int a = ev.ActivePageIndex;
                    EditorGUILayout.LabelField(ev.DisplayName, a >= 0 ? $"ページ {a + 1}  {HorrorKitEditorUtil.PageSummary(ev.pages[a])}" : "有効なページなし");
                    using (new EditorGUI.DisabledScope(a < 0 || EventRunner.IsBusy))
                        if (GUILayout.Button("実行", GUILayout.Width(50))) ev.RunActivePage();
                }
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("フラグ・所持品・実行済みページをリセット")) GameState.ResetAll();
            EditorGUILayout.EndScrollView();
        }
    }
}
