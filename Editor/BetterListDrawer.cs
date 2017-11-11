using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Custom drawer for the ReorderableList, fixing lots of
/// the clunkiness with the original
/// 
/// ref: http://va.lent.in/unity-make-your-lists-functional-with-reorderablelist/
/// </summary>

namespace BetterLists
{
    public class BetterListDrawer
    {
        private const float ExpandSpeed = 3f;
        private const float HeaderHeight = 16f;
        private const float HeaderSizeLabelOffset = 1f;
        private const float HeaderPadding = 4f;
        private const float EntryHeightBuffer = 4f;
        private const float EntryLeftPadding = 10f;
        private const float ElementIndexLabelOffset = -3f;
        private const float DeleteEntryButtonWidth = 18f;
        private const float ElementToDeleteButtonPadding = 3f;
        private static readonly Color ElementIndexColour = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        private static readonly Color ElementIndexSelectedColour = new Color(1f, 1f, 1f, 0.8f);

        private readonly string _headerStringFormat;
        private GUIContent _addButtonContent;
        private GUIContent _deleteButtonContent;
        private SerializedProperty _property;
        private bool _addedElementSinceLastDraw;
        private GUIStyle _elementIndexLabelStyle;
        private int _countLastDraw;

        public AnimBool IsExpanded { get; private set; }
        public bool ToggleExpandNextRepaint { get; set; }
        public ReorderableList List { get; private set; }

        public SerializedProperty Property
        {
            get { return _property; }

            set
            {
                _property = value;
                List.serializedProperty = _property;
            }
        }

        // TODO(Matt): Add support for drag/drop items onto an array property to add them
        public BetterListDrawer(SerializedProperty property)
        {
            // TODO(Matt): Re-use loaded icons and pass in
            Texture addIcon = Resources.Load("reorderable_list.plus_icon") as Texture;
            _addButtonContent = new GUIContent(addIcon);

            Texture deleteIcon = Resources.Load("reorderable_list.minus_icon") as Texture;
            _deleteButtonContent = new GUIContent(deleteIcon);

            _property = property;

            _headerStringFormat = BuildHeaderStringFormat(_property);
            IsExpanded = new AnimBool(property.isExpanded) { speed = ExpandSpeed };

            // TODO(Matt): Pass this in? Maybe move all GUI styles elsewhere
            if (_elementIndexLabelStyle == null)
            {
                CreateElementIndexLabelStyle();
            }

            CreateList();
        }

        // TODO(Matt): Do we need this destructor?
        ~BetterListDrawer()
        {
            _property = null;
            List = null;
        }

        private void CreateList()
        {
            bool draggable = true;
            bool header = true;
            bool add = true;
            bool remove = true;

            List = new ReorderableList(Property.serializedObject, Property, draggable, header, add, remove)
            {
                headerHeight = HeaderHeight + HeaderPadding
            };

            List.displayAdd = false;
            List.displayRemove = false;

            List.drawHeaderCallback += DrawHeader;
            List.drawElementCallback += DrawEntry;
            List.elementHeightCallback += CalcElementHeight;
            List.onAddCallback += _ => AddElement();
        }

        public void DrawFoldoutControl()
        {
            // Cache the event type so we can detect if the event was mouse/kb
            // (avoid input being detected in layout/repaint events)
            EventType evtType = Event.current.type;

            var style = new GUIStyle(EditorStyles.foldout);
            style.richText = true;

            string headerString = BuildHeaderString();
            bool foldoutIsOpen = EditorGUILayout.Foldout(_property.isExpanded, headerString, true, style);
            if (foldoutIsOpen != _property.isExpanded)
            {
                bool consumedInputEvent =
                    (evtType == EventType.MouseUp || evtType == EventType.KeyDown) &&
                    Event.current.type == EventType.Used;

                if (consumedInputEvent)
                {
                    ToggleExpandNextRepaint = true;
                }
            }
        }

        public void OnDrawBegin()
        {
            if (List.count != _countLastDraw)
            {
                UpdateElementIndexLabelStyle();
                _countLastDraw = List.count;
            }
        }

        private void DrawHeader(Rect rect)
        {
            rect.height -= HeaderPadding;
            rect.width -= (ElementToDeleteButtonPadding + DeleteEntryButtonWidth) * 2;
            rect.y += HeaderSizeLabelOffset;

            // TODO(Matt): Cleanup the size label placement
            int desiredSize = EditorGUI.DelayedIntField(rect, "Size:", List.count);
            List.SetSize(desiredSize);

            // Draw add button
            rect.x = rect.xMax + ElementToDeleteButtonPadding;
            rect.width = DeleteEntryButtonWidth;

            if (GUI.Button(rect, _addButtonContent))
            {
                List.SetSize(List.count + 1);
            }

            // Draw delete button
            rect.x = rect.xMax + ElementToDeleteButtonPadding;
            rect.width = DeleteEntryButtonWidth;
            // TODO(Matt): Delay delete until next frame

            bool wasEnabled = GUI.enabled;
            GUI.enabled = List.count > 0;
            if (GUI.Button(rect, _deleteButtonContent))
            {
                Property.DeleteArrayElementAtIndex(List.count - 1);
            }
            GUI.enabled = wasEnabled;
        }

