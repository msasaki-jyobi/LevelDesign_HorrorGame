using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EventKit.EditorTools
{
    /// <summary>
    /// ページのタブ列。クリックで選択、左右にドラッグで並び替え。
    /// 出現条件のあるページは「ページ 1 条」と表示し、マウスを乗せると条件が出る。
    /// インスペクターとイベントエディタの両方で使う。
    /// </summary>
    public static class PageTabBar
    {
        class DragState
        {
            public int from = -1;
            public float startX;
            public bool dragging;
            public int slot = -1;
        }

        const float DragThreshold = 5f;
        static GUIStyle left, mid, right, single;

        static GUIStyle Style(int i, int count)
        {
            if (left == null)
            {
                GUIStyle Make(GUIStyle s) => new GUIStyle(s) { richText = true, fixedHeight = 0, fontSize = 11 };
                left = Make(EditorStyles.miniButtonLeft);
                mid = Make(EditorStyles.miniButtonMid);
                right = Make(EditorStyles.miniButtonRight);
                single = Make(EditorStyles.miniButton);
            }
            if (count == 1) return single;
            return i == 0 ? left : i == count - 1 ? right : mid;
        }

        public static string Label(EventPage p, int index, bool active)
        {
            string s = (active ? "▶ " : "") + $"ページ {index + 1}";
            if (p != null && p.conditions.Count > 0) s += " <color=#f0b04a><b>条</b></color>";
            return s;
        }

        /// <summary>ページの有効判定の決まり（ツールチップに出す）</summary>
        public const string RuleText =
            "【有効になるページ】番号の大きいページ（右）から順に条件を調べ、\n最初に条件を満たしたページが1つだけ有効になります。\n" +
            "→ 条件の厳しいページほど右に置きます（条件なしは左端）。";

        static string Tooltip(EventPage p)
        {
            if (p == null) return "";
            string head;
            if (p.conditions.Count == 0) head = "出現条件なし（条件の上では常に満たす）";
            else
            {
                var lines = new List<string>();
                foreach (var c in p.conditions) lines.Add("・" + EventKitEditorUtil.ConditionText(c));
                head = "出現条件：\n" + string.Join("\n", lines);
            }
            return head + "\n\n" + RuleText + "\nドラッグで並び替え";
        }

        /// <summary>タブ列を描いて、選択中のページ番号を返す。並び替えたときは onMove(元, 先) を呼ぶ。</summary>
        public static int Draw(IList<EventPage> pages, int selected, int activeIndex, Action<int, int> onMove, float height = 24f)
        {
            int count = pages.Count;
            var rect = GUILayoutUtility.GetRect(10, height, GUILayout.ExpandWidth(true));
            if (count == 0) return selected;

            int id = GUIUtility.GetControlID(FocusType.Passive);
            var state = (DragState)GUIUtility.GetStateObject(typeof(DragState), id);
            float w = rect.width / count;
            Rect TabRect(int i) => new Rect(rect.x + w * i, rect.y, w, rect.height);
            int SlotAt(float x) => Mathf.Clamp(Mathf.RoundToInt((x - rect.x) / w), 0, count);

            var e = Event.current;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when e.button == 0 && rect.Contains(e.mousePosition):
                    int hit = Mathf.Clamp((int)((e.mousePosition.x - rect.x) / w), 0, count - 1);
                    selected = hit;
                    state.from = hit;
                    state.startX = e.mousePosition.x;
                    state.dragging = false;
                    state.slot = -1;
                    GUIUtility.hotControl = id;
                    GUI.FocusControl(null);
                    GUI.changed = true;
                    e.Use();
                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    if (!state.dragging && Mathf.Abs(e.mousePosition.x - state.startX) > DragThreshold) state.dragging = true;
                    if (state.dragging) state.slot = SlotAt(e.mousePosition.x);
                    e.Use();
                    break;

                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    if (state.dragging && state.from >= 0)
                    {
                        // 挿入位置（タブの間）→ 並び替え後の番号
                        int to = state.slot > state.from ? state.slot - 1 : state.slot;
                        if (to >= 0 && to != state.from)
                        {
                            onMove(state.from, to);
                            selected = to;
                            GUI.changed = true;
                        }
                    }
                    state.from = -1;
                    state.dragging = false;
                    state.slot = -1;
                    e.Use();
                    break;

                case EventType.KeyDown when GUIUtility.hotControl == id && e.keyCode == KeyCode.Escape:
                    GUIUtility.hotControl = 0;
                    state.dragging = false;
                    state.slot = -1;
                    e.Use();
                    break;

                case EventType.Repaint:
                    for (int i = 0; i < count; i++)
                    {
                        var r = TabRect(i);
                        bool on = i == selected;
                        var content = new GUIContent(Label(pages[i], i, i == activeIndex), Tooltip(pages[i]));
                        Style(i, count).Draw(r, content, r.Contains(e.mousePosition), state.dragging && i == state.from, on, false);
                        if (state.dragging && i == state.from) EditorGUI.DrawRect(r, new Color(0.3f, 0.6f, 1f, 0.18f));
                    }
                    if (state.dragging && state.slot >= 0)
                    {
                        float x = rect.x + w * state.slot;
                        EditorGUI.DrawRect(new Rect(x - 1.5f, rect.y - 2, 3, rect.height + 4), new Color(0.35f, 0.7f, 1f));
                    }
                    break;
            }

            // ツールチップ用（Repaint 以外では内容を出さない透明ラベル）
            for (int i = 0; i < count; i++)
                GUI.Label(TabRect(i), new GUIContent("", Tooltip(pages[i])));

            if (GUIUtility.hotControl == id && state.dragging) EditorGUIUtility.AddCursorRect(rect, MouseCursor.Pan);
            else EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            return selected;
        }

        /// <summary>リスト内で要素を from から to へ移す。</summary>
        public static void Move<T>(IList<T> list, int from, int to)
        {
            var item = list[from];
            list.RemoveAt(from);
            list.Insert(to, item);
        }
    }
}
