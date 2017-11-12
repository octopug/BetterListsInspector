using UnityEditor;
using UnityEngine;

namespace BetterLists
{
    public static class GuiTheme
    {
        public static class Buttons
        {
            public const float AddOrDeleteWidth = 18f;

            private static GUIContent _addIcon;
            public static GUIContent AddIcon
            {
                get
                {
                    if (_addIcon == null)
                    {
                        _addIcon = LoadIcon("better_lists.plus_icon");
                    }
                    return _addIcon;
                }
            }

            private static GUIContent _deleteIcon;
            public static GUIContent DeleteIcon
            {
                get
                {
                    if (_deleteIcon == null)
                    {
                        _deleteIcon = LoadIcon("better_lists.minus_icon");
                    }
                    return _deleteIcon;
                }
            }

            private static GUIContent LoadIcon(string resourcesPath)
            {
                var icon = Resources.Load(resourcesPath) as Texture;
                return new GUIContent(icon);
            }

            private static GUIStyle _arrayFoldoutStyle;
            public static GUIStyle ArrayFoldoutStyle
            {
                get
                {
                    if (_arrayFoldoutStyle == null)
                    {
                        _arrayFoldoutStyle = new GUIStyle(EditorStyles.foldout)
                        {
                            richText = true
                        };
                    }
                    return _arrayFoldoutStyle;
                }
            }
        }

        public static class Header
        {
            public const float Height = 16f;
            public const float SizeLabelOffset = 1f;
            public const float Padding = 4f;

            public static string BuildStringFormat(SerializedProperty property)
            {
                const string format = "{0}   <color=grey><size=10>{1}[{2}]</size></color>";
                if (property.propertyType != SerializedPropertyType.Generic)
                {
                    return string.Format(format, property.displayName, property.propertyType);
                }
                else
                {
                    // TODO(Matt): Does this work for all types, looking at our inspector not all have a $ sign?
                    // Some types are prepended with the string below (e.g. AudioClip), strip it
                    const string pptrStr = "PPtr<$";
                    string elementType = property.arrayElementType;
                    if (elementType.StartsWith(pptrStr))
                    {
                        int closingChevronIndex = elementType.IndexOf('>');
                        elementType = elementType.Substring(pptrStr.Length, closingChevronIndex - pptrStr.Length);
                    }

                    // Return format, replacing the {2} above with {0} so it can be formatted again
                    return string.Format(format, property.displayName, elementType, "{0}");
                }
            }
        }

        public static class Entry
        {
            public const float HeightBuffer = 4f;
            public const float LeftPadding = 10f;
            public const float DeleteButtonPadding = 3f;

            public static class ElementIndex
            {
                public const float LabelOffset = -3f;

                public static readonly Color Colour = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                public static readonly Color SelectedColour = new Color(1f, 1f, 1f, 0.8f);

                private static GUIStyle _style;
                public static GUIStyle Style
                {
                    get
                    {
                        if (_style == null)
                        {
                            _style = new GUIStyle(GUI.skin.label)
                            {
                                stretchWidth = false,
                                alignment = TextAnchor.MiddleCenter
                            };
                        }
                        return _style;
                    }
                }
            }
        }
    }
}
