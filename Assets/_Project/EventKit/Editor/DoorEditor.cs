using UnityEditor;

namespace EventKit.EditorTools
{
    /// <summary>開き戸／引き戸に合わせて、関係する項目だけを表示する。</summary>
    [CustomEditor(typeof(Door))]
    [CanEditMultipleObjects]
    public class DoorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var motion = serializedObject.FindProperty("motion");
            bool sliding = !motion.hasMultipleDifferentValues && (DoorMotion)motion.intValue == DoorMotion.Sliding;
            bool hinged = !motion.hasMultipleDifferentValues && (DoorMotion)motion.intValue == DoorMotion.Hinged;

            var exclude = new System.Collections.Generic.List<string> { "m_Script" };
            if (sliding) exclude.Add("openAngle");
            if (hinged) exclude.Add("slideOffset");
            DrawPropertiesExcluding(serializedObject, exclude.ToArray());

            if (sliding)
                EditorGUILayout.HelpBox("引き戸：「Hinge」に指定した物（扉の板の親）が Slide Offset だけ動きます。扉の幅と同じだけ横（X）にずらすのが基本です。", MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
