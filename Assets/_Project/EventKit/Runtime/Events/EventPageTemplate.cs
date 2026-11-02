using System.Collections.Generic;
using UnityEngine;

namespace EventKit
{
    /// <summary>
    /// ページのテンプレート。イベントエディタの「テンプレート」から保存・追加する。
    /// シーン上のオブジェクトへの参照（ドア・対象など）はアセットに保存できないため、追加後に設定し直す。
    /// </summary>
    [CreateAssetMenu(menuName = "EventKit/ページテンプレート", fileName = "PageTemplate")]
    public class EventPageTemplate : ScriptableObject
    {
        [Tooltip("メニューに出る名前（「/」で区切るとグループになる）。空ならファイル名")]
        public string templateName;
        [TextArea(2, 5)] public string description;
        public List<EventPage> pages = new List<EventPage>();

        public string DisplayName => string.IsNullOrEmpty(templateName) ? name : templateName;
    }
}
