using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
#if EVENTKIT_URP
using UnityEngine.Rendering.Universal;
#endif

namespace EventKit.EditorTools
{
    /// <summary>
    /// 「視点変更」コマンドのカメラ操作ウィンドウ。
    /// シーンビューで視点を決めて「今のシーンビューを視点に設定」、プレビューでゲーム画面の見え方を確認できる。
    /// イベントエディタで別の「視点変更」を選ぶと、こちらも自動で切り替わる。
    /// </summary>
    public class CameraViewWindow : EditorWindow
    {
        [SerializeField] GameEvent ev;
        [SerializeField] int page, index;
        [SerializeField] bool follow;

        [SerializeField] bool flashlight = true;
        RenderTexture previewTexture;
        static Camera previewCamera;
        static Light previewLight;

        public static void Open(GameEvent ev, int page, int index)
        {
            var w = GetWindow<CameraViewWindow>();
            w.titleContent = new GUIContent("視点変更（カメラ）");
            w.minSize = new Vector2(360, 420);
            w.SetTarget(ev, page, index);
            w.Show();
        }

        /// <summary>ウィンドウが開いていれば、対象のコマンドを切り替える。</summary>
        public static void Follow(GameEvent ev, int page, int index)
        {
            if (!HasOpenInstances<CameraViewWindow>()) return;
            var w = GetWindow<CameraViewWindow>(false, null, false);
            if (w.ev != ev || w.page != page || w.index != index) w.SetTarget(ev, page, index);
        }

        void SetTarget(GameEvent e, int p, int i)
        {
            ev = e;
            page = p;
            index = i;
            follow = false;
            Repaint();
        }

        SerializedProperty FindCommand(SerializedObject so)
        {
            if (ev == null || page < 0 || page >= ev.pages.Count) return null;
            var cmds = ev.pages[page].commands;
            if (index < 0 || index >= cmds.Count || cmds[index] == null || cmds[index].type != CommandType.CameraView) return null;
            return so.FindProperty($"pages.Array.data[{page}].commands.Array.data[{index}]");
        }

        void OnDisable()
        {
            if (previewTexture != null) DestroyImmediate(previewTexture);
            if (previewCamera != null) DestroyImmediate(previewCamera.gameObject);
            previewCamera = null;
            previewLight = null;
        }

        void Update()
        {
            // シーンビュー追従中は、シーンビューを動かすとそのまま視点に入る
            if (!follow || ev == null) return;
            var so = new SerializedObject(ev);
            var prop = FindCommand(so);
            if (prop == null) { follow = false; return; }
            if (SetFromSceneView(prop)) Repaint();
        }

        void OnGUI()
        {
            var so = ev != null ? new SerializedObject(ev) : null;
            var prop = so != null ? FindCommand(so) : null;
            if (prop == null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox("イベントエディタで「視点変更」コマンドを選ぶと、ここで視点を設定できます。", MessageType.Info);
                return;
            }
            so.Update();

            EditorGUILayout.LabelField($"{ev.DisplayName}　ページ {page + 1}　{index + 1}行目", EditorStyles.boldLabel);

            // プレビュー
            float w = position.width - 12f;
            var rect = GUILayoutUtility.GetRect(w, w * 9f / 16f, GUILayout.ExpandWidth(true));
            rect.xMin += 4f;
            rect.xMax -= 4f;
            if (Event.current.type == EventType.Repaint)
            {
                var pos = prop.FindPropertyRelative("viewPosition").vector3Value;
                var rot = Quaternion.Euler(prop.FindPropertyRelative("viewRotation").vector3Value);
                float fov = prop.FindPropertyRelative("viewFov").floatValue;
                var tex = RenderPreview((int)rect.width, (int)rect.height, pos, rot, fov > 0f ? fov : PlayerFov());
                if (tex != null) GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, false);
                else EditorGUI.DrawRect(rect, Color.black);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("プレビュー（ゲーム画面での見え方）", EditorStyles.miniLabel);
                flashlight = EditorGUILayout.ToggleLeft("懐中電灯で照らして見る", flashlight, GUILayout.Width(150));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("今のシーンビューを視点に設定", GUILayout.Height(28)))
                {
                    SetFromSceneView(prop);
                    so.ApplyModifiedProperties();
                }
                if (GUILayout.Button("シーンビューをこの視点に合わせる", GUILayout.Height(28)))
                    AlignSceneView(prop);
            }
            var oldBg = GUI.backgroundColor;
            if (follow) GUI.backgroundColor = new Color(1f, 0.6f, 0.3f);
            follow = GUILayout.Toggle(follow, follow ? "● シーンビューに追従中（もう一度押すと止まる）" : "シーンビューに追従（動かした視点がそのまま入る）", "Button", GUILayout.Height(24));
            GUI.backgroundColor = oldBg;

