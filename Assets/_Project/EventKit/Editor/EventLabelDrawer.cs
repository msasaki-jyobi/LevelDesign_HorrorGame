using UnityEditor;
using UnityEngine;

namespace EventKit.EditorTools
{
    [CustomPropertyDrawer(typeof(EventLabelAttribute))]
    public class EventLabelDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUI.GetPropertyHeight(property, label, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (EventLabelAttribute)attribute;
            EditorGUI.PropertyField(position, property, new GUIContent(attr.Text, label.tooltip), true);
        }
    }
}
