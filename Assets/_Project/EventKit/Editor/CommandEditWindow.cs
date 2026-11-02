using UnityEditor;
using UnityEngine;

namespace EventKit.EditorTools
{
    /// <summary>
    /// コマンドを別ウィンドウで編集する（イベントエディタで行をダブルクリック）。
    /// 中身はイベントエディタ下の「選択中のコマンド」と同じ。イベントエディタで別の行を選ぶと、こちらも切り替わる。
    /// </summary>
    public class CommandEditWindow : EditorWindow
    {
        [SerializeField] GameEvent ev;
        [SerializeField] int page, index;
        Vector2 scroll;

        public static void Open(GameEvent ev, int page, int index)
        {
            var w = GetWindow<CommandEditWindow>(true, "コマンドの編集", true);
            w.minSize = new Vector2(380, 260);
            w.SetTarget(ev, page, index);
            w.Show();
        }

        /// <summary>ウィンドウが開いていれば、対象のコマンドを切り替える。</summary>
        public static void Follow(GameEvent ev, int page, int index)
        {
            if (!HasOpenInstances<CommandEditWindow>()) return;
            var w = GetWindow<CommandEditWindow>(true, "コマンドの編集", false);
            if (w.ev != ev || w.page != page || w.index != index) w.SetTarget(ev, page, index);
        }

        void SetTarget(GameEvent e, int p, int i)
        {
            ev = e;
            page = p;
            index = i;
            Repaint();
        }

        void OnInspectorUpdate() => Repaint();

        void OnGUI()
        {
            EventCommand c = null;
            if (ev != null && page >= 0 && page < ev.pages.Count && index >= 0 && index < ev.pages[page].commands.Count)
                c = ev.pages[page].commands[index];
            if (c == null || CommandTypes.IsCloser(c.type) || CommandTypes.IsDivider(c.type))
            {
                EditorGUILayout.HelpBox("イベントエディタでコマンドの行を選ぶと、ここで編集できます。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"{ev.DisplayName}　ページ{page + 1}・{index + 1}行目", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(EventCommandDrawer.TypeLabel(c.type), EditorStyles.boldLabel);

            var so = new SerializedObject(ev);
            so.Update();
            var prop = so.FindProperty($"pages.Array.data[{page}].commands.Array.data[{index}]");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(prop, GUIContent.none, true);
            if (EditorGUI.EndChangeCheck())
            {
                so.ApplyModifiedProperties();
                foreach (var w in Resources.FindObjectsOfTypeAll<EventEditorWindow>()) w.Repaint();
            }
            if (c.type == CommandType.If || c.type == CommandType.ShowChoices)
                EditorGUILayout.LabelField("「それ以外のとき」などの分岐の追加は、イベントエディタ下の欄で行います。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }
    }
}
