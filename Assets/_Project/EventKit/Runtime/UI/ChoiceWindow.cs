using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EventKit
{
    /// <summary>
    /// 選択肢ウィンドウ（プレハブのルートに付ける）。itemTemplate を選択肢の数だけ複製して並べる。
    /// 並べ方は itemContainer の Layout Group で決まる（標準は縦並び）。
    /// </summary>
    public class ChoiceWindow : MonoBehaviour
    {
        [Tooltip("選択肢を並べる親（Vertical Layout Group 等を付ける）")]
        public RectTransform itemContainer;
        [Tooltip("選択肢1つ分の見本（非アクティブにしておく）")]
        public Text itemTemplate;
        [Tooltip("選んでいる項目の書式（{0} に選択肢）")]
        public string selectedFormat = "<color=#e8c872>▶ {0}</color>";
        public string normalFormat = "　 {0}";

        readonly List<Text> items = new List<Text>();
        IList<string> options;

        public bool IsOpen => gameObject.activeSelf;

        public virtual void Open(IList<string> choices)
        {
            options = choices;
            foreach (var t in items) if (t != null) Destroy(t.gameObject);
            items.Clear();
            if (itemTemplate != null)
            {
                var parent = itemContainer != null ? itemContainer : (RectTransform)itemTemplate.transform.parent;
                for (int i = 0; i < choices.Count; i++)
                {
                    var t = Instantiate(itemTemplate, parent);
                    t.gameObject.SetActive(true);
                    t.name = "Choice" + i;
                    items.Add(t);
                }
                itemTemplate.gameObject.SetActive(false);
            }
            gameObject.SetActive(true);
            Select(0);
        }

        public virtual void Select(int index)
        {
            for (int i = 0; i < items.Count; i++)
                items[i].text = string.Format(i == index ? selectedFormat : normalFormat, options[i]);
        }

        public virtual void Close() => gameObject.SetActive(false);
    }
}
