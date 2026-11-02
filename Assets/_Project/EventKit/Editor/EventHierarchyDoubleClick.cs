using UnityEditor;
using UnityEngine;

namespace EventKit.EditorTools
{
    /// <summary>Hierarchy で GameEvent を持つオブジェクトをダブルクリックするとイベントエディタで開く。</summary>
    [InitializeOnLoad]
    static class EventHierarchyDoubleClick
    {
        static EventHierarchyDoubleClick()
        {
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnItemGUI;
        }

        static void OnItemGUI(EntityId id, Rect rect)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || e.clickCount != 2 || !rect.Contains(e.mousePosition)) return;

            // 1回目のクリックで選択済みなので、選択中のオブジェクトを対象にする
            var go = Selection.activeGameObject;
            if (go == null || go.GetEntityId() != id || !go.TryGetComponent<GameEvent>(out var ev)) return;

            // シーンビューのフォーカス（既定の動作）はそのまま残す
            EditorApplication.delayCall += () => { if (ev != null) EventEditorWindow.Open(ev); };
        }
    }
}
