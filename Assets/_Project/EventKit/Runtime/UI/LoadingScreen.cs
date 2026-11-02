using UnityEngine;
using UnityEngine.UI;

namespace EventKit
{
    /// <summary>
    /// ローディング用UIプレハブに付けると、読み込みの進み具合を表示する（どの欄も任意）。
    /// 付けなくても、プレハブはロード中そのまま表示される。
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        [Tooltip("進み具合のバー（Slider・任意）")]
        public Slider progressBar;
        [Tooltip("進み具合のバー（Image Type = Filled・任意）")]
        public Image progressFill;
        [Tooltip("進み具合の文字（任意）。{0} に 0〜100 が入る")]
        public Text progressText;
        public string format = "{0}%";
        [Tooltip("点滅させる文字（任意。例：Now Loading...）")]
        public Graphic blink;
        public float blinkSpeed = 2.5f;

        void Awake() => EventUIFont.Apply(gameObject);


        void Update()
        {
            if (blink == null) return;
            var c = blink.color;
            c.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * blinkSpeed));
            blink.color = c;
        }

        public void SetProgress(float progress01)
        {
            if (progressBar != null) progressBar.value = progress01;
            if (progressFill != null) progressFill.fillAmount = progress01;
            if (progressText != null) progressText.text = string.Format(format, Mathf.RoundToInt(progress01 * 100f));
        }
    }
}