            EditorGUILayout.Space(6);
            float lw = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 120f;
            EditorGUIUtility.wideMode = true;
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("viewPosition"), new GUIContent("位置"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("viewRotation"), new GUIContent("向き"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("viewFov"), new GUIContent("画角（0=通常）"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("duration"), new GUIContent("ブレンド秒数", "この秒数をかけて視点が切り替わる（0で瞬時）"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("waitMode"), new GUIContent("次に進める条件"));
            EditorGUIUtility.labelWidth = lw;
            so.ApplyModifiedProperties();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("シーンビューで見せたい構図に合わせてから「今のシーンビューを視点に設定」を押してください。イベントを選ぶとシーンに黄色い枠でカメラ位置が出ます。", EditorStyles.wordWrappedMiniLabel);

            if (follow) Repaint();
        }

        // ───────── 共通の操作（コマンドの詳細パネルからも使う） ─────────

        /// <summary>シーンビューのカメラ位置・向きをコマンドに入れる。変化があれば true。</summary>
        public static bool SetFromSceneView(SerializedProperty cmd)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null || sv.camera == null) return false;
            var t = sv.camera.transform;
            var posProp = cmd.FindPropertyRelative("viewPosition");
            var rotProp = cmd.FindPropertyRelative("viewRotation");
            var euler = t.rotation.eulerAngles;
            if ((posProp.vector3Value - t.position).sqrMagnitude < 1e-6f && (rotProp.vector3Value - euler).sqrMagnitude < 1e-4f) return false;
            posProp.vector3Value = t.position;
            rotProp.vector3Value = euler;
            cmd.serializedObject.ApplyModifiedProperties();
            return true;
        }

        /// <summary>シーンビューをコマンドの視点に合わせる（見るだけでデータは変えない）。</summary>
        public static void AlignSceneView(SerializedProperty cmd)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return;
            var pos = cmd.FindPropertyRelative("viewPosition").vector3Value;
            var rot = Quaternion.Euler(cmd.FindPropertyRelative("viewRotation").vector3Value);
            // pivot をカメラの少し前に置き、カメラ位置がちょうど指定位置になるようにする
            float size = 1f;
            float dist = size / Mathf.Tan(sv.camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            sv.LookAtDirect(pos + rot * Vector3.forward * dist, rot, size);
            sv.Repaint();
        }

        /// <summary>プロパティのパス（pages.Array.data[p].commands.Array.data[i]）からページと行を取り出す。</summary>
        public static bool TryParsePath(SerializedProperty cmd, out int page, out int index)
        {
            var m = Regex.Match(cmd.propertyPath, @"pages\.Array\.data\[(\d+)\]\.commands\.Array\.data\[(\d+)\]");
            page = m.Success ? int.Parse(m.Groups[1].Value) : -1;
            index = m.Success ? int.Parse(m.Groups[2].Value) : -1;
            return m.Success;
        }

        /// <summary>プレイヤーの画角（シーンの CinemachineCamera のうち優先度が一番高いもの。無ければ Main Camera）</summary>
        static float PlayerFov()
        {
            Unity.Cinemachine.CinemachineCamera best = null;
            foreach (var c in FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsInactive.Exclude))
                if (best == null || c.Priority.Value > best.Priority.Value) best = c;
            if (best != null) return best.Lens.FieldOfView;
            return Camera.main != null ? Camera.main.fieldOfView : 60f;
        }

        // ───────── プレビュー描画 ─────────

        Texture RenderPreview(int width, int height, Vector3 pos, Quaternion rot, float fov)
        {
            if (width < 8 || height < 8) return null;
            if (previewTexture == null || previewTexture.width != width || previewTexture.height != height)
            {
                if (previewTexture != null) DestroyImmediate(previewTexture);
                previewTexture = new RenderTexture(width, height, 24) { hideFlags = HideFlags.HideAndDontSave };
            }

            if (previewCamera == null)
            {
                // スクリプト再読み込みで残った古い隠しカメラを片付ける
                foreach (var old in Resources.FindObjectsOfTypeAll<Camera>())
                    if (old != null && old.name == "[EventKit Preview Camera]") DestroyImmediate(old.gameObject);
                // 保存されない隠しカメラ（シーンのデータには影響しない）
                var go = EditorUtility.CreateGameObjectWithHideFlags("[EventKit Preview Camera]", HideFlags.HideAndDontSave, typeof(Camera));
                previewCamera = go.GetComponent<Camera>();
#if EVENTKIT_URP
                go.AddComponent<UniversalAdditionalCameraData>();
#endif
                var lightGo = EditorUtility.CreateGameObjectWithHideFlags("[EventKit Preview Light]", HideFlags.HideAndDontSave, typeof(Light));
                lightGo.transform.SetParent(go.transform, false);
                previewLight = lightGo.GetComponent<Light>();
                previewLight.type = LightType.Spot;
                previewLight.spotAngle = 80f;
                previewLight.range = 25f;
                previewLight.intensity = 1.5f;
                previewLight.color = new Color(1f, 0.95f, 0.85f);
                previewLight.enabled = false;
            }
            var cam = previewCamera;
            var src = Camera.main;
            if (src != null)
            {
                cam.CopyFrom(src);
#if EVENTKIT_URP
                // URP のときはポストプロセス等の設定も合わせる
                var srcData = src.GetUniversalAdditionalCameraData();
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = srcData.renderPostProcessing;
                data.volumeLayerMask = srcData.volumeLayerMask;
                data.antialiasing = srcData.antialiasing;
#endif
            }
            cam.enabled = false;
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
            cam.aspect = (float)width / height;
            cam.targetTexture = previewTexture;

            // 懐中電灯の代わり：プレビューを描く間だけ点く隠しライト
            previewLight.enabled = flashlight;
            cam.Render();
            previewLight.enabled = false;
            cam.targetTexture = null;
            return previewTexture;
        }
    }
}
