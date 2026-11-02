using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace EventKit
{
    /// <summary>
    /// 所持品ウィンドウ（プレハブのルートに付ける）。所持しているキーアイテムの一覧と、選んだ物の説明・アイコンを出す。
    /// </summary>
    public class InventoryWindow : MonoBehaviour
    {
        public Text listText;
        public Text descriptionText;
        [Tooltip("選んだアイテムのアイコン（任意。アイテム定義の icon を表示）")]
        public Image iconImage;
        public string emptyText = "<color=#777777>何も持っていない</color>";
        public string selectedFormat = "<color=#e8c872>▶ {0}</color>";
        public string normalFormat = "　 {0}";

        public bool IsOpen => gameObject.activeSelf;

        public virtual void Open() => gameObject.SetActive(true);
        public virtual void Close() => gameObject.SetActive(false);

        /// <summary>一覧を描き直す。index は選んでいる項目（範囲内に補正済み）。</summary>
        public virtual void Refresh(IReadOnlyList<string> itemIds, int index)
        {
            if (itemIds.Count == 0)
            {
                if (listText != null) listText.text = emptyText;
                if (descriptionText != null) descriptionText.text = "";
                if (iconImage != null) iconImage.enabled = false;
                return;
            }
            var sb = new StringBuilder();
            for (int i = 0; i < itemIds.Count; i++)
                sb.AppendLine(string.Format(i == index ? selectedFormat : normalFormat, EventDatabase.ItemName(itemIds[i])));
            if (listText != null) listText.text = sb.ToString();

            var def = EventDatabase.Instance != null ? EventDatabase.Instance.GetItem(itemIds[index]) : null;
            if (descriptionText != null) descriptionText.text = def != null ? def.description : "";
            if (iconImage != null)
            {
                iconImage.sprite = def != null ? def.icon : null;
                iconImage.enabled = iconImage.sprite != null;
            }
        }
    }
}
