using UnityEditor;
#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
#else
using UnityEngine.Experimental.UIElements;
#endif

namespace Unity.Labs.Utils
{
    public abstract class ScriptableSettingsProvider<T> : Internal.ScriptableSettingsProviderBase<T> where T : Internal.ScriptableSettingsBase<T>
    {
        protected static T target
        {
            get
            {
                if (s_Target == null || s_SerializedObject == null)
                    GetSerializedSettings();

                return s_Target;
            }
        }

        protected static SerializedObject serializedObject
        {
            get
            {
                if (s_SerializedObject == null)
                    s_SerializedObject = GetSerializedSettings();

                return s_SerializedObject;
            }
        }

        protected ScriptableSettingsProvider(string path, SettingsScope scope = SettingsScope.User)
            : base(path, scope) {}

        public abstract override void OnActivate(string searchContext, VisualElement rootElement);
        public abstract override void OnGUI(string searchContext);
    }
}

namespace Unity.Labs.Utils.Internal
{
    public abstract class ScriptableSettingsProviderBase<T> : SettingsProvider where T : ScriptableSettingsBase<T>
    {
        protected static T s_Target;
        protected static SerializedObject s_SerializedObject;

        protected ScriptableSettingsProviderBase(string path, SettingsScope scope)
            : base(path, scope) { }

        protected static SerializedObject GetSerializedSettings()
        {
            s_Target = ScriptableSettings<T>.CreateAndLoad();
            return new SerializedObject(s_Target);
        }
    }
}
