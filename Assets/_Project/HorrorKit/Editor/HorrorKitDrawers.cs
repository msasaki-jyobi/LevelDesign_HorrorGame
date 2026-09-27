using UnityEditor;
using UnityEngine;

namespace HorrorKit.EditorTools
{
    [CustomPropertyDrawer(typeof(ItemIdAttribute))]
    public class ItemIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            property.stringValue = HorrorKitEditorUtil.IdPopup(position, label, property.stringValue, true);
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(FlagIdAttribute))]
    public class FlagIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            property.stringValue = HorrorKitEditorUtil.IdPopup(position, label, property.stringValue, false);
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(EventCondition))]
    public class EventConditionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight + 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var typeProp = property.FindPropertyRelative("type");
            var idProp = property.FindPropertyRelative("id");
            position.height = EditorGUIUtility.singleLineHeight;
            var typeRect = new Rect(position.x, position.y, Mathf.Min(170f, position.width * 0.4f), position.height);
            var idRect = new Rect(typeRect.xMax + 4f, position.y, position.width - typeRect.width - 4f, position.height);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);
            var type = (ConditionType)typeProp.intValue;
            bool isItem = type == ConditionType.HasItem || type == ConditionType.NotHasItem;
            idProp.stringValue = HorrorKitEditorUtil.IdPopup(idRect, GUIContent.none, idProp.stringValue, isItem);
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(EventCommand))]
    public class EventCommandDrawer : PropertyDrawer
    {
        static float Line => EditorGUIUtility.singleLineHeight;
        static float Space => EditorGUIUtility.standardVerticalSpacing;

        static GUIStyle wrapArea;
        static GUIStyle WrapArea => wrapArea ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };

        static float TextHeight(SerializedProperty property)
        {
            string text = property.FindPropertyRelative("text").stringValue ?? "";
            float width = Mathf.Max(200f, EditorGUIUtility.currentViewWidth - 140f);
            return Mathf.Max(Line * 3f, WrapArea.CalcHeight(new GUIContent(text), width) + 4f);
        }

        static int ExtraLines(CommandType t)
        {
            switch (t)
            {
                case CommandType.Message: return 1;
                case CommandType.GiveItem:
                case CommandType.RemoveItem:
                case CommandType.SetFlag:
                case CommandType.PlaySound:
                case CommandType.SetActive: return 2;
                case CommandType.EndEvent: return 0;
                default: return 1;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var type = (CommandType)property.FindPropertyRelative("type").intValue;
            float h = (1 + ExtraLines(type)) * (Line + Space) + 4f;
            if (type == CommandType.Message) h += TextHeight(property) + Space;
            return h;
        }

        static Color CategoryColor(CommandType t)
        {
            switch (t)
            {
                case CommandType.Message: return new Color(0.35f, 0.7f, 1f);
                case CommandType.GiveItem:
                case CommandType.RemoveItem: return new Color(1f, 0.8f, 0.3f);
                case CommandType.SetFlag: return new Color(0.5f, 1f, 0.5f);
                case CommandType.OpenDoor:
                case CommandType.CloseDoor:
                case CommandType.LockDoor:
                case CommandType.UnlockDoor: return new Color(0.8f, 0.6f, 0.4f);
                case CommandType.JumpScare:
                case CommandType.PlaySound:
                case CommandType.FlickerFlashlight:
                case CommandType.FadeIn:
                case CommandType.FadeOut: return new Color(1f, 0.35f, 0.35f);
                default: return new Color(0.7f, 0.7f, 0.7f);
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 90f;

            var typeProp = property.FindPropertyRelative("type");
            var type = (CommandType)typeProp.intValue;
            EditorGUI.DrawRect(new Rect(position.x - 4f, position.y + 2f, 3f, position.height - 6f), CategoryColor(type));

            var r = new Rect(position.x, position.y + 2f, position.width, Line);
            EditorGUI.PropertyField(r, typeProp, new GUIContent("コマンド"));
            r.y += Line + Space;

            switch (type)
            {
                case CommandType.Message:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("speaker"), new GUIContent("話者（任意）"));
                    r.y += Line + Space;
                    var textProp = property.FindPropertyRelative("text");
                    float th = TextHeight(property);
                    var labelRect = new Rect(r.x, r.y, EditorGUIUtility.labelWidth, Line);
                    EditorGUI.LabelField(labelRect, "本文");
                    var areaRect = new Rect(r.x + EditorGUIUtility.labelWidth + 2f, r.y, r.width - EditorGUIUtility.labelWidth - 2f, th);
                    EditorGUI.BeginChangeCheck();
                    string text = EditorGUI.TextArea(areaRect, textProp.stringValue, WrapArea);
                    if (EditorGUI.EndChangeCheck()) textProp.stringValue = text;
                    break;

                case CommandType.GiveItem:
                case CommandType.RemoveItem:
                    var itemProp = property.FindPropertyRelative("itemId");
                    itemProp.stringValue = HorrorKitEditorUtil.IdPopup(r, new GUIContent("アイテム"), itemProp.stringValue, true);
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("silent"), new GUIContent("通知を出さない"));
                    break;

                case CommandType.SetFlag:
                    var flagProp = property.FindPropertyRelative("flagId");
                    flagProp.stringValue = HorrorKitEditorUtil.IdPopup(r, new GUIContent("フラグ"), flagProp.stringValue, false);
                    r.y += Line + Space;
                    var boolProp = property.FindPropertyRelative("boolValue");
                    boolProp.boolValue = EditorGUI.Popup(r, "値", boolProp.boolValue ? 0 : 1, new[] { "ON", "OFF" }) == 0;
                    break;

                case CommandType.OpenDoor:
                case CommandType.CloseDoor:
                case CommandType.LockDoor:
                case CommandType.UnlockDoor:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("door"), new GUIContent("ドア"));
                    break;

                case CommandType.JumpScare:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("jumpScare"), new GUIContent("ジャンプスケア"));
                    break;

                case CommandType.PlaySound:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("clip"), new GUIContent("効果音"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("boolValue"), new GUIContent("終わるまで待つ"));
                    break;

                case CommandType.FlickerFlashlight:
                case CommandType.FadeIn:
                case CommandType.FadeOut:
                case CommandType.Wait:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("duration"), new GUIContent("秒数"));
                    break;

                case CommandType.SetActive:
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("target"), new GUIContent("対象"));
                    r.y += Line + Space;
                    EditorGUI.PropertyField(r, property.FindPropertyRelative("boolValue"), new GUIContent("表示する"));
                    break;
            }

            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
