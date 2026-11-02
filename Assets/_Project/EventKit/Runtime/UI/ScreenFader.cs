using System.Collections;
using UnityEngine;

namespace EventKit
{
    /// <summary>
    /// 画面のフェード（プレハブのルートに付ける）。CanvasGroup の透明度で暗転する。
    /// 黒以外（白フェード・テクスチャ付き等）にしたいときはプレハブの中の画像を変える。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        CanvasGroup group;

        CanvasGroup Group
        {
            get
            {
                if (group == null)
                {
                    group = GetComponent<CanvasGroup>();
                    group.blocksRaycasts = false;
                    group.interactable = false;
                }
                return group;
            }
        }

        /// <summary>0＝見える、1＝真っ暗</summary>
        public float Alpha
        {
            get => Group.alpha;
            set => Group.alpha = value;
        }

        /// <summary>seconds 秒かけて target まで変える（時間停止中も進む）。</summary>
        public IEnumerator FadeTo(float target, float seconds)
        {
            float from = Alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                Alpha = Mathf.Lerp(from, target, t / seconds);
                yield return null;
            }
            Alpha = target;
        }
    }
}
