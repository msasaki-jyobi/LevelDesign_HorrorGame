using UnityEngine;

namespace EventKit
{
    /// <summary>
    /// インスペクター・イベントエディタでの項目名を日本語などに変える（拡張コマンドのフィールドに便利）。
    /// 例：[EventLabel("秒数")] public float seconds = 1f;
    /// </summary>
    public class EventLabelAttribute : PropertyAttribute
    {
        public string Text { get; }
        public EventLabelAttribute(string text) => Text = text;
    }
}
