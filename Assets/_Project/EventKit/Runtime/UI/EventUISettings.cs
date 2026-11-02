using UnityEngine;
using UnityEngine.UI;

namespace EventKit
{
    /// <summary>
    /// UIプレハブ管理。会話ウィンドウ・選択肢・調べるプロンプト・所持品・フェード・ロード画面に使うプレハブをまとめて指定する。
    /// Resources/EventUISettings.asset に置くと自動で使われる（メニュー「EventKit > UIプレハブ管理」で編集）。
    /// 見た目を変えたいときは標準のプレハブを複製し、複製したものをここで差し替える。
    /// </summary>
    [CreateAssetMenu(menuName = "EventKit/UI Settings", fileName = "EventUISettings")]
    public class EventUISettings : ScriptableObject
    {
        public const string ResourcePath = "EventUISettings";

        [Header("UIプレハブ（未設定の物は表示されない）")]
        public DialogueWindow dialogue;
        public ChoiceWindow choices;
        public InteractPrompt prompt;
        public InventoryWindow inventory;
        public ScreenFader fade;
        [Tooltip("ロード画面付きシーン遷移で、コマンドにプレハブを指定しなかったときに使う")]
        public LoadingScreen loading;

        [Header("フォント（未設定・標準フォントの文字は、OSの日本語フォントで表示）")]
        public Font font;
        public string[] osFontNames = { "Yu Gothic UI", "Meiryo UI", "Meiryo", "Hiragino Sans", "MS Gothic", "Arial" };

        [Header("画面")]
        [Tooltip("UI の Canvas の表示順（大きいほど手前）")]
        public int sortingOrder = 100;
        public Vector2 referenceResolution = new Vector2(1920, 1080);
        [Range(0f, 1f)] public float matchWidthOrHeight = 0.5f;

        static EventUISettings instance;

        public static EventUISettings Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<EventUISettings>(ResourcePath);
                return instance;
            }
        }

        /// <summary>UI 用の Canvas を作る（Screen Space Overlay ＋ 解像度に合わせて拡縮）。</summary>
        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            var s = Instance;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = s != null ? s.referenceResolution : new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = s != null ? s.matchWidthOrHeight : 0.5f;
            return canvas;
        }
    }

    /// <summary>UI の日本語フォント。プレハブの Text が未設定か Unity 標準フォントなら、設定のフォント（無ければ OS の日本語フォント）にする。</summary>
    public static class EventUIFont
    {
        static Font cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cached = null;

        public static Font Default
        {
            get
            {
                if (cached != null) return cached;
                var s = EventUISettings.Instance;
                if (s != null && s.font != null) return cached = s.font;
                var names = s != null && s.osFontNames != null && s.osFontNames.Length > 0
                    ? s.osFontNames
                    : new[] { "Yu Gothic UI", "Meiryo UI", "Meiryo", "Hiragino Sans", "MS Gothic", "Arial" };
                return cached = Font.CreateDynamicFontFromOSFont(names, 32);
            }
        }

        public static void Apply(GameObject root)
        {
            if (root == null) return;
            foreach (var t in root.GetComponentsInChildren<Text>(true))
                if (t.font == null || t.font.name == "LegacyRuntime" || t.font.name == "Arial") t.font = Default;
        }
    }
}
