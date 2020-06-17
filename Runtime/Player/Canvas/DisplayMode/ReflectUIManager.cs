using UnityEngine.EventSystems;
using UnityEngine.Reflect.Controller;

namespace UnityEngine.Reflect
{
    public class ReflectUIManager : MonoBehaviour
    {
        public Canvas MainCanvas;
        public EventSystem EventSystem;
        public FreeCamController FreeCamController;
        public SettingsTopMenu SettingsMenu;
    }
}