        // TODO(Matt): Replace the remove button with one alongside each entry?
        private void DrawEntry(Rect rect, int index, bool active, bool focused)
        {
            SerializedProperty entry = _property.GetArrayElementAtIndex(index);

            rect.height = EditorGUI.GetPropertyHeight(entry, GUIContent.none, true);
            rect.y += 1; // TODO(Matt): Move magic number to const

            // Some types contain children, we need to indent to avoid overlap with the drag control and the foldout button
            float propertyWidth = rect.width - (ElementToDeleteButtonPadding + DeleteEntryButtonWidth);
            if (entry.propertyType == SerializedPropertyType.Generic)
            {
                rect.x += EntryLeftPadding;
                propertyWidth -= EntryLeftPadding;
            }

            // Draw an index label
            DrawElementIndexLabel(rect, index);
            rect.x += _elementIndexLabelStyle.fixedWidth;

            propertyWidth -= _elementIndexLabelStyle.fixedWidth;

            // Draw the property
            string controlName = GetElementGuiControlName(index);
            GUI.SetNextControlName(controlName);

            rect.width = propertyWidth;
            EditorGUI.PropertyField(rect, entry, GUIContent.none, true);
            List.elementHeight = rect.height + EntryHeightBuffer;

            // Draw name label
            if (ShouldShowEntryDisplayName(entry))
            {
                // TODO(Matt): Could add an attribute which is used to set a custom display name per type?
                EditorGUI.LabelField(rect, entry.displayName);
            }

            // Edit element if one was newly added
            if (_addedElementSinceLastDraw)
            {
                bool isFinalElement = index == List.count - 1;
                if (isFinalElement)
                {
                    List.EditElement(index, controlName);
                    _addedElementSinceLastDraw = false;
                }
            }
            else
            {
                // When editing an element then select it in the list (easier to select this way for deletion)
                if (GUI.GetNameOfFocusedControl() == controlName)
                {
                    List.SelectElement(index);
                }
            }

            // Draw delete button
            rect.x = rect.xMax + ElementToDeleteButtonPadding;
            rect.width = DeleteEntryButtonWidth;

            if (GUI.Button(rect, _deleteButtonContent))
            {
                Property.DeleteArrayElementAtIndex(index);
            }
        }

        private void DrawElementIndexLabel(Rect rect, int index)
        {
            bool isSelected = index == List.GetSelectedElementIndex();
            _elementIndexLabelStyle.normal.textColor = isSelected ? ElementIndexSelectedColour : ElementIndexColour;

            rect.x += ElementIndexLabelOffset;
            rect.width = _elementIndexLabelStyle.fixedWidth;
            EditorGUI.LabelField(rect, index.ToString(), _elementIndexLabelStyle);
        }

        private float CalcElementHeight(int index)
        {
            SerializedProperty entry = _property.GetArrayElementAtIndex(index);
            float propHeight = EditorGUI.GetPropertyHeight(entry, GUIContent.none, true);
            return Mathf.Max(EditorGUIUtility.singleLineHeight, propHeight) + EntryHeightBuffer;
        }

        private void AddElement()
        {
            List.AddElement(true);
            _addedElementSinceLastDraw = true;
        }

        private string GetElementGuiControlName(int index)
        {
            const string format = "prop_{0}";
            return string.Format(format, index);
        }

        private void UpdateElementIndexLabelStyle()
        {
            // Clear fixed width and update it based on widest content
            _elementIndexLabelStyle.fixedWidth = 0;

            float minWidth, _;
            int finalIndex = List.count - 1;
            var content = new GUIContent(finalIndex.ToString());
            _elementIndexLabelStyle.CalcMinMaxWidth(content, out minWidth, out _);
            _elementIndexLabelStyle.fixedWidth = minWidth;
        }

        private void CreateElementIndexLabelStyle()
        {
            _elementIndexLabelStyle = new GUIStyle(GUI.skin.label);
            _elementIndexLabelStyle.stretchWidth = false;
            _elementIndexLabelStyle.alignment = TextAnchor.MiddleCenter;
        }

        private string BuildHeaderString()
        {
            return string.Format(_headerStringFormat, _property.arraySize);
        }

        private bool ShouldShowEntryDisplayName(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.Generic;
        }

        private static string BuildHeaderStringFormat(SerializedProperty property)
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
}
