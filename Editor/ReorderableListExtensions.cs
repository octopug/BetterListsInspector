using UnityEditor;
using UnityEditorInternal;

namespace BetterLists
{
    public static class ReorderableListExtensions
    {
        public static void AddElement(this ReorderableList list, bool grabKeyboardFocus)
        {
            int index = list.count;
            list.serializedProperty.InsertArrayElementAtIndex(index);

            if (grabKeyboardFocus)
            {
                list.GrabKeyboardFocus();
            }
        }

        public static void EditElement(this ReorderableList list, int index, string guiElementControlName)
        {
            list.GrabKeyboardFocus();
            EditorGUI.FocusTextInControl(guiElementControlName);

            list.SelectElement(index);
        }

        public static void SelectElement(this ReorderableList list, int index)
        {
            list.index = index;
        }

        public static int GetSelectedElementIndex(this ReorderableList list)
        {
            return list.index;
        }

        public static void SetSize(this ReorderableList list, int size)
        {
            if (size < list.count)
            {
                while (size < list.count)
                {
                    list.serializedProperty.DeleteArrayElementAtIndex(list.count - 1);
                }
            }
            else if (size > list.count)
            {
                while (list.count < size)
                {
                    list.serializedProperty.InsertArrayElementAtIndex(list.count);
                }
            }
        }
    }
}
