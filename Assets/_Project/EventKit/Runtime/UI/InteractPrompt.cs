using UnityEngine;
using UnityEngine.UI;

namespace EventKit
{
    /// <summary>
    /// 照準と「[E] 調べる」の表示（プレハブのルートに付ける）。どちらも任意（照準が要らなければ空欄でよい）。
    /// </summary>
    public class InteractPrompt : MonoBehaviour
    {
        [Tooltip("画面中央の照準（任意）")]
        public Graphic crosshair;
        public Vector2 crosshairSize = new Vector2(6, 6);
        [Tooltip("調べられる物を見ているときの照準の大きさ")]
        public Vector2 crosshairFocusedSize = new Vector2(12, 12);

        [Tooltip("プロンプト全体（表示/非表示を切り替える）")]
        public GameObject promptRoot;
        public Text promptText;
        [Tooltip("{0} にキー、{1} に行動名（調べる・開ける など）")]
        public string format = "<color=#d9c38c>[{0}]</color> {1}";
        public string keyLabel = "E";

        /// <summary>行動名を出す（null か空なら消す）。</summary>
        public virtual void Show(string action)
        {
            bool show = !string.IsNullOrEmpty(action);
            if (promptRoot != null) promptRoot.SetActive(show);
            if (show && promptText != null) promptText.text = string.Format(format, keyLabel, action);
            if (crosshair != null) crosshair.rectTransform.sizeDelta = show ? crosshairFocusedSize : crosshairSize;
        }

        /// <summary>会話中・所持品表示中は照準ごと隠す。</summary>
        public virtual void SetVisible(bool visible)
        {
            if (crosshair != null) crosshair.enabled = visible;
            if (!visible && promptRoot != null) promptRoot.SetActive(false);
        }
    }
}
