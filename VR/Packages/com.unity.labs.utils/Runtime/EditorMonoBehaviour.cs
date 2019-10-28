using System.Collections;
using UnityEngine;

namespace Unity.Labs.Utils
{
    /// <summary>
    /// Used for launching co-routines
    /// TODO: Use EditorCoroutines package
    /// </summary>
    public sealed class EditorMonoBehaviour : MonoBehaviour
    {
        public static EditorMonoBehaviour instance { get; private set; }

        void Awake()
        {
            instance = this;
        }

        internal static void StartEditorCoroutine(IEnumerator routine)
        {
            // Avoid null-coalescing operator for UnityObject
            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (instance)
                instance.StartCoroutine(routine);
        }
    }
}
