using UnityEngine.UIElements;

namespace UnityEditor.Reflect
{
    internal static class UIToolkitUtils
    {
        public static void DisplayNone(VisualElement element)
        {
            element.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }
        
        public static void DisplayFlex(VisualElement element)
        {
            element.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
        }
        
        public static void Show(VisualElement element)
        {
            element.visible = true;
        }
        
        public static void Hide(VisualElement element)
        {
            element.visible = false;
        }
    }
}
