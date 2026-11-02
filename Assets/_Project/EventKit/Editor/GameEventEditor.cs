using UnityEditor;
using UnityEngine;

namespace EventKit.EditorTools
{
    [CustomEditor(typeof(GameEvent))]
    public class GameEventEditor : Editor
    {
        SerializedProperty nameProp, pagesProp;
        int selectedPage;

        [System.Serializable] class PageCopy { public EventPage page; }

        void OnEnable()
        {
            nameProp = serializedObject.FindProperty("eventName");
            pagesProp = serializedObject.FindProperty("pages");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var ev = (GameEvent)target;

            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
            if (GUILayout.Button("▶ イベントエディタで開く（Ctrl+Shift+E）", GUILayout.Height(30)))
                EventEditorWindow.Open(ev, Mathf.Clamp(selectedPage, 0, Mathf.Max(0, pagesProp.arraySize - 1)));
            GUI.backgroundColor = oldBg;
            EditorGUILayout.Space(4);

            EditorGUILayout.PropertyField(nameProp, new GUIContent("イベント名"));

            if (Application.isPlaying)
            {
                int active = ev.ActivePageIndex;
                EditorGUILayout.HelpBox(active >= 0 ? $"実行中: 現在有効なページは {active + 1}" : "実行中: 有効なページなし", MessageType.Info);
            }

            DrawColliderHelp(ev);

            if (pagesProp.arraySize == 0)
            {
                if (GUILayout.Button("ページを追加")) AddPage(0);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            selectedPage = Mathf.Clamp(selectedPage, 0, pagesProp.arraySize - 1);

            EditorGUILayout.Space(4);
            selectedPage = PageTabBar.Draw(ev.pages, selectedPage, Application.isPlaying ? ev.ActivePageIndex : -1,
                (from, to) => pagesProp.MoveArrayElement(from, to), 22f);
            EditorGUILayout.LabelField("タブをドラッグで並び替え／「条」＝出現条件あり（マウスを乗せると条件を表示）", EditorStyles.centeredGreyMiniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("＋ 新規ページ", EditorStyles.miniButtonLeft)) AddPage(pagesProp.arraySize);
                if (GUILayout.Button("複製", EditorStyles.miniButtonMid))
                {
                    // 拡張コマンド（SerializeReference）を共有しないよう、JSON 経由で丸ごと複製する
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(ev, "ページを複製");
                    var copy = new PageCopy();
                    EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(new PageCopy { page = ev.pages[selectedPage] }), copy);
                    ev.pages.Insert(selectedPage + 1, copy.page);
                    selectedPage++;
                    EditorUtility.SetDirty(ev);
                    serializedObject.Update();
                    GUIUtility.ExitGUI();
                }
                using (new EditorGUI.DisabledScope(selectedPage == 0))
                    if (GUILayout.Button("◀", EditorStyles.miniButtonMid))
                    {
                        pagesProp.MoveArrayElement(selectedPage, selectedPage - 1);
                        selectedPage--;
                    }
                using (new EditorGUI.DisabledScope(selectedPage >= pagesProp.arraySize - 1))
                    if (GUILayout.Button("▶", EditorStyles.miniButtonMid))
                    {
                        pagesProp.MoveArrayElement(selectedPage, selectedPage + 1);
                        selectedPage++;
                    }
                if (GUILayout.Button("削除", EditorStyles.miniButtonRight) &&
                    EditorUtility.DisplayDialog("ページ削除", $"ページ {selectedPage + 1} を削除しますか？", "削除", "キャンセル"))
                {
                    pagesProp.DeleteArrayElementAtIndex(selectedPage);
                    selectedPage = Mathf.Max(0, selectedPage - 1);
                    serializedObject.ApplyModifiedProperties();
                    return;
                }
            }

