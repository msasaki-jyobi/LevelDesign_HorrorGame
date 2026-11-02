using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EventKit.EditorTools
{
    /// <summary>
    /// 「モーション再生」コマンドの設定欄。Animator Controller のステート・トリガーをプルダウンで選び、
    /// シーン上でポーズを確認できる（プレビューはシーンのデータを変えない）。
    /// </summary>
    static class AnimationCommandGUI
    {
        static GameObject previewTarget;
        static AnimationClip previewClip;
        static float previewTime;

        public static void Draw(Rect r, SerializedProperty property, float line, float space)
        {
            var targetProp = property.FindPropertyRelative("target");
            var modeProp = property.FindPropertyRelative("animMode");
            var nameProp = property.FindPropertyRelative("animName");
            var clipProp = property.FindPropertyRelative("animClip");

            EditorGUI.PropertyField(r, targetProp, new GUIContent("対象", "モーションさせるキャラクター（Animator を持つオブジェクトか、その親）"));
            r.y += line + space;
            EditorGUI.PropertyField(r, modeProp, new GUIContent("指定方法"));
            r.y += line + space;

            var target = targetProp.objectReferenceValue as GameObject;
            var animator = target != null ? target.GetComponentInChildren<Animator>() : null;
            var mode = (AnimationPlayMode)modeProp.enumValueIndex;
            AnimationClip clipForPreview = null;

            if (mode == AnimationPlayMode.Clip)
            {
                EditorGUI.PropertyField(r, clipProp, new GUIContent("クリップ"));
                clipForPreview = clipProp.objectReferenceValue as AnimationClip;
            }
            else
            {
                var controller = Controller(animator);
                var names = new List<string>();
                if (controller != null)
                {
                    if (mode == AnimationPlayMode.State)
                    {
                        foreach (var layer in controller.layers)
                            foreach (var s in States(layer.stateMachine))
                                if (!names.Contains(s.name)) names.Add(s.name);
                    }
                    else
                    {
                        foreach (var p in controller.parameters)
                            if (p.type == AnimatorControllerParameterType.Trigger) names.Add(p.name);
                    }
                }
                string label = mode == AnimationPlayMode.State ? "ステート" : "トリガー";
                if (names.Count > 0)
                {
                    int current = names.IndexOf(nameProp.stringValue);
                    var options = new List<string>(names);
                    if (current < 0)
                    {
                        options.Insert(0, string.IsNullOrEmpty(nameProp.stringValue) ? "（選ぶ）" : nameProp.stringValue + "（見つかりません）");
                        current = 0;
                    }
                    else if (options.Count != names.Count) current++;
                    int chosen = EditorGUI.Popup(r, label, current, options.ToArray());
                    if (chosen != current)
                    {
                        int idx = names.IndexOf(options[chosen]);
                        if (idx >= 0) nameProp.stringValue = names[idx];
                    }
                }
                else
                {
                    EditorGUI.PropertyField(r, nameProp, new GUIContent(label, animator == null ? "対象に Animator がありません" : "Animator Controller が見つからないので名前を直接入力"));
                }
                if (mode == AnimationPlayMode.State) clipForPreview = StateClip(animator, controller, nameProp.stringValue);
            }
            r.y += line + space;

            using (new EditorGUI.DisabledScope(mode == AnimationPlayMode.Clip))
                EditorGUI.PropertyField(r, property.FindPropertyRelative("duration"), new GUIContent("切り替え秒数", "今のモーションからなめらかに切り替える時間"));
            r.y += line + space;
            EditorGUI.PropertyField(r, property.FindPropertyRelative("waitMode"), new GUIContent("次に進める条件", "そのまま次へ：再生と同時に次へ／終了するまで待機：モーションが終わってから次へ"));
            r.y += line + space;

            DrawPreview(r, target, clipForPreview);
        }

        // ───────── プレビュー（AnimationMode で一時的にポーズを付ける。止めると元に戻る） ─────────

        static void DrawPreview(Rect r, GameObject target, AnimationClip clip)
        {
            bool canPreview = target != null && clip != null && !Application.isPlaying;
            bool previewing = AnimationMode.InAnimationMode() && previewTarget == target && previewClip == clip && canPreview;

            var buttonRect = new Rect(r.x, r.y, 110f, r.height);
            using (new EditorGUI.DisabledScope(!canPreview))
            {
                if (GUI.Button(buttonRect, previewing ? "■ 停止" : "▶ プレビュー"))
                {
                    if (previewing) StopPreview();
                    else
                    {
                        StopPreview();
                        previewTarget = target;
                        previewClip = clip;
                        previewTime = 0f;
                        AnimationMode.StartAnimationMode();
                        Sample();
                    }
                }
            }
            var sliderRect = new Rect(r.x + 114f, r.y, r.width - 114f, r.height);
            if (previewing)
            {
                EditorGUI.BeginChangeCheck();
                previewTime = EditorGUI.Slider(sliderRect, previewTime, 0f, clip.length);
                if (EditorGUI.EndChangeCheck()) Sample();
            }
            else
            {
                string hint = target == null ? "対象を指定するとプレビューできます"
                    : clip == null ? "このモーションはプレビューできません（トリガー・ブレンドツリー等）"
                    : "シーン上でポーズを確認（データは変わりません）";
                EditorGUI.LabelField(sliderRect, hint, EditorStyles.miniLabel);
            }
        }

        static void Sample()
        {
            if (previewTarget == null || previewClip == null) return;
            var animator = previewTarget.GetComponentInChildren<Animator>();
            var root = animator != null ? animator.gameObject : previewTarget;
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(root, previewClip, previewTime);
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }

        [InitializeOnLoadMethod]
        static void Init()
        {
            // 再生・スクリプト再読み込み・選択の変更でプレビューを止める（ポーズを元に戻す）
            EditorApplication.playModeStateChanged += _ => StopPreview();
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            Selection.selectionChanged += () =>
            {
                if (previewTarget != null && !(Selection.activeObject is GameObject go && go.GetComponent<GameEvent>() != null)) StopPreview();
            };
        }

        static void StopPreview()
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            previewTarget = null;
            previewClip = null;
        }

        // ───────── Animator Controller ─────────

        static AnimatorController Controller(Animator animator)
        {
            if (animator == null) return null;
            var rc = animator.runtimeAnimatorController;
            if (rc is AnimatorOverrideController oc) rc = oc.runtimeAnimatorController;
            return rc as AnimatorController;
        }

        static IEnumerable<AnimatorState> States(AnimatorStateMachine sm)
        {
            foreach (var s in sm.states) yield return s.state;
            foreach (var sub in sm.stateMachines)
                foreach (var s in States(sub.stateMachine)) yield return s;
        }

        static AnimationClip StateClip(Animator animator, AnimatorController controller, string stateName)
        {
            if (controller == null || string.IsNullOrEmpty(stateName)) return null;
            foreach (var layer in controller.layers)
                foreach (var s in States(layer.stateMachine))
                {
                    if (s.name != stateName || !(s.motion is AnimationClip clip)) continue;
                    // Override Controller なら差し替え後のクリップ
                    if (animator.runtimeAnimatorController is AnimatorOverrideController oc && oc[clip] != null) return oc[clip];
                    return clip;
                }
            return null;
        }
    }
}
