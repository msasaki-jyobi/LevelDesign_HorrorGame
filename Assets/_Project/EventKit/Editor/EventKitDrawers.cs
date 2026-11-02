using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EventKit.EditorTools
{
    [CustomPropertyDrawer(typeof(ItemIdAttribute))]
    public class ItemIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            property.stringValue = EventKitEditorUtil.IdPopup(position, label, property.stringValue, true);
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(FlagIdAttribute))]
    public class FlagIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            property.stringValue = EventKitEditorUtil.IdPopup(position, label, property.stringValue, false);
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(EventCondition))]
    public class EventConditionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight + 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var typeProp = property.FindPropertyRelative("type");
            var idProp = property.FindPropertyRelative("id");
            position.height = EditorGUIUtility.singleLineHeight;
            var typeRect = new Rect(position.x, position.y, Mathf.Min(170f, position.width * 0.4f), position.height);
            var idRect = new Rect(typeRect.xMax + 4f, position.y, position.width - typeRect.width - 4f, position.height);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);
            var type = (ConditionType)typeProp.intValue;
            bool isItem = type == ConditionType.HasItem || type == ConditionType.NotHasItem;
            idProp.stringValue = EventKitEditorUtil.IdPopup(idRect, GUIContent.none, idProp.stringValue, isItem);
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(EventCommand))]
    public class EventCommandDrawer : PropertyDrawer
    {
        static float Line => EditorGUIUtility.singleLineHeight;
        static float Space => EditorGUIUtility.standardVerticalSpacing;

        static GUIStyle wrapArea;
        static GUIStyle WrapArea => wrapArea ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };

        static float TextHeight(SerializedProperty property)
        {
            string text = property.FindPropertyRelative("text").stringValue ?? "";
            float width = Mathf.Max(200f, EditorGUIUtility.currentViewWidth - 140f);
            return Mathf.Max(Line * 3f, WrapArea.CalcHeight(new GUIContent(text), width) + 4f);
        }

        static int ExtraLines(CommandType t, SerializedProperty property)
        {
            switch (t)
            {
                case CommandType.Message:
                    // 話者・音・振り向く先（＋振り向く先があれば秒数）
                    return property.FindPropertyRelative("target").objectReferenceValue != null ? 4 : 3;
                case CommandType.GiveItem:
                case CommandType.RemoveItem:
                case CommandType.SetFlag:
                case CommandType.LookAt:
                case CommandType.SetActive: return 2;
                case CommandType.PlaySound: return 3;
                case CommandType.EndEvent:
                case CommandType.Else:
                case CommandType.EndIf:
                case CommandType.When:
                case CommandType.EndChoices: return 0;
                case CommandType.ShowChoices: return 0;
                case CommandType.CameraView: return 6;      // ボタン・位置・向き・画角・ブレンド秒・次へ
                case CommandType.CameraReset: return 2;     // 秒数・次へ
                case CommandType.CameraShake: return 3;     // 秒数・強さ・次へ
                case CommandType.MoveObject: return 5;      // 対象・移動量・向き基準・秒数・次へ
                case CommandType.RotateObject: return 5;    // 対象・指定方法・角度・秒数・次へ
                case CommandType.ScaleObject: return 4;     // 対象・大きさ・秒数・次へ
                case CommandType.InvokeUnityEvent: return 0;
                case CommandType.Custom: return 0;
                case CommandType.PlayAnimation: return 6; // 対象・方法・名前/クリップ・切替秒・次へ・プレビュー
                case CommandType.TransferScene:
                case CommandType.TransferSceneLoading:
                {
                    // シーン・出現地点・ボタン・（Build Settings 警告）・フェードアウト・フェードイン・（ローディングUI）
                    string path = property.FindPropertyRelative("scenePath").stringValue;
                    int n = 5;
                    if (!string.IsNullOrEmpty(path) && !SpawnPointTools.InBuildSettings(path)) n++;
                    if (t == CommandType.TransferSceneLoading) n++;
                    return n;
                }
                case CommandType.Warp:
                    // ワープ先・ボタン・フェードあり・（フェードアウト・フェードイン）
                    return property.FindPropertyRelative("boolValue").boolValue ? 5 : 3;
                default: return 1;
            }
        }

        // 種類の選択肢（自動管理される行は選べないようにする）
        static int[] typeValues;
        static GUIContent[] typeLabels;

        static void EnsureTypeList()
        {
            if (typeValues != null) return;
            var values = new List<int>();
            var labels = new List<GUIContent>();
            foreach (var f in typeof(CommandType).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                var t = (CommandType)f.GetValue(null);
                if (CommandTypes.IsStructural(t)) continue;
                var attr = (InspectorNameAttribute)System.Attribute.GetCustomAttribute(f, typeof(InspectorNameAttribute));
                values.Add((int)t);
                labels.Add(new GUIContent(attr != null ? attr.displayName : t.ToString()));
            }
            typeValues = values.ToArray();
            typeLabels = labels.ToArray();
        }

        /// <summary>コマンド種類の表示名（InspectorName）。</summary>
        public static string TypeLabel(CommandType t)
        {
            var f = typeof(CommandType).GetField(t.ToString());
            var attr = f != null ? (InspectorNameAttribute)System.Attribute.GetCustomAttribute(f, typeof(InspectorNameAttribute)) : null;
            return attr != null ? attr.displayName : t.ToString();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var type = (CommandType)property.FindPropertyRelative("type").intValue;
            float h = (1 + ExtraLines(type, property)) * (Line + Space) + 4f;
            if (type == CommandType.ShowChoices) h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("choices"), true) + Space;
            if (type == CommandType.Message) h += TextHeight(property) + Space;
            if (type == CommandType.InvokeUnityEvent) h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("unityEvent"), true) + Space;
            if (type == CommandType.Custom) h += CustomHeight(property);
            return h;
        }

        static Color CategoryColor(CommandType t)
        {
            switch (t)
            {
                case CommandType.Message: return new Color(0.35f, 0.7f, 1f);
                case CommandType.GiveItem:
                case CommandType.RemoveItem: return new Color(1f, 0.8f, 0.3f);
                case CommandType.SetFlag: return new Color(0.5f, 1f, 0.5f);
                case CommandType.OpenDoor:
                case CommandType.CloseDoor:
                case CommandType.LockDoor:
                case CommandType.UnlockDoor: return new Color(0.8f, 0.6f, 0.4f);
                case CommandType.JumpScare:
                case CommandType.PlaySound:
                case CommandType.LookAt:
                case CommandType.FlickerFlashlight:
                case CommandType.FadeIn:
                case CommandType.FadeOut:
                case CommandType.CameraView:
                case CommandType.CameraReset:
                case CommandType.CameraShake: return new Color(1f, 0.35f, 0.35f);
                case CommandType.ActivateObject:
                case CommandType.DeactivateObject:
                case CommandType.MoveObject:
                case CommandType.RotateObject:
                case CommandType.ScaleObject: return new Color(0.8f, 0.6f, 0.4f);
                default: return new Color(0.7f, 0.7f, 0.7f);
            }
        }

        const string DefaultSpawnLabel = "（シーンの初期位置）";

        /// <summary>GameObject やコンポーネントから IEventPerformance を持つコンポーネントを探す。</summary>
        static MonoBehaviour ResolvePerformance(Object o)
        {
            if (o is MonoBehaviour mb && mb is IEventPerformance) return mb;
            var go = o as GameObject ?? (o as Component)?.gameObject;
            if (go == null) return null;
            foreach (var c in go.GetComponents<MonoBehaviour>())
                if (c is IEventPerformance) return c;
            return null;
        }

        // ───────── 拡張コマンド ─────────

        static float CustomHeight(SerializedProperty property)
        {
            var cp = property.FindPropertyRelative("custom");
            float h = Line + Space; // 種類の選択
            if (cp.managedReferenceValue == null) return h;
            var it = cp.Copy();
            var end = cp.GetEndProperty();
            bool enter = true;
            while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
            {
                h += EditorGUI.GetPropertyHeight(it, true) + Space;
                enter = false;
            }
            return h;
        }

        /// <summary>拡張コマンド：種類の選択と、そのクラスの public フィールドを設定欄として並べる。</summary>
        static void DrawCustom(Rect r, SerializedProperty property)
        {
            var cp = property.FindPropertyRelative("custom");
            var types = EventKitEditorUtil.CustomCommandTypes;
            var labels = new string[types.Count + 1];
            labels[0] = "（種類を選ぶ）";
            int current = 0;
            var value = cp.managedReferenceValue;
            for (int i = 0; i < types.Count; i++)
            {
                labels[i + 1] = types[i].path;
                if (value != null && value.GetType() == types[i].type) current = i + 1;
            }
            int chosen = EditorGUI.Popup(r, "拡張コマンド", current, labels);
            if (chosen != current)
                cp.managedReferenceValue = chosen == 0 ? null : System.Activator.CreateInstance(types[chosen - 1].type);
            r.y += Line + Space;
            if (cp.managedReferenceValue == null)
            {
                if (types.Count == 0) EditorGUI.LabelField(r, "拡張コマンドがありません（追加パックを入れると選べます）", EditorStyles.miniLabel);
                return;
            }
            var it = cp.Copy();
            var end = cp.GetEndProperty();
            bool enter = true;
            while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
            {
                float h = EditorGUI.GetPropertyHeight(it, true);
                EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, h), it, true);
                r.y += h + Space;
                enter = false;
            }
        }

        /// <summary>シーン遷移：遷移先シーン・出現地点・フェード・（ローディングUI）</summary>
        static void DrawTransfer(Rect r, SerializedProperty property, bool loading)
        {
            var pathProp = property.FindPropertyRelative("scenePath");
            var guidProp = property.FindPropertyRelative("sceneGuid");
            var spawnProp = property.FindPropertyRelative("spawnId");

            // シーンを移動・改名していたら GUID からパスを直す
            if (!string.IsNullOrEmpty(guidProp.stringValue))
            {
                string byGuid = AssetDatabase.GUIDToAssetPath(guidProp.stringValue);
                if (!string.IsNullOrEmpty(byGuid) && byGuid != pathProp.stringValue) pathProp.stringValue = byGuid;
            }
            string path = pathProp.stringValue;

            var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            EditorGUI.BeginChangeCheck();
            var picked = (SceneAsset)EditorGUI.ObjectField(r, new GUIContent("遷移先シーン"), asset, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                path = picked != null ? AssetDatabase.GetAssetPath(picked) : "";
                pathProp.stringValue = path;
                guidProp.stringValue = picked != null ? AssetDatabase.AssetPathToGUID(path) : "";
                var first = SpawnPointTools.Ids(path);
                spawnProp.stringValue = first.Contains("start") ? "start" : "";
            }
            r.y += Line + Space;

            // 出現地点
            var ids = SpawnPointTools.Ids(path);
            var options = new List<string> { DefaultSpawnLabel };
            options.AddRange(ids);
            int current = string.IsNullOrEmpty(spawnProp.stringValue) ? 0 : ids.IndexOf(spawnProp.stringValue) + 1;
            if (current == 0 && !string.IsNullOrEmpty(spawnProp.stringValue))
            {
                options.Add(spawnProp.stringValue + "（見つかりません）");
                current = options.Count - 1;
            }
            int chosen = EditorGUI.Popup(r, "到着する出現地点", current, options.ToArray());
            if (chosen != current) spawnProp.stringValue = chosen == 0 ? "" : chosen <= ids.Count ? ids[chosen - 1] : spawnProp.stringValue;
            r.y += Line + Space;

            // 遷移先シーンを開いて出現地点を置く・確認する
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool loaded = !string.IsNullOrEmpty(path) && scene.isLoaded;
            var owner = property.serializedObject.targetObject as Component;
            bool sameScene = loaded && owner != null && owner.gameObject.scene == scene;
            float bw = (r.width - 4f) / 3f;
            var b1 = new Rect(r.x, r.y, bw, Line);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(path) || sameScene))
            {
                if (!loaded)
                {
                    if (GUI.Button(b1, new GUIContent("遷移先を追加で開く", "今のシーンを開いたまま、遷移先シーンも Hierarchy に開く（出現地点を置くため）")))
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                        GUIUtility.ExitGUI();
                    }
                }
                else if (GUI.Button(b1, new GUIContent(sameScene ? "同じシーン" : "遷移先を閉じる", "追加で開いた遷移先シーンを閉じる（変更があれば保存を確認）")))
                {
                    if (UnityEditor.SceneManagement.EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new[] { scene }))
                        UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                    GUIUtility.ExitGUI();
                }
            }
            using (new EditorGUI.DisabledScope(!loaded))
            {
                if (GUI.Button(new Rect(r.x + bw + 2f, r.y, bw, Line), new GUIContent("＋ 出現地点を作る", "遷移先シーンの、シーンビュー中央の地面に、シーンビューの向きで作る")))
                {
                    var sp = SpawnPointTools.Create(scene, null);
                    spawnProp.stringValue = sp.spawnId;
                    property.serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
                var found = loaded ? SpawnPointTools.FindLoaded(path, spawnProp.stringValue) : null;
                using (new EditorGUI.DisabledScope(found == null))
                    if (GUI.Button(new Rect(r.x + (bw + 2f) * 2f, r.y, bw, Line), "出現地点を見る"))
                        SpawnPointTools.Frame(found.gameObject);
            }
            r.y += Line + Space;

            if (!string.IsNullOrEmpty(path) && !SpawnPointTools.InBuildSettings(path))
            {
                var warn = new Rect(r.x, r.y, r.width - 150f, Line);
                EditorGUI.LabelField(warn, new GUIContent("⚠ Build Settings に入っていません（ビルドすると移動できません）"), EditorStyles.miniBoldLabel);
                if (GUI.Button(new Rect(r.xMax - 146f, r.y, 146f, Line), "Build Settings に追加"))
                    SpawnPointTools.AddToBuildSettings(path);
                r.y += Line + Space;
            }

            EditorGUI.PropertyField(r, property.FindPropertyRelative("duration"), new GUIContent("フェードアウト秒", "画面が暗くなるまでの秒数"));
            r.y += Line + Space;
            EditorGUI.PropertyField(r, property.FindPropertyRelative("fadeInDuration"), new GUIContent("フェードイン秒", "画面が明るくなるまでの秒数"));
            if (loading)
            {
                r.y += Line + Space;
                EditorGUI.PropertyField(r, property.FindPropertyRelative("loadingPrefab"), new GUIContent("ローディングUIプレハブ", "読み込み中に表示するUI。LoadingScreen を付けると進み具合も出せる"));
            }
        }

        static void DrawWaitMode(Rect r, SerializedProperty property) =>
            EditorGUI.PropertyField(r, property.FindPropertyRelative("waitMode"), new GUIContent("次に進める条件", "そのまま次へ：動きと同時に次のコマンドへ／終了するまで待機：動き終わってから次へ"));

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            bool oldWide = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true; // Vector3 を1行で出す
            EditorGUIUtility.labelWidth = 110f;

            var typeProp = property.FindPropertyRelative("type");
            var type = (CommandType)typeProp.intValue;
            EditorGUI.DrawRect(new Rect(position.x - 4f, position.y + 2f, 3f, position.height - 6f), CategoryColor(type));

            var r = new Rect(position.x, position.y + 2f, position.width, Line);
            if (CommandTypes.IsStructural(type))
            {
                // 条件分岐・選択肢の行は種類を変えられない（ブロックが壊れるため）
                EditorGUI.LabelField(r, "コマンド", TypeLabel(type));
            }
            else
            {
                EnsureTypeList();
                int current = System.Array.IndexOf(typeValues, (int)type);
                int chosen = EditorGUI.Popup(r, new GUIContent("コマンド"), Mathf.Max(0, current), typeLabels);
                if (chosen != current) typeProp.intValue = typeValues[chosen];
            }
            r.y += Line + Space;

            switch (type)
            {
                case CommandType.Message:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("speaker"), new GUIContent("話者（任意）"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("clip"), new GUIContent("音（任意）", "表示と同時に鳴らす声・効果音"));
                    r.y += Line + Space;
                    var lookProp = property.FindPropertyRelative("target");
                    EditorGUI.PropertyField(r, lookProp, new GUIContent("振り向く先（任意）", "指定すると、そこから音が鳴り、プレイヤーがそちらへ振り向いてから会話が出る"));
                    r.y += Line + Space;
                    if (lookProp.objectReferenceValue != null)
                    {
                        EditorGUI.PropertyField(r, property.FindPropertyRelative("duration"), new GUIContent("振り向く秒数"));
                        r.y += Line + Space;
                    }
                    var textProp = property.FindPropertyRelative("text");
                    float th = TextHeight(property);
                    var labelRect = new Rect(r.x, r.y, EditorGUIUtility.labelWidth, Line);
                    EditorGUI.LabelField(labelRect, "本文");
                    var areaRect = new Rect(r.x + EditorGUIUtility.labelWidth + 2f, r.y, r.width - EditorGUIUtility.labelWidth - 2f, th);
                    EditorGUI.BeginChangeCheck();
                    string text = EditorGUI.TextArea(areaRect, textProp.stringValue, WrapArea);
                    if (EditorGUI.EndChangeCheck()) textProp.stringValue = text;
                    break;

                case CommandType.GiveItem:
                case CommandType.RemoveItem:
                    var itemProp = property.FindPropertyRelative("itemId");
                    itemProp.stringValue = EventKitEditorUtil.IdPopup(r, new GUIContent("アイテム"), itemProp.stringValue, true);
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("silent"), new GUIContent("通知を出さない"));
                    break;

                case CommandType.SetFlag:
                    var flagProp = property.FindPropertyRelative("flagId");
                    flagProp.stringValue = EventKitEditorUtil.IdPopup(r, new GUIContent("フラグ"), flagProp.stringValue, false);
                    r.y += Line + Space;
                    var boolProp = property.FindPropertyRelative("boolValue");
                    boolProp.boolValue = EditorGUI.Popup(r, "値", boolProp.boolValue ? 0 : 1, new[] { "ON", "OFF" }) == 0;
                    break;

                case CommandType.OpenDoor:
                case CommandType.CloseDoor:
                case CommandType.LockDoor:
                case CommandType.UnlockDoor:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("door"), new GUIContent("ドア"));
                    break;

                case CommandType.JumpScare:
                {
                    var perfProp = property.FindPropertyRelative("jumpScare");
                    EditorGUI.BeginChangeCheck();
                    var picked = EditorGUI.ObjectField(r, new GUIContent("演出オブジェクト", "IEventPerformance を持つコンポーネント（JumpScare など）。GameObject をドロップしても探す"), perfProp.objectReferenceValue, typeof(Object), true);
                    if (EditorGUI.EndChangeCheck()) perfProp.objectReferenceValue = ResolvePerformance(picked);
                    break;
                }

                case CommandType.Custom:
                    DrawCustom(r, property);
                    break;

                case CommandType.PlayAnimation:
                    AnimationCommandGUI.Draw(r, property, Line, Space);
                    break;

                case CommandType.PlaySound:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("clip"), new GUIContent("効果音"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("target"), new GUIContent("鳴らす場所（任意）", "指定するとその位置から立体音響で鳴る。空欄なら画面全体で鳴る"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("boolValue"), new GUIContent("終わるまで待つ"));
                    break;

                case CommandType.LookAt:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("target"), new GUIContent("向く先"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("duration"), new GUIContent("振り向く秒数"));
                    break;

                case CommandType.If:
                {
                    var condRect = new Rect(r.x + EditorGUIUtility.labelWidth, r.y, r.width - EditorGUIUtility.labelWidth, Line);
                    EditorGUI.LabelField(new Rect(r.x, r.y, EditorGUIUtility.labelWidth, Line), "条件");
                    EditorGUI.PropertyField(condRect, property.FindPropertyRelative("condition"), GUIContent.none);
                    break;
                }

                case CommandType.ShowChoices:
                {
                    var listProp = property.FindPropertyRelative("choices");
                    r.height = EditorGUI.GetPropertyHeight(listProp, true);
                    EditorGUI.PropertyField(r, listProp, new GUIContent("選択肢"), true);
                    break;
                }

                case CommandType.CallEvent:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("callEvent"), new GUIContent("実行するイベント"));
                    break;

                case CommandType.FlickerFlashlight:
                case CommandType.FadeIn:
                case CommandType.FadeOut:
                case CommandType.Wait:
                    string tip = type == CommandType.FadeOut ? "画面が暗くなるまでの秒数（フェードアウト＝暗転）"
                        : type == CommandType.FadeIn ? "画面が明るくなるまでの秒数（フェードイン＝暗転から戻る）" : "";
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("duration"), new GUIContent("秒数", tip));
                    break;

                case CommandType.SetActive:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("target"), new GUIContent("対象"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("boolValue"), new GUIContent("表示する"));
                    break;

                case CommandType.ActivateObject:
                case CommandType.DeactivateObject:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("target"), new GUIContent("対象"));
                    break;

                case CommandType.CameraView:
                {
                    float bw = (r.width - 4f) / 3f;
                    if (GUI.Button(new Rect(r.x, r.y, bw, Line), new GUIContent("シーンビューから設定", "今のシーンビューの位置・向きを視点にする")))
                        CameraViewWindow.SetFromSceneView(property);
                    if (GUI.Button(new Rect(r.x + bw + 2f, r.y, bw, Line), new GUIContent("シーンビューで見る", "シーンビューをこの視点に合わせる（データは変わらない）")))
                        CameraViewWindow.AlignSceneView(property);
                    if (GUI.Button(new Rect(r.x + (bw + 2f) * 2f, r.y, bw, Line), new GUIContent("カメラ操作ウィンドウ", "プレビュー付きのウィンドウで設定する"))
                        && CameraViewWindow.TryParsePath(property, out int pg, out int ix))
                        CameraViewWindow.Open(property.serializedObject.targetObject as GameEvent, pg, ix);
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("viewPosition"), new GUIContent("位置"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("viewRotation"), new GUIContent("向き"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("viewFov"), new GUIContent("画角（0=通常）"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("duration"), new GUIContent("ブレンド秒数", "この秒数をかけて視点が切り替わる（0で瞬時）"));
                    r.y += Line + Space;
                    DrawWaitMode(r, property);
                    break;
                }

                case CommandType.CameraReset:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("duration"), new GUIContent("戻る秒数", "この秒数をかけて通常の視点に戻る（0で瞬時）"));
                    r.y += Line + Space;
                    DrawWaitMode(r, property);
                    break;

                case CommandType.CameraShake:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("duration"), new GUIContent("揺れる秒数"));
                    r.y += Line + Space;
                    EditorGUI.Slider(r, property.FindPropertyRelative("strength"), 0.1f, 5f, new GUIContent("強さ"));
                    r.y += Line + Space;
                    DrawWaitMode(r, property);
                    break;

                case CommandType.MoveObject:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("target"), new GUIContent("対象"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("vector"), new GUIContent("移動量(m)", "今の位置からどれだけ動かすか。X＝左右　Y＝上下　Z＝前後"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("localSpace"), new GUIContent("オブジェクトの向き基準", "OFF：ワールドの向き（Y＝真上）／ON：オブジェクト自身の向き（Z＝オブジェクトの前）"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("duration"), new GUIContent("秒数", "何秒かけて動くか（0で瞬時）"));
                    r.y += Line + Space;
                    DrawWaitMode(r, property);
                    break;

                case CommandType.RotateObject:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("target"), new GUIContent("対象"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("rotateMode"), new GUIContent("指定方法"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("vector"), new GUIContent("角度 XYZ"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("duration"), new GUIContent("秒数", "何秒かけて回るか（0で瞬時）"));
                    r.y += Line + Space;
                    DrawWaitMode(r, property);
                    break;

                case CommandType.ScaleObject:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("target"), new GUIContent("対象"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("vector"), new GUIContent("スケール XYZ", "最終的な大きさ（1,1,1 が元の大きさの目安）"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("duration"), new GUIContent("秒数", "何秒かけてその大きさになるか（0で瞬時）"));
                    r.y += Line + Space;
                    DrawWaitMode(r, property);
                    break;

                case CommandType.TransferScene:
                case CommandType.TransferSceneLoading:
                    DrawTransfer(r, property, type == CommandType.TransferSceneLoading);
                    break;

                case CommandType.Warp:
                {
                    var targetProp = property.FindPropertyRelative("target");
                    EditorGUI.PropertyField(r, targetProp, new GUIContent("ワープ先", "出現地点（緑の人型）か、任意のオブジェクト。その位置に、そのY回転の向きで立つ"));
                    r.y += Line + Space;
                    float bw = (r.width - 2f) / 2f;
                    if (GUI.Button(new Rect(r.x, r.y, bw, Line), new GUIContent("＋ シーンビューの位置に作る", "シーンビュー中央の地面に、シーンビューの向きで出現地点を作ってワープ先にする")))
                    {
                        var owner = property.serializedObject.targetObject as Component;
                        var sp = SpawnPointTools.Create(owner != null ? owner.gameObject.scene : UnityEngine.SceneManagement.SceneManager.GetActiveScene(), null);
                        targetProp.objectReferenceValue = sp.gameObject;
                        property.serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }
                    using (new EditorGUI.DisabledScope(targetProp.objectReferenceValue == null))
                        if (GUI.Button(new Rect(r.x + bw + 2f, r.y, bw, Line), "ワープ先を見る"))
                            SpawnPointTools.Frame(targetProp.objectReferenceValue as GameObject);
                    r.y += Line + Space;
                    var fadeProp = property.FindPropertyRelative("boolValue");
                    EditorGUI.PropertyField(r, fadeProp, new GUIContent("フェードあり", "OFF なら一瞬で移動する"));
                    if (fadeProp.boolValue)
                    {
                        r.y += Line + Space;
                        EditorGUI.PropertyField(r, property.FindPropertyRelative("duration"), new GUIContent("フェードアウト秒", "画面が暗くなるまでの秒数"));
                        r.y += Line + Space;
                        EditorGUI.PropertyField(r, property.FindPropertyRelative("fadeInDuration"), new GUIContent("フェードイン秒", "画面が明るくなるまでの秒数"));
                    }
                    break;
                }

                case CommandType.InvokeUnityEvent:
                {
                    var evProp = property.FindPropertyRelative("unityEvent");
                    r.height = EditorGUI.GetPropertyHeight(evProp, true);
                    EditorGUI.PropertyField(r, evProp, new GUIContent("実行する UnityEvent"), true);
                    break;
                }
            }

            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUIUtility.wideMode = oldWide;
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
