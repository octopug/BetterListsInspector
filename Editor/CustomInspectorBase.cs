using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for all types, extend it when making custom inspectors.
/// Draws reorderable lists for all arrays/lists by default.
/// 
/// ref: https://gist.github.com/t0chas/34afd1e4c9bc28649311
/// </summary>

namespace BetterLists
{
    [CustomEditor(typeof(Object), true, isFallback = true)]
    [CanEditMultipleObjects]
    public class CustomInspectorBase : Editor
    {
        private const BindingFlags BindFlags =
            BindingFlags.GetField |
            BindingFlags.GetProperty |
            BindingFlags.IgnoreCase |
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public;

        private Dictionary<string, BetterListDrawer> _listDrawers;
        private bool _requiresRepaint;

        protected virtual void OnEnable()
        {
            _listDrawers = new Dictionary<string, BetterListDrawer>(10);
        }

        // TODO(Matt): Fix issues with width causing horizontal scrolling
        public override void OnInspectorGUI()
        {
            _requiresRepaint = false;

            Color cachedGuiColor = GUI.color;
            serializedObject.Update();

            SerializedProperty property = serializedObject.GetIterator();
            bool next = property.NextVisible(true);
            if (next)
            {
                do
                {
                    GUI.color = cachedGuiColor;
                    HandleProperty(property);
                }
                while (property.NextVisible(false));
            }

            serializedObject.ApplyModifiedProperties();
        }

        // TODO(Matt): Check for the Header attribute and draw one if set
        protected void HandleProperty(SerializedProperty property)
        {
            //Debug.LogFormat("name: {0}, displayName: {1}, type: {2}, propertyType: {3}, path: {4}", property.name, property.displayName, property.type, property.propertyType, property.propertyPath);

            // Skip past the default "Script" field
            bool isDefaultScriptProperty = property.name.Equals("m_Script") && property.type.Equals("PPtr<MonoScript>") && property.propertyType == SerializedPropertyType.ObjectReference && property.propertyPath.Equals("m_Script");
            if (isDefaultScriptProperty)
            {
                bool wasEnabled = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.PropertyField(property, property.isExpanded);
                GUI.enabled = wasEnabled;
            }
            else
            {
                // TODO(Matt): This doesn't check for arrays within the property itself (it could be a custom serialized class)
                if (property.isArray && property.propertyType != SerializedPropertyType.String)
                {
                    DrawArray(property);
                }
                else
                {
                    EditorGUILayout.PropertyField(property, property.isExpanded);
                }
            }
        }

        protected void DrawArray(SerializedProperty property)
        {
            var listDrawer = GetOrCreateListDrawer(property);
            listDrawer.DrawFoldoutControl();

            // Draw list if expanded or animating
            if (listDrawer.IsExpanded.value || listDrawer.IsExpanded.isAnimating)
            {
                if (EditorGUILayout.BeginFadeGroup(listDrawer.IsExpanded.faded))
                {
                    listDrawer.OnDrawBegin();
                    listDrawer.List.DoLayoutList();
                }
                EditorGUILayout.EndFadeGroup();
            }

            // We have to delay changes to toggling expanding to avoid conflicts in number
            // of present gui elements between the Layout and Repaint events
            if (Event.current.type == EventType.Repaint && listDrawer.ToggleExpandNextRepaint)
            {
                property.isExpanded = !property.isExpanded;
                listDrawer.ToggleExpandNextRepaint = false;
            }

            // Update our anim which tracks the expanded value
            listDrawer.IsExpanded.target = property.isExpanded;

            _requiresRepaint |= listDrawer.IsExpanded.isAnimating || listDrawer.ToggleExpandNextRepaint;
        }

        private BetterListDrawer GetOrCreateListDrawer(SerializedProperty property)
        {
            BetterListDrawer listDrawer = null;
            if (_listDrawers.TryGetValue(property.name, out listDrawer))
            {
                listDrawer.Property = property;
                return listDrawer;
            }

            listDrawer = new BetterListDrawer(property);
            _listDrawers.Add(property.name, listDrawer);
            return listDrawer;
        }

        public override bool RequiresConstantRepaint()
        {
            return _requiresRepaint || base.RequiresConstantRepaint();
        }
    }
}
