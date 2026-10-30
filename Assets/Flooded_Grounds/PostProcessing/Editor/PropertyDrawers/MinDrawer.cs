using UnityEngine;

namespace UnityEditor.PostProcessing
{
    [CustomPropertyDrawer(typeof(UnityEngine.PostProcessing.MinAttribute))]
    sealed class MinDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            UnityEngine.PostProcessing.MinAttribute minimum = (UnityEngine.PostProcessing.MinAttribute)base.attribute;
            if (property.propertyType == SerializedPropertyType.Integer)
            {
                int value = EditorGUI.IntField(position, label, property.intValue);
                property.intValue = (int)Mathf.Max(value, minimum.min);
            }
            else if (property.propertyType == SerializedPropertyType.Float)
            {
                float value = EditorGUI.FloatField(position, label, property.floatValue);
                property.floatValue = Mathf.Max(value, minimum.min);
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use Min with float or int.");
            }
        }
    }
}
