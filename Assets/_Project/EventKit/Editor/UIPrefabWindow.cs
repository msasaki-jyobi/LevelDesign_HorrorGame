using UnityEditor;
using UnityEngine;

namespace EventKit.EditorTools
{
    /// <summary>
    /// UIプレハブ管理。会話ウィンドウなどの UI プレハブを一覧し、開く・複製して差し替える・標準に戻すができる。
    /// メニュー：EventKit > UIプレハブ管理
    /// </summary>
    public class UIPrefabWindow : EditorWindow
    {
        [MenuItem("EventKit/UIプレハブ管理", priority = 2)]
        public static void Open()
        {
            var w = GetWindow<UIPrefabWindow>();
            w.titleContent = new GUIContent("UIプレハブ管理");
            w.minSize = new Vector2(520, 420);
            w.Show();
        }

        EventUISettings settings;
        Vector2 scroll;

        void OnEnable() => settings = UIPrefabTools.EnsureSettings();

        void OnGUI()
        {
            if (settings == null) settings = UIPrefabTools.EnsureSettings();
            var so = new SerializedObject(settings);
            so.Update();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "ゲーム中の UI はここで指定したプレハブから作られます。\n" +
                "見た目を変えるときは「複製して差し替え」→ 複製したプレハブを開いて自由に編集してください。\n" +
                "（標準プレハブを直接編集すると、EventKit を更新したときに上書きされます）", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField("設定アセット", settings, typeof(EventUISettings), false);
                if (GUILayout.Button("選択", GUILayout.Width(50))) Selection.activeObject = settings;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(6);
            Slot(so, "dialogue", "会話ウィンドウ", "話者名・本文・送りマーク", UIPrefabTools.DefaultDialogue);
            Slot(so, "choices", "選択肢", "選択肢の見本（ItemTemplate）を数だけ複製して並べる", UIPrefabTools.DefaultChoices);
            Slot(so, "prompt", "調べるプロンプト・照準", "「[E] 調べる」と画面中央の点", UIPrefabTools.DefaultPrompt);
            Slot(so, "inventory", "所持品ウィンドウ", "キーアイテムの一覧・説明・アイコン", UIPrefabTools.DefaultInventory);
            Slot(so, "fade", "フェード", "フェードアウト（暗くなる）・フェードイン（明るくなる）、シーン遷移の暗転", UIPrefabTools.DefaultFade);
            Slot(so, "loading", "ロード画面", "ロード画面付きシーン遷移（コマンドでの指定が無いとき）", () => UIPrefabTools.DefaultPrefab().GetComponent<LoadingScreen>());

            EditorGUILayout.Space(8);
            EditorGUILayout.PropertyField(so.FindProperty("font"), new GUIContent("フォント", "未設定なら OS の日本語フォント"));
            EditorGUILayout.PropertyField(so.FindProperty("osFontNames"), new GUIContent("OSフォントの候補"), true);
            EditorGUILayout.PropertyField(so.FindProperty("sortingOrder"), new GUIContent("Canvas の表示順"));
            EditorGUILayout.PropertyField(so.FindProperty("referenceResolution"), new GUIContent("基準解像度"));
            EditorGUILayout.PropertyField(so.FindProperty("matchWidthOrHeight"), new GUIContent("幅/高さの合わせ方"));
            EditorGUILayout.EndScrollView();

            so.ApplyModifiedProperties();
        }

        void Slot<T>(SerializedObject so, string field, string title, string help, System.Func<T> makeDefault) where T : Component
        {
            var prop = so.FindProperty(field);
            var current = prop.objectReferenceValue as T;
            var standard = makeDefault();
            bool isStandard = current != null && standard != null && current.gameObject == standard.gameObject;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel, GUILayout.Width(150));
                    EditorGUILayout.LabelField(current == null ? "（なし：表示されません）" : isStandard ? "標準" : "差し替え済み", EditorStyles.miniLabel);
                }
                EditorGUILayout.LabelField(help, EditorStyles.wordWrappedMiniLabel);
                prop.objectReferenceValue = EditorGUILayout.ObjectField(prop.objectReferenceValue, typeof(T), false);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(current == null))
                        if (GUILayout.Button("開いて編集"))
                            AssetDatabase.OpenAsset(current.gameObject);
                    using (new EditorGUI.DisabledScope(current == null))
                        if (GUILayout.Button("複製して差し替え"))
                        {
                            var copy = UIPrefabTools.Duplicate(current, current.gameObject.name + "_Custom");
                            if (copy != null)
                            {
                                prop.objectReferenceValue = copy;
                                so.ApplyModifiedProperties();
                                EditorGUIUtility.PingObject(copy.gameObject);
                            }
                            GUIUtility.ExitGUI();
                        }
                    using (new EditorGUI.DisabledScope(isStandard || standard == null))
                        if (GUILayout.Button("標準に戻す"))
                            prop.objectReferenceValue = standard;
                }
            }
        }
    }
}
