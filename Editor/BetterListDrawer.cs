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

        private readonly string _headerStringFormat;
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
            _property = property;

            _headerStringFormat = GuiTheme.Header.BuildStringFormat(_property);
            IsExpanded = new AnimBool(property.isExpanded) { speed = ExpandSpeed };

            _elementIndexLabelStyle = new GUIStyle(GuiTheme.Entry.ElementIndex.Style);

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
                headerHeight = GuiTheme.Header.Height + GuiTheme.Header.Padding
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

            string headerString = BuildHeaderString();
            bool foldoutIsOpen = EditorGUILayout.Foldout(_property.isExpanded, headerString, true, GuiTheme.Buttons.ArrayFoldoutStyle);
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
            rect.height -= GuiTheme.Header.Padding;
            rect.width -= (GuiTheme.Entry.DeleteButtonPadding + GuiTheme.Buttons.AddOrDeleteWidth) * 2;
            rect.y += GuiTheme.Header.SizeLabelOffset;

            // TODO(Matt): Cleanup the size label placement
            int desiredSize = EditorGUI.DelayedIntField(rect, "Size:", List.count);
            List.SetSize(desiredSize);

            // Draw add button
            rect.x = rect.xMax + GuiTheme.Entry.DeleteButtonPadding;
            rect.width = GuiTheme.Buttons.AddOrDeleteWidth;

            if (GUI.Button(rect, GuiTheme.Buttons.AddIcon))
            {
                List.SetSize(List.count + 1);
            }

            // Draw delete button
            rect.x = rect.xMax + GuiTheme.Entry.DeleteButtonPadding;
            rect.width = GuiTheme.Buttons.AddOrDeleteWidth;
            // TODO(Matt): Delay delete until next frame

            bool wasEnabled = GUI.enabled;
            GUI.enabled = List.count > 0;
            if (GUI.Button(rect, GuiTheme.Buttons.DeleteIcon))
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
            float propertyWidth = rect.width - (GuiTheme.Entry.DeleteButtonPadding + GuiTheme.Buttons.AddOrDeleteWidth);
            if (entry.propertyType == SerializedPropertyType.Generic)
            {
                rect.x += GuiTheme.Entry.LeftPadding;
                propertyWidth -= GuiTheme.Entry.LeftPadding;
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
            List.elementHeight = rect.height + GuiTheme.Entry.HeightBuffer;

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
            rect.x = rect.xMax + GuiTheme.Entry.DeleteButtonPadding;
            rect.width = GuiTheme.Buttons.AddOrDeleteWidth;

            if (GUI.Button(rect, GuiTheme.Buttons.DeleteIcon))
            {
                Property.DeleteArrayElementAtIndex(index);
            }
        }

        private void DrawElementIndexLabel(Rect rect, int index)
        {
            bool isSelected = index == List.GetSelectedElementIndex();
            _elementIndexLabelStyle.normal.textColor = isSelected ? GuiTheme.Entry.ElementIndex.SelectedColour : GuiTheme.Entry.ElementIndex.Colour;

            rect.x += GuiTheme.Entry.ElementIndex.LabelOffset;
            rect.width = _elementIndexLabelStyle.fixedWidth;
            EditorGUI.LabelField(rect, index.ToString(), _elementIndexLabelStyle);
        }

        private float CalcElementHeight(int index)
        {
            SerializedProperty entry = _property.GetArrayElementAtIndex(index);
            float propHeight = EditorGUI.GetPropertyHeight(entry, GUIContent.none, true);
            return Mathf.Max(EditorGUIUtility.singleLineHeight, propHeight) + GuiTheme.Entry.HeightBuffer;
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

        private string BuildHeaderString()
        {
            return string.Format(_headerStringFormat, _property.arraySize);
        }

        private bool ShouldShowEntryDisplayName(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.Generic;
        }
    }
}
