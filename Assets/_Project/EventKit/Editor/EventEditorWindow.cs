using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EventKit.EditorTools
{
    /// <summary>
    /// RPGツクール風のイベントエディタ。
    /// 左：ページの設定（出現条件・起動方法）／右：実行内容（◆の行）を上から順に並べる。
    /// 行のダブルクリック・Enter で挿入、Ctrl+C/X/V でコピー・切り取り・貼り付け、Delete で削除、Alt+↑↓ で並び替え。
    /// 条件分岐・選択肢はブロック単位で扱う（分岐終了などの印は自動で管理される）。
    /// </summary>
    public class EventEditorWindow : EditorWindow
    {
        [MenuItem("EventKit/イベントエディタ %#e", priority = 1)]
        static void OpenFromMenu()
        {
            var go = Selection.activeGameObject;
            Open(go != null ? go.GetComponent<GameEvent>() : null);
        }

        public static void Open(GameEvent ev, int page = 0, int line = -1)
        {
            var w = GetWindow<EventEditorWindow>();
            w.titleContent = new GUIContent("イベントエディタ");
            w.minSize = new Vector2(860, 540);
            w.SetTarget(ev, page);
            if (line >= 0) w.selected = line;
            w.Show();
            w.Focus();
        }

        [SerializeField] GameEvent target;
        [SerializeField] int page;
        [SerializeField] int selected = -1;

        SerializedObject so;
        Vector2 leftScroll, listScroll, detailScroll;
        Rect listRect;
        readonly List<Action> deferred = new List<Action>();

        static string clipboardCommands;
        static string clipboardPage;

        const float RowHeight = 20f;
        const float IndentWidth = 18f;

        [Serializable] class CommandClip { public List<EventCommand> items = new List<EventCommand>(); }
        [Serializable] class PageClip { public EventPage page; }
        [Serializable] class PagesClip { public List<EventPage> pages = new List<EventPage>(); }

        static GUIStyle rowStyle, hintStyle;
        static GUIStyle RowStyle => rowStyle ??= new GUIStyle(EditorStyles.label) { richText = false, fontSize = 12, padding = new RectOffset(4, 4, 2, 2) };
        static GUIStyle HintStyle => hintStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

        void SetTarget(GameEvent ev, int p)
        {
            target = ev;
            page = Mathf.Max(0, p);
            selected = -1;
            so = null;
            Repaint();
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.hierarchyChanged += Repaint;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.hierarchyChanged -= Repaint;
        }

        void OnUndoRedo()
        {
            so?.Update();
            Repaint();
        }

        void OnSelectionChange()
        {
            var go = Selection.activeGameObject;
            if (go != null && go.TryGetComponent<GameEvent>(out var ev) && ev != target) SetTarget(ev, 0);
        }

        void OnInspectorUpdate()
        {
            if (Application.isPlaying) Repaint();
        }

        List<EventCommand> Commands => target.pages[page].commands;

        // ───────── 描画 ─────────

        void OnGUI()
        {
            if (target == null)
            {
                DrawNoTarget();
                return;
            }
            if (so == null || so.targetObject != target) so = new SerializedObject(target);
            so.Update();

            if (target.pages.Count == 0)
            {
                EditorGUILayout.HelpBox("ページがありません。", MessageType.Info);
                if (GUILayout.Button("ページを追加")) Defer(() => target.pages.Add(new EventPage()));
                EndGUI();
                return;
            }
            page = Mathf.Clamp(page, 0, target.pages.Count - 1);
            selected = Mathf.Clamp(selected, -1, Commands.Count);

            DrawHeader();
            DrawPageTabs();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLeft();
                GUILayout.Space(6);
                DrawRight();
            }
            HandleKeyboard();
            EndGUI();
        }

        void EndGUI()
        {
            so?.ApplyModifiedProperties();
            if (deferred.Count > 0)
            {
                var actions = deferred.ToArray();
                deferred.Clear();
                Undo.RecordObject(target, "イベントの編集");
                foreach (var a in actions) a();
                EditorUtility.SetDirty(target);
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                so?.Update();
                Repaint();
            }
        }

        void Defer(Action a)
        {
            deferred.Add(a);
            Repaint();
        }

        void DrawNoTarget()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("編集するイベントを選んでください。\nHierarchy で GameEvent を持つオブジェクトを選ぶと、ここに表示されます。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("＋ 調べるイベントを作る", GUILayout.Height(28)))
                    SetTarget(EventKitMenus.CreateInteractEvent(null), 0);
                if (GUILayout.Button("＋ エリアイベントを作る", GUILayout.Height(28)))
                    SetTarget(EventKitMenus.CreateAreaEvent(null), 0);
            }
            DrawEventListButton(GUI.skin.button, GUILayout.Height(28));
        }

        /// <summary>イベント一覧（アクティブシーン／全体）のポップアップを出すボタン。一覧は読み取りのみ。</summary>
        void DrawEventListButton(GUIStyle style, params GUILayoutOption[] options)
        {
            var content = new GUIContent("イベント一覧");
            var r = GUILayoutUtility.GetRect(content, style, options);
            if (GUI.Button(r, content, style))
                PopupWindow.Show(r, new EventListPopup(target, ev => SetTarget(ev, 0)));
        }

        void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("イベント名", GUILayout.Width(60));
                var nameProp = so.FindProperty("eventName");
                nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue, EditorStyles.toolbarTextField, GUILayout.Width(220));
                GUILayout.Space(8);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(target.gameObject, typeof(GameObject), true, GUILayout.Width(180));
                if (GUILayout.Button("シーンで選択", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    Selection.activeGameObject = target.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
                DrawEventListButton(EditorStyles.toolbarDropDown, GUILayout.Width(90));
                GUILayout.FlexibleSpace();
                if (Application.isPlaying)
                {
                    int a = target.ActivePageIndex;
                    GUILayout.Label(a >= 0 ? $"▶ 実行中の有効ページ：{a + 1}" : "有効なページなし", EditorStyles.miniBoldLabel);
                }
            }
        }

        void DrawPageTabs()
        {
            int active = Application.isPlaying ? target.ActivePageIndex : -1;
            int newPage = PageTabBar.Draw(target.pages, page, active,
                (from, to) => Defer(() => PageTabBar.Move(target.pages, from, to)));
            if (newPage != page) { page = newPage; selected = -1; }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("＋ 新規ページ", "右端に追加します。\n" + PageTabBar.RuleText), EditorStyles.miniButtonLeft))
                    Defer(() => { target.pages.Add(new EventPage()); page = target.pages.Count - 1; selected = -1; });
                if (GUILayout.Button("ページをコピー", EditorStyles.miniButtonMid))
                    clipboardPage = EditorJsonUtility.ToJson(new PageClip { page = target.pages[page] });
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(clipboardPage)))
                    if (GUILayout.Button("ページを貼り付け", EditorStyles.miniButtonMid))
                        Defer(() =>
                        {
                            var clip = new PageClip();
                            EditorJsonUtility.FromJsonOverwrite(clipboardPage, clip);
                            target.pages.Insert(page + 1, clip.page);
                            page++;
                            selected = -1;
                        });
                var tplContent = new GUIContent("テンプレート");
                var tplRect = GUILayoutUtility.GetRect(tplContent, EditorStyles.miniButtonMid);
                if (GUI.Button(tplRect, tplContent, EditorStyles.miniButtonMid)) ShowTemplateMenu();
                using (new EditorGUI.DisabledScope(page == 0))
                    if (GUILayout.Button("◀ 前へ", EditorStyles.miniButtonMid))
                        Defer(() => { (target.pages[page - 1], target.pages[page]) = (target.pages[page], target.pages[page - 1]); page--; });
                using (new EditorGUI.DisabledScope(page >= target.pages.Count - 1))
                    if (GUILayout.Button("次へ ▶", EditorStyles.miniButtonMid))
                        Defer(() => { (target.pages[page + 1], target.pages[page]) = (target.pages[page], target.pages[page + 1]); page++; });
                using (new EditorGUI.DisabledScope(target.pages.Count <= 1))
                    if (GUILayout.Button("ページを削除", EditorStyles.miniButtonRight)
                        && EditorUtility.DisplayDialog("ページの削除", $"ページ {page + 1} を削除しますか？", "削除", "キャンセル"))
                        Defer(() => { target.pages.RemoveAt(page); page = Mathf.Max(0, page - 1); selected = -1; });
            }
        }

        // ───────── ページテンプレート ─────────

        /// <summary>テンプレートの保存先（EventKit の外。例: Assets/_Project/EventTemplates）</summary>
        static string TemplateFolder
        {
            get
            {
                string root = EventKitEditorUtil.KitRoot;
                int slash = root.LastIndexOf('/');
                return (slash > 0 ? root.Substring(0, slash) : "Assets") + "/EventTemplates";
            }
        }

        void ShowTemplateMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent($"このページ（ページ {page + 1}）をテンプレートに保存…"), false, () => SaveTemplate(new List<EventPage> { target.pages[page] }));
            menu.AddItem(new GUIContent($"全ページ（{target.pages.Count}ページ）をテンプレートに保存…"), false, () => SaveTemplate(target.pages));
            menu.AddSeparator("");

            var templates = new List<EventPageTemplate>();
            foreach (var guid in AssetDatabase.FindAssets("t:EventPageTemplate"))
            {
                var t = AssetDatabase.LoadAssetAtPath<EventPageTemplate>(AssetDatabase.GUIDToAssetPath(guid));
                if (t != null && t.pages.Count > 0) templates.Add(t);
            }
            templates.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            if (templates.Count == 0) menu.AddDisabledItem(new GUIContent("テンプレートから追加（まだありません）"));
            foreach (var t in templates)
            {
                var tpl = t;
                string suffix = tpl.pages.Count > 1 ? $"（{tpl.pages.Count}ページ）" : "";
                menu.AddItem(new GUIContent("テンプレートから追加/" + tpl.DisplayName + suffix), false, () => InsertTemplate(tpl));
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("テンプレートの場所を表示"), false, () =>
            {
                EventKitEditorUtil.EnsureFolder(TemplateFolder);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TemplateFolder));
            });
            menu.ShowAsContext();
        }

        void SaveTemplate(List<EventPage> pages)
        {
            EventKitEditorUtil.EnsureFolder(TemplateFolder);
            string defaultName = (pages.Count == 1 && !string.IsNullOrEmpty(pages[0].memo) ? pages[0].memo : target.DisplayName);
            string path = EditorUtility.SaveFilePanelInProject("ページをテンプレートに保存", defaultName, "asset",
                "テンプレートの名前を付けて保存します。シーン上のオブジェクトへの参照（ドア・対象など）は保存されないので、追加したあとに設定し直してください。",
                TemplateFolder);
            if (string.IsNullOrEmpty(path)) return;

            so.ApplyModifiedProperties();
            var tpl = CreateInstance<EventPageTemplate>();
            tpl.templateName = System.IO.Path.GetFileNameWithoutExtension(path);
            tpl.pages = ClonePages(pages);
            // シーンのオブジェクトはアセットに保存できないので外しておく
            foreach (var p in tpl.pages)
                foreach (var c in p.commands)
                    if (c != null) ClearSceneRefs(c);

            var existing = AssetDatabase.LoadAssetAtPath<EventPageTemplate>(path);
            if (existing != null)
            {
                Undo.RecordObject(existing, "テンプレートの上書き");
                existing.pages = tpl.pages;
                EditorUtility.SetDirty(existing);
                DestroyImmediate(tpl);
                tpl = existing;
            }
            else AssetDatabase.CreateAsset(tpl, path);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("テンプレートに保存しました：" + tpl.DisplayName));
        }

        void InsertTemplate(EventPageTemplate tpl)
        {
            var pages = ClonePages(tpl.pages);
            Defer(() =>
            {
                target.pages.InsertRange(page + 1, pages);
                page++;
                selected = -1;
            });
            Repaint();
        }

        static List<EventPage> ClonePages(List<EventPage> pages)
        {
            var clip = new PagesClip();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(new PagesClip { pages = pages }), clip);
            return clip.pages;
        }

        static void ClearSceneRefs(EventCommand c)
        {
            bool IsScene(UnityEngine.Object o) => o != null && !EditorUtility.IsPersistent(o);
            if (IsScene(c.door)) c.door = null;
            if (IsScene(c.jumpScare)) c.jumpScare = null;
            if (IsScene(c.target)) c.target = null;
            if (IsScene(c.callEvent)) c.callEvent = null;
            if (c.unityEvent != null)
                for (int i = 0; i < c.unityEvent.GetPersistentEventCount(); i++)
                    if (IsScene(c.unityEvent.GetPersistentTarget(i))) { c.unityEvent = new UnityEngine.Events.UnityEvent(); break; }
        }

        void DrawLeft()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(310)))
            {
                leftScroll = EditorGUILayout.BeginScrollView(leftScroll);
                var p = so.FindProperty("pages").GetArrayElementAtIndex(page);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("出現条件", EditorStyles.boldLabel);
                    var conds = p.FindPropertyRelative("conditions");
                    EditorGUILayout.LabelField(conds.arraySize == 0 ? "条件なし＝常に有効" : "すべて満たすと、このページが有効になる", EditorStyles.miniLabel);
                    conds.isExpanded = true;
                    EditorGUILayout.PropertyField(conds, new GUIContent($"条件（{conds.arraySize}）"), true);
                }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("トリガー（起動方法）", EditorStyles.boldLabel);
                    float lw = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 100;
                    var trig = p.FindPropertyRelative("trigger");
                    EditorGUILayout.PropertyField(trig, new GUIContent("起動方法"));
                    if ((EventTrigger)trig.intValue == EventTrigger.Interact)
                        EditorGUILayout.PropertyField(p.FindPropertyRelative("prompt"), new GUIContent("表示する行動名"));
                    if ((EventTrigger)trig.intValue != EventTrigger.AutoStart)
                        EditorGUILayout.PropertyField(p.FindPropertyRelative("runOnce"), new GUIContent("一度だけ実行"));
                    EditorGUILayout.PropertyField(p.FindPropertyRelative("memo"), new GUIContent("メモ"));
                    EditorGUIUtility.labelWidth = lw;
                }
                EditorGUILayout.HelpBox("番号の大きいページから順に出現条件を調べ、最初に満たしたページが有効になります。", MessageType.None);
                EditorGUILayout.EndScrollView();
            }
        }

        void DrawRight()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                DrawListToolbar();

                // 実行内容の一覧
                float listHeight = Mathf.Max(160f, position.height * 0.55f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(listHeight)))
                {
                    listScroll = EditorGUILayout.BeginScrollView(listScroll);
                    DrawCommandRows();
                    EditorGUILayout.EndScrollView();
                }
                if (Event.current.type == EventType.Repaint) listRect = GUILayoutUtility.GetLastRect();
                EditorGUILayout.LabelField("ダブルクリック／Enter：挿入（選んだ行の上に入る）　Ctrl+C / X / V：コピー・切り取り・貼り付け　Delete：削除　Alt+↑↓：並び替え　右クリック：メニュー", HintStyle);

                DrawDetail();
            }
        }

        void DrawListToolbar()
        {
            bool editable = CanOperate(selected);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("実行内容", EditorStyles.boldLabel, GUILayout.Width(60));
                var r = GUILayoutUtility.GetRect(new GUIContent("＋ コマンドを挿入"), EditorStyles.miniButtonLeft, GUILayout.Width(120));
                if (GUI.Button(r, "＋ コマンドを挿入", EditorStyles.miniButtonLeft)) ShowInsertPopup(r);
                using (new EditorGUI.DisabledScope(!editable))
                {
                    if (GUILayout.Button("コピー", EditorStyles.miniButtonMid)) CopySelection();
                    if (GUILayout.Button("切り取り", EditorStyles.miniButtonMid)) { CopySelection(); DeleteSelection(); }
                }
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(clipboardCommands)))
                    if (GUILayout.Button("貼り付け", EditorStyles.miniButtonMid)) Paste();
                using (new EditorGUI.DisabledScope(!editable))
                {
                    if (GUILayout.Button("削除", EditorStyles.miniButtonMid)) DeleteSelection();
                    if (GUILayout.Button("▲", EditorStyles.miniButtonMid, GUILayout.Width(26))) MoveSelection(-1);
                    if (GUILayout.Button("▼", EditorStyles.miniButtonRight, GUILayout.Width(26))) MoveSelection(1);
                }
                GUILayout.FlexibleSpace();
            }
        }

        void DrawCommandRows()
        {
            var cmds = Commands;
            var indents = EventKitEditorUtil.Indents(cmds);
            var (s, e) = UnitRange(selected);
            var ev = Event.current;

            for (int i = 0; i <= cmds.Count; i++)
            {
                var row = GUILayoutUtility.GetRect(10, RowHeight, GUILayout.ExpandWidth(true));
                bool isBlank = i == cmds.Count;
                bool inSel = selected >= 0 && i >= s && i <= e;

                if (ev.type == EventType.Repaint)
                {
                    if (inSel) EditorGUI.DrawRect(row, EditorGUIUtility.isProSkin ? new Color(0.17f, 0.36f, 0.53f) : new Color(0.6f, 0.78f, 1f));
                    else if (i % 2 == 1) EditorGUI.DrawRect(row, new Color(0, 0, 0, 0.04f));

                    int indent = isBlank ? 0 : indents[i];
                    var textRect = new Rect(row.x + 4 + indent * IndentWidth, row.y, row.width - indent * IndentWidth, row.height);
                    // ブロックの中を示す縦線
                    for (int k = 0; k < indent; k++)
                        EditorGUI.DrawRect(new Rect(row.x + 10 + k * IndentWidth, row.y, 1, row.height), new Color(0.5f, 0.5f, 0.5f, 0.35f));

                    var style = new GUIStyle(RowStyle);
                    style.normal.textColor = isBlank ? new Color(0.5f, 0.5f, 0.5f) : EventKitEditorUtil.CommandColor(cmds[i].type);
                    if (!isBlank && CommandTypes.IsStructural(cmds[i].type)) style.fontStyle = FontStyle.Bold;
                    style.Draw(textRect, isBlank ? "◆" : EventKitEditorUtil.CommandLine(cmds, i), false, false, false, false);
                }

                if (ev.type == EventType.MouseDown && row.Contains(ev.mousePosition))
                {
                    selected = i;
                    GUI.FocusControl(null);
                    if (ev.button == 1) ShowContextMenu(i);
                    else if (ev.clickCount == 2)
                    {
                        if (isBlank || CommandTypes.IsCloser(cmds[i].type) || CommandTypes.IsDivider(cmds[i].type))
                            ShowInsertPopup(new Rect(ev.mousePosition, Vector2.zero));
                        else
                        {
                            // コマンドを別ウィンドウで開く（視点変更はカメラ操作ウィンドウ）
                            var t = target; int p = page, idx = i;
                            var type = cmds[i].type;
                            EditorApplication.delayCall += () =>
                            {
                                if (type == CommandType.CameraView) CameraViewWindow.Open(t, p, idx);
                                else CommandEditWindow.Open(t, p, idx);
                            };
                        }
                    }
                    ev.Use();
                    Repaint();
                }
            }
        }

        void DrawDetail()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var cmds = Commands;
                if (selected < 0 || selected >= cmds.Count)
                {
                    EditorGUILayout.LabelField("選択中のコマンド", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("行を選ぶと、ここで内容を編集できます。行をダブルクリックすると別ウィンドウで開きます。末尾の ◆ をダブルクリックするとコマンドを追加できます。", HintStyle);
                    return;
                }

                var c = cmds[selected];
                if (CommandTypes.IsCloser(c.type) || CommandTypes.IsDivider(c.type))
                {
                    EditorGUILayout.LabelField("ブロックの区切り", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("この行は自動で管理されます。ダブルクリックすると、この行の上にコマンドを挿入できます。", HintStyle);
                    return;
                }

                EditorGUILayout.LabelField("選択中のコマンド：" + EventCommandDrawer.TypeLabel(c.type), EditorStyles.boldLabel);
                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                var prop = so.FindProperty("pages").GetArrayElementAtIndex(page).FindPropertyRelative("commands").GetArrayElementAtIndex(selected);
                EditorGUILayout.PropertyField(prop, GUIContent.none, true);

                if (c.type == CommandType.CameraView) CameraViewWindow.Follow(target, page, selected);
                CommandEditWindow.Follow(target, page, selected);
                if (c.type == CommandType.If) DrawIfOptions(selected);
                if (c.type == CommandType.ShowChoices) DrawChoiceOptions(selected);
                EditorGUILayout.EndScrollView();
            }
        }

        // 条件分岐：「それ以外のとき」の有無
        void DrawIfOptions(int ifIndex)
        {
            var cmds = Commands;
            int elseIndex = CommandTypes.ScanLevel(cmds, ifIndex + 1, t => t == CommandType.Else);
            bool hasElse = elseIndex < cmds.Count && cmds[elseIndex].type == CommandType.Else;
            bool want = EditorGUILayout.ToggleLeft("条件を満たさないとき（それ以外のとき）の処理を作る", hasElse);
            if (want == hasElse) return;
            if (want)
            {
                Defer(() =>
                {
                    int end = CommandTypes.BlockEnd(Commands, ifIndex);
                    Commands.Insert(end, new EventCommand { type = CommandType.Else });
                });
            }
            else
            {
                int end = CommandTypes.BlockEnd(cmds, ifIndex);
                bool hasContent = end - elseIndex > 1;
                if (!hasContent || EditorUtility.DisplayDialog("それ以外のときの削除", "「それ以外のとき」の中のコマンドも削除されます。よろしいですか？", "削除", "キャンセル"))
                    Defer(() =>
                    {
                        int e2 = CommandTypes.BlockEnd(Commands, ifIndex);
                        int el = CommandTypes.ScanLevel(Commands, ifIndex + 1, t => t == CommandType.Else);
                        Commands.RemoveRange(el, e2 - el);
                    });
            }
        }

        // 選択肢：選択肢の数と［○○］のときの数を揃える
        void DrawChoiceOptions(int openIndex)
        {
            var cmds = Commands;
            var c = cmds[openIndex];
            var whens = WhenIndices(cmds, openIndex);
            int wanted = Mathf.Clamp(c.choices.Count, 1, 6);
            if (c.choices.Count > 6) EditorGUILayout.HelpBox("選択肢は6個までです。", MessageType.Warning);
            if (whens.Count == wanted) return;

            Defer(() =>
            {
                var list = Commands;
                var current = WhenIndices(list, openIndex);
                if (current.Count < wanted)
                {
                    for (int k = current.Count; k < wanted; k++)
                    {
                        int end = CommandTypes.BlockEnd(list, openIndex);
                        list.Insert(end, new EventCommand { type = CommandType.When, choiceIndex = k });
                    }
                }
                else
                {
                    // 後ろの選択肢から、その中身ごと削除
                    for (int k = current.Count - 1; k >= wanted; k--)
                    {
                        var idx = WhenIndices(list, openIndex);
                        int start = idx[k];
                        int stop = k + 1 < idx.Count ? idx[k + 1] : CommandTypes.BlockEnd(list, openIndex);
                        list.RemoveRange(start, stop - start);
                    }
                }
                var fixedIdx = WhenIndices(list, openIndex);
                for (int k = 0; k < fixedIdx.Count; k++) list[fixedIdx[k]].choiceIndex = k;
            });
        }

        static List<int> WhenIndices(IList<EventCommand> cmds, int openIndex)
        {
            var result = new List<int>();
            int j = openIndex + 1;
            while (true)
            {
                j = CommandTypes.ScanLevel(cmds, j, t => t == CommandType.When);
                if (j >= cmds.Count || cmds[j].type != CommandType.When) break;
                result.Add(j);
                j++;
            }
            return result;
        }

        // ───────── 選択と操作 ─────────

        /// <summary>選択行が表すまとまり（開始行ならブロック全体）。</summary>
        (int, int) UnitRange(int index)
        {
            if (target == null || index < 0 || index >= Commands.Count) return (index, index);
            return (index, CommandTypes.BlockEnd(Commands, index));
        }

        /// <summary>コピー・削除・移動できる行か（区切り・終了・末尾の空行は不可）。</summary>
        bool CanOperate(int index)
        {
            if (target == null || index < 0 || index >= Commands.Count) return false;
            var t = Commands[index].type;
            return !CommandTypes.IsCloser(t) && !CommandTypes.IsDivider(t);
        }

        int InsertIndex => selected < 0 ? Commands.Count : Mathf.Min(selected, Commands.Count);

        void ShowInsertPopup(Rect at)
        {
            PopupWindow.Show(at, new InsertCommandPopup(this));
        }

        public void InsertNew(CommandType type, Type customType = null)
        {
            int at = InsertIndex;
            Defer(() =>
            {
                var items = NewCommands(type, customType);
                Commands.InsertRange(at, items);
                selected = at;
                // 視点変更はカメラ操作ウィンドウを出す
                if (type == CommandType.CameraView)
                {
                    var ev = target;
                    int pg = page;
                    EditorApplication.delayCall += () => CameraViewWindow.Open(ev, pg, at);
                }
            });
            Repaint();
        }

        static List<EventCommand> NewCommands(CommandType type, Type customType = null)
        {
            switch (type)
            {
                case CommandType.If:
                    return new List<EventCommand>
                    {
                        new EventCommand { type = CommandType.If, condition = new EventCondition { type = ConditionType.FlagOn } },
                        new EventCommand { type = CommandType.Else },
                        new EventCommand { type = CommandType.EndIf },
                    };
                case CommandType.ShowChoices:
                    return new List<EventCommand>
                    {
                        new EventCommand { type = CommandType.ShowChoices, choices = new List<string> { "はい", "いいえ" } },
                        new EventCommand { type = CommandType.When, choiceIndex = 0 },
                        new EventCommand { type = CommandType.When, choiceIndex = 1 },
                        new EventCommand { type = CommandType.EndChoices },
                    };
                case CommandType.Message:
                    return new List<EventCommand> { new EventCommand { type = type, text = "" } };
                case CommandType.SetActive:
                    return new List<EventCommand> { new EventCommand { type = type, boolValue = false } };
                case CommandType.PlaySound:
                    return new List<EventCommand> { new EventCommand { type = type, boolValue = false } };
                case CommandType.CameraView:
                {
                    // 最初は今のシーンビューの視点を入れておく
                    var c = new EventCommand { type = type, duration = 1f };
                    var sv = SceneView.lastActiveSceneView;
                    if (sv != null && sv.camera != null)
                    {
                        c.viewPosition = sv.camera.transform.position;
                        c.viewRotation = sv.camera.transform.rotation.eulerAngles;
                    }
                    return new List<EventCommand> { c };
                }
                case CommandType.CameraShake:
                    return new List<EventCommand> { new EventCommand { type = type, duration = 0.5f, strength = 1f } };
                case CommandType.RotateObject:
                    return new List<EventCommand> { new EventCommand { type = type, rotateMode = RotateMode.Add, vector = new Vector3(0f, 90f, 0f) } };
                case CommandType.ScaleObject:
                    return new List<EventCommand> { new EventCommand { type = type, vector = Vector3.one } };
                case CommandType.MoveObject:
                    return new List<EventCommand> { new EventCommand { type = type, vector = new Vector3(0f, 1f, 0f) } };
                case CommandType.Warp:
                    return new List<EventCommand> { new EventCommand { type = type, boolValue = true, duration = 0.5f, fadeInDuration = 0.5f } };
                case CommandType.TransferSceneLoading:
                    return new List<EventCommand> { new EventCommand { type = type, loadingPrefab = UIPrefabTools.DefaultPrefab() } };
                case CommandType.Custom:
                    return new List<EventCommand> { new EventCommand { type = type, custom = customType != null ? (CustomCommand)Activator.CreateInstance(customType) : null } };
                default:
                    return new List<EventCommand> { new EventCommand { type = type } };
            }
        }

        void CopySelection()
        {
            if (!CanOperate(selected)) return;
            var (s, e) = UnitRange(selected);
            var clip = new CommandClip { items = Commands.GetRange(s, e - s + 1) };
            clipboardCommands = EditorJsonUtility.ToJson(clip);
        }

        void Paste()
        {
            if (string.IsNullOrEmpty(clipboardCommands)) return;
            int at = InsertIndex;
            Defer(() =>
            {
                var clip = new CommandClip();
                EditorJsonUtility.FromJsonOverwrite(clipboardCommands, clip);
                Commands.InsertRange(at, clip.items);
                selected = at + clip.items.Count;
            });
        }

        void DeleteSelection()
        {
            if (!CanOperate(selected)) return;
            var (s, e) = UnitRange(selected);
            Defer(() =>
            {
                Commands.RemoveRange(s, e - s + 1);
                selected = Mathf.Min(s, Commands.Count);
            });
        }

        /// <summary>選択中のまとまりを1つ上／下へ。隣がブロックならブロックごと飛び越える。</summary>
        void MoveSelection(int dir)
        {
            if (!CanOperate(selected)) return;
            Defer(() =>
            {
                var cmds = Commands;
                var (s, e) = UnitRange(selected);
                int len = e - s + 1;
                var unit = cmds.GetRange(s, len);
                if (dir < 0)
                {
                    if (s == 0) return;
                    int to = s - 1;
                    if (CommandTypes.IsCloser(cmds[to].type))
                    {
                        int opener = EventKitEditorUtil.ParentOpener(cmds, to);
                        if (opener >= 0) to = opener;
                    }
                    cmds.RemoveRange(s, len);
                    cmds.InsertRange(to, unit);
                    selected = to;
                }
                else
                {
                    if (e + 1 >= cmds.Count) return;
                    int next = e + 1;
                    int skipEnd = CommandTypes.IsOpener(cmds[next].type) ? CommandTypes.BlockEnd(cmds, next) : next;
                    cmds.RemoveRange(s, len);
                    int insertAt = skipEnd - len + 1;
                    cmds.InsertRange(insertAt, unit);
                    selected = insertAt;
                }
            });
        }

        void ShowContextMenu(int index)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("挿入…"), false, () => ShowInsertPopup(new Rect(Event.current != null ? Event.current.mousePosition : Vector2.zero, Vector2.zero)));
            menu.AddSeparator("");
            if (CanOperate(index))
            {
                menu.AddItem(new GUIContent("コピー"), false, CopySelection);
                menu.AddItem(new GUIContent("切り取り"), false, () => { CopySelection(); DeleteSelection(); });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("コピー"));
                menu.AddDisabledItem(new GUIContent("切り取り"));
            }
            if (!string.IsNullOrEmpty(clipboardCommands)) menu.AddItem(new GUIContent("貼り付け"), false, Paste);
            else menu.AddDisabledItem(new GUIContent("貼り付け"));
            menu.AddSeparator("");
            if (CanOperate(index))
            {
                menu.AddItem(new GUIContent("上へ移動"), false, () => MoveSelection(-1));
                menu.AddItem(new GUIContent("下へ移動"), false, () => MoveSelection(1));
                menu.AddItem(new GUIContent("削除"), false, DeleteSelection);
            }
            menu.ShowAsContext();
        }

        void HandleKeyboard()
        {
            var ev = Event.current;
            if (ev.type != EventType.KeyDown || EditorGUIUtility.editingTextField) return;
            if (GUIUtility.keyboardControl != 0) return;

            bool ctrl = ev.control || ev.command;
            int count = Commands.Count;
            switch (ev.keyCode)
            {
                case KeyCode.UpArrow:
                    if (ev.alt) MoveSelection(-1);
                    else selected = Mathf.Max(0, (selected < 0 ? count : selected) - 1);
                    break;
                case KeyCode.DownArrow:
                    if (ev.alt) MoveSelection(1);
                    else selected = Mathf.Min(count, selected + 1);
                    break;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    DeleteSelection();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    ShowInsertPopup(new Rect(listRect.x + 40, listRect.y + 20, 0, 0));
                    break;
                case KeyCode.C when ctrl: CopySelection(); break;
                case KeyCode.X when ctrl: CopySelection(); DeleteSelection(); break;
                case KeyCode.V when ctrl: Paste(); break;
                default: return;
            }
            ev.Use();
            Repaint();
        }

        // ───────── コマンド挿入ポップアップ ─────────

        class InsertCommandPopup : PopupWindowContent
        {
            readonly EventEditorWindow owner;
            readonly List<(string title, List<(string label, CommandType type, Type custom)> items)> groups;
            readonly List<int[]> columns;

            public InsertCommandPopup(EventEditorWindow owner)
            {
                this.owner = owner;
                groups = new List<(string, List<(string, CommandType, Type)>)>();
                foreach (var g in BuiltIn)
                {
                    var items = new List<(string, CommandType, Type)>();
                    foreach (var (label, type) in g.items) items.Add((label, type, null));
                    groups.Add((g.title, items));
                }
                columns = new List<int[]> { new[] { 0, 4 }, new[] { 1 }, new[] { 2, 3 }, new[] { 5 } };

                // 追加パックの拡張コマンド（「グループ/表示名」のグループごと）を右の列に並べる
                var customGroups = new List<int>();
                var byTitle = new Dictionary<string, int>();
                foreach (var (type, path) in EventKitEditorUtil.CustomCommandTypes)
                {
                    int slash = path.IndexOf('/');
                    string title = slash > 0 ? path.Substring(0, slash) : "拡張";
                    string label = slash > 0 ? path.Substring(slash + 1) : path;
                    if (!byTitle.TryGetValue(title, out int gi))
                    {
                        groups.Add((title, new List<(string, CommandType, Type)>()));
                        gi = byTitle[title] = groups.Count - 1;
                        customGroups.Add(gi);
                    }
                    groups[gi].items.Add((label, CommandType.Custom, type));
                }
                if (customGroups.Count > 0) columns.Add(customGroups.ToArray());
            }

            static readonly (string title, (string label, CommandType type)[] items)[] BuiltIn =
            {
                ("会話", new[]
                {
                    ("文章の表示", CommandType.Message),
                    ("選択肢の表示", CommandType.ShowChoices),
                }),
                ("進行・フロー", new[]
                {
                    ("フラグの操作", CommandType.SetFlag),
                    ("アイテムの入手", CommandType.GiveItem),
                    ("アイテムを失う", CommandType.RemoveItem),
                    ("条件分岐", CommandType.If),
                    ("イベントの呼び出し", CommandType.CallEvent),
                    ("待機", CommandType.Wait),
                    ("UnityEventの実行", CommandType.InvokeUnityEvent),
                    ("イベント処理の中断", CommandType.EndEvent),
                }),
                ("演出", new[]
                {
                    ("効果音", CommandType.PlaySound),
                    ("演出オブジェクトを再生", CommandType.JumpScare),
                    ("フェードアウト（暗くなる）", CommandType.FadeOut),
                    ("フェードイン（明るくなる）", CommandType.FadeIn),
                }),
                ("シーン・移動", new[]
                {
                    ("シーン遷移", CommandType.TransferScene),
                    ("ロード画面付きシーン遷移", CommandType.TransferSceneLoading),
                    ("シーン内ワープ", CommandType.Warp),
                }),
                ("カメラ", new[]
                {
                    ("視点変更", CommandType.CameraView),
                    ("視点変更解除", CommandType.CameraReset),
                    ("カメラシェイク", CommandType.CameraShake),
                    ("視点を向ける（振り返る）", CommandType.LookAt),
                }),
                ("オブジェクト", new[]
                {
                    ("アクティブにする", CommandType.ActivateObject),
                    ("非アクティブにする", CommandType.DeactivateObject),
                    ("移動", CommandType.MoveObject),
                    ("回転", CommandType.RotateObject),
                    ("スケール変更", CommandType.ScaleObject),
                    ("ドアを開ける", CommandType.OpenDoor),
                    ("ドアを閉める", CommandType.CloseDoor),
                    ("ドアを施錠", CommandType.LockDoor),
                    ("ドアを解錠", CommandType.UnlockDoor),
                }),
            };

            const float ColumnWidth = 168f;
            const float ButtonHeight = 22f;

            public override Vector2 GetWindowSize()
            {
                // いちばん長い列に合わせて高さを決める
                float tallest = 0f;
                foreach (var col in columns)
                {
                    float h = 0f;
                    foreach (int g in col) h += 30f + groups[g].items.Count * (ButtonHeight + 2f);
                    tallest = Mathf.Max(tallest, h);
                }
                // 枠（helpBox）の余白ぶんを足す
                return new Vector2(columns.Count * (ColumnWidth + 12f) + 16f, tallest + 40f);
            }

            public override void OnGUI(Rect rect)
            {
                GUILayout.Label("コマンドの挿入（選んだ行の上に入ります）", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (var col in columns)
                    {
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(ColumnWidth)))
                        {
                            foreach (int gi in col)
                            {
                                var g = groups[gi];
                                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(ColumnWidth)))
                                {
                                    GUILayout.Label(g.title, EditorStyles.miniBoldLabel);
                                    foreach (var item in g.items)
                                    {
                                        if (GUILayout.Button(item.label, GUILayout.Width(ColumnWidth - 10f), GUILayout.Height(ButtonHeight)))
                                        {
                                            owner.InsertNew(item.type, item.custom);
                                            editorWindow.Close();
                                            GUIUtility.ExitGUI();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }    }
}