            var page = pagesProp.GetArrayElementAtIndex(selectedPage);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(page.FindPropertyRelative("memo"), new GUIContent("メモ"));
                var trigger = page.FindPropertyRelative("trigger");
                EditorGUILayout.PropertyField(trigger, new GUIContent("起動方法"));
                if ((EventTrigger)trigger.intValue == EventTrigger.Interact)
                    EditorGUILayout.PropertyField(page.FindPropertyRelative("prompt"), new GUIContent("表示する行動名"));
                if ((EventTrigger)trigger.intValue != EventTrigger.AutoStart)
                    EditorGUILayout.PropertyField(page.FindPropertyRelative("runOnce"), new GUIContent("一度だけ実行"));

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(page.FindPropertyRelative("conditions"), new GUIContent("出現条件（すべて満たすと有効）"), true);
                EditorGUILayout.Space(2);
                DrawCommandSummary(ev, selectedPage);
            }

            EditorGUILayout.HelpBox("番号の大きいページから順に条件を判定し、最初に条件を満たしたページが有効になります。\n「一度だけ実行」したページは以後スキップされ、下のページに移ります。", MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }

        static GUIStyle lineStyle;

        /// <summary>実行内容を RPGツクール風の1行表示で並べ、イベントエディタを開くボタンを出す。</summary>
        static void DrawCommandSummary(GameEvent ev, int pageIndex)
        {
            EditorGUILayout.LabelField("実行内容", EditorStyles.boldLabel);
            var cmds = ev.pages[pageIndex].commands;
            var indents = EventKitEditorUtil.Indents(cmds);
            lineStyle ??= new GUIStyle(EditorStyles.label) { fontSize = 11, clipping = TextClipping.Clip };

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (cmds.Count == 0) EditorGUILayout.LabelField("（まだコマンドがありません）", EditorStyles.miniLabel);
                for (int i = 0; i < cmds.Count; i++)
                {
                    var r = GUILayoutUtility.GetRect(10, 17, GUILayout.ExpandWidth(true));
                    r.xMin += indents[i] * 14;
                    var style = new GUIStyle(lineStyle);
                    style.normal.textColor = EventKitEditorUtil.CommandColor(cmds[i].type);
                    GUI.Label(r, EventKitEditorUtil.CommandLine(cmds, i), style);
                    if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 2 && r.Contains(Event.current.mousePosition))
                    {
                        EventEditorWindow.Open(ev, pageIndex, i);
                        Event.current.Use();
                    }
                }
            }
            if (GUILayout.Button("イベントエディタで編集する（Ctrl+Shift+E）", GUILayout.Height(28)))
                EventEditorWindow.Open(ev, pageIndex);
        }

        void AddPage(int index)
        {
            pagesProp.InsertArrayElementAtIndex(Mathf.Min(index, pagesProp.arraySize));
            var p = pagesProp.GetArrayElementAtIndex(index);
            p.FindPropertyRelative("memo").stringValue = "";
            p.FindPropertyRelative("trigger").intValue = (int)EventTrigger.Interact;
            p.FindPropertyRelative("prompt").stringValue = "調べる";
            p.FindPropertyRelative("runOnce").boolValue = false;
            p.FindPropertyRelative("conditions").ClearArray();
            p.FindPropertyRelative("commands").ClearArray();
            selectedPage = index;
        }

        void DrawColliderHelp(GameEvent ev)
        {
            bool needsSolid = false, needsTrigger = false;
            foreach (var p in ev.pages)
            {
                if (p == null) continue;
                if (p.trigger == EventTrigger.Interact) needsSolid = true;
                if (p.trigger == EventTrigger.EnterArea) needsTrigger = true;
            }
            bool hasSolid = false, hasTrigger = false;
            foreach (var c in ev.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) hasTrigger = true;
                else hasSolid = true;
            }

            if (needsSolid && !hasSolid)
            {
                EditorGUILayout.HelpBox("「調べる」には通常の(トリガーでない) Collider が必要です。", MessageType.Warning);
                if (GUILayout.Button("BoxCollider を追加")) Undo.AddComponent<BoxCollider>(ev.gameObject);
            }
            if (needsTrigger && !hasTrigger)
            {
                EditorGUILayout.HelpBox("「エリアに入る」には Is Trigger の Collider が必要です。", MessageType.Warning);
                if (GUILayout.Button("トリガー BoxCollider を追加"))
                {
                    var box = Undo.AddComponent<BoxCollider>(ev.gameObject);
                    box.isTrigger = true;
                    box.size = new Vector3(2f, 2f, 2f);
                }
            }
        }
    }
}
