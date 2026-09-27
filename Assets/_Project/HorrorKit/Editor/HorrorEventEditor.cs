using UnityEditor;
using UnityEngine;

namespace HorrorKit.EditorTools
{
    [CustomEditor(typeof(HorrorEvent))]
    public class HorrorEventEditor : Editor
    {
        SerializedProperty nameProp, pagesProp;
        int selectedPage;

        void OnEnable()
        {
            nameProp = serializedObject.FindProperty("eventName");
            pagesProp = serializedObject.FindProperty("pages");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var ev = (HorrorEvent)target;

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
            var labels = new string[pagesProp.arraySize];
            for (int i = 0; i < labels.Length; i++) labels[i] = $"ページ {i + 1}";
            selectedPage = GUILayout.Toolbar(selectedPage, labels);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("＋ 新規ページ", EditorStyles.miniButtonLeft)) AddPage(pagesProp.arraySize);
                if (GUILayout.Button("複製", EditorStyles.miniButtonMid))
                {
                    pagesProp.InsertArrayElementAtIndex(selectedPage);
                    selectedPage++;
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
                EditorGUILayout.PropertyField(page.FindPropertyRelative("commands"), new GUIContent("実行内容"), true);
            }

            EditorGUILayout.HelpBox("番号の大きいページから順に条件を判定し、最初に条件を満たしたページが有効になります。\n「一度だけ実行」したページは以後スキップされ、下のページに移ります。", MessageType.None);

            serializedObject.ApplyModifiedProperties();
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

        void DrawColliderHelp(HorrorEvent ev)
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
