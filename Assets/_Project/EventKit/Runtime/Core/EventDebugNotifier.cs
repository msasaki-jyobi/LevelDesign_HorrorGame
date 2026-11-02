using System.Collections.Generic;
using UnityEngine;

namespace EventKit
{
    /// <summary>
    /// デバッグ用：フラグ・所持品が変わるたびに、ゲーム画面の下に通知を出す。
    /// エディタと Development Build だけで動く。ON/OFF は「EventKit > デバッグ」ウィンドウから。
    /// </summary>
    public class EventDebugNotifier : MonoBehaviour
    {
        /// <summary>通知を出すか（エディタではデバッグウィンドウの設定が入る）</summary>
        public static bool Enabled = true;
        public static float Duration = 3f;
        const int MaxLines = 6;

        struct Toast
        {
            public string text;
            public Color color;
            public float time;
        }

        static EventDebugNotifier instance;
        readonly List<Toast> toasts = new List<Toast>();
        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            if (instance != null) return;
            var go = new GameObject("[EventDebugNotifier]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<EventDebugNotifier>();
            GameState.FlagChanged += (id, on) => Push($"フラグ {id} → {(on ? "ON" : "OFF")}", on ? new Color(0.35f, 0.9f, 0.45f) : new Color(1f, 0.45f, 0.4f));
            GameState.ItemAdded += id => Push($"アイテム入手：{EventDatabase.ItemName(id)}", new Color(1f, 0.85f, 0.35f));
            GameState.ItemRemoved += id => Push($"アイテム消失：{EventDatabase.ItemName(id)}", new Color(0.8f, 0.7f, 0.5f));
        }

        /// <summary>任意のデバッグ通知を出す（スクリプトからも呼べる）</summary>
        public static void Push(string text, Color color)
        {
            if (!Enabled || instance == null) return;
            instance.toasts.Add(new Toast { text = text, color = color, time = Time.unscaledTime });
            if (instance.toasts.Count > MaxLines) instance.toasts.RemoveAt(0);
        }

        void OnGUI()
        {
            if (toasts.Count == 0) return;
            float now = Time.unscaledTime;
            toasts.RemoveAll(t => now - t.time > Duration);
            if (toasts.Count == 0) return;

            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            // 解像度に合わせて文字を大きくする（1080p で 22）
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            style.fontSize = Mathf.RoundToInt(22 * scale);
            style.padding = new RectOffset(Mathf.RoundToInt(16 * scale), Mathf.RoundToInt(16 * scale), Mathf.RoundToInt(6 * scale), Mathf.RoundToInt(6 * scale));
            var font = EventUIFont.Default;
            if (font != null) style.font = font;

            float y = Screen.height - 24f * scale;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                float alpha = Mathf.Clamp01((Duration - (now - t.time)) / 0.5f);
                var content = new GUIContent(t.text);
                var size = style.CalcSize(content);
                y -= size.y + 4f * scale;
                var r = new Rect((Screen.width - size.x) * 0.5f, y, size.x, size.y);
                var old = GUI.color;
                GUI.color = new Color(0.12f, 0.12f, 0.14f, 0.85f * alpha);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = old;
                style.normal.textColor = new Color(t.color.r, t.color.g, t.color.b, alpha);
                GUI.Label(r, content, style);
            }
        }
    }
}
