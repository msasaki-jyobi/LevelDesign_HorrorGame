using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace EventKit
{
    /// <summary>
    /// 会話ウィンドウ（プレハブのルートに付ける）。話者名・本文・送りマークを表示し、1文字ずつ出す。
    /// 見た目はプレハブで自由に変えてよい（参照さえ合っていれば配置・画像・色は自由）。
    /// 動きを変えたいときは継承して Show を上書きする。
    /// </summary>
    public class DialogueWindow : MonoBehaviour
    {
        [Tooltip("話者名の枠（話者が空のときは隠す）")]
        public GameObject speakerRoot;
        public Text speakerText;
        public Text bodyText;
        [Tooltip("全文が出たときに点滅する送りマーク（▼ など）")]
        public Graphic nextIndicator;

        [Header("表示")]
        public float charactersPerSecond = 32f;
        [Tooltip("1文字ごとに鳴らす音（任意）")]
        public AudioClip typeSound;
        [Range(0f, 1f)] public float typeVolume = 0.5f;

        AudioSource audioSource;
        float indicatorAlpha = 1f;

        public bool IsOpen => gameObject.activeSelf;

        protected virtual void Awake()
        {
            if (nextIndicator != null) indicatorAlpha = nextIndicator.color.a;
        }

        protected virtual void Update()
        {
            if (nextIndicator != null && nextIndicator.enabled)
            {
                var c = nextIndicator.color;
                c.a = indicatorAlpha * (0.5f + 0.5f * Mathf.PingPong(Time.unscaledTime * 2f, 1f));
                nextIndicator.color = c;
            }
        }

        public virtual void Close() => gameObject.SetActive(false);

        /// <summary>メッセージを1文字ずつ表示し、決定されるまで待つ。submitPressed は「このフレームで決定が押されたか」。</summary>
        public virtual IEnumerator Show(string speaker, string text, Func<bool> submitPressed)
        {
            text = text ?? "";
            gameObject.SetActive(true);
            if (speakerRoot != null) speakerRoot.SetActive(!string.IsNullOrEmpty(speaker));
            if (speakerText != null) speakerText.text = speaker;
            SetBody("");
            if (nextIndicator != null) nextIndicator.enabled = false;

            yield return null; // 開始フレームの決定入力を無視する

            float shown = 0f;
            int lastCount = 0;
            while (lastCount < text.Length)
            {
                if (submitPressed()) shown = text.Length;
                else shown += charactersPerSecond * Time.deltaTime;

                int count = Mathf.Min(text.Length, Mathf.FloorToInt(shown));
                count = SkipRichTextTags(text, count);
                if (count != lastCount)
                {
                    SetBody(CloseOpenTags(text.Substring(0, count)));
                    if (typeSound != null && count > lastCount && !char.IsWhiteSpace(text[count - 1])) PlayType();
                    lastCount = count;
                }
                yield return null;
            }

            if (nextIndicator != null) nextIndicator.enabled = true;
            yield return null;
            while (!submitPressed()) yield return null;
            if (nextIndicator != null) nextIndicator.enabled = false;
        }

        protected virtual void SetBody(string s)
        {
            if (bodyText != null) bodyText.text = s;
        }

        void PlayType()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            audioSource.PlayOneShot(typeSound, typeVolume);
        }

        /// <summary>途中表示でリッチテキストのタグが切れないよう、タグの途中なら閉じ '>' まで進める。</summary>
        public static int SkipRichTextTags(string text, int count)
        {
            int open = text.LastIndexOf('<', Mathf.Max(0, count - 1));
            if (open >= 0 && open < count)
            {
                int close = text.IndexOf('>', open);
                if (close >= count) return Mathf.Min(text.Length, close + 1);
            }
            return count;
        }

        /// <summary>途中まで表示した文字列で開いたままのタグ（&lt;size&gt; 等）を閉じる。</summary>
        public static string CloseOpenTags(string partial)
        {
            var stack = new System.Collections.Generic.List<string>();
            int i = 0;
            while ((i = partial.IndexOf('<', i)) >= 0)
            {
                int end = partial.IndexOf('>', i);
                if (end < 0) break;
                string tag = partial.Substring(i + 1, end - i - 1);
                if (tag.StartsWith("/"))
                {
                    if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                }
                else
                {
                    int sep = tag.IndexOfAny(new[] { '=', ' ' });
                    stack.Add(sep >= 0 ? tag.Substring(0, sep) : tag);
                }
                i = end + 1;
            }
            for (int k = stack.Count - 1; k >= 0; k--) partial += "</" + stack[k] + ">";
            return partial;
        }
    }
}
