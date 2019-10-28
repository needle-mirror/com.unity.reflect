using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Unity.Labs.Utils
{
    /// <summary>
    /// Class that automatically groups a series of object actions together as a single undo-operation
    /// And works in both the editor and player (with player support simply turning off undo-operations)
    /// Mirrors the normal functions you find in the Undo class and collapses them into one operation
    /// when the block is complete
    /// Proper usage of this class is:
    /// using (var undoBlock = new UndoBlock("Desired Undo Message"))
    /// {
    ///     undoBlock.yourCodeToUndo()
    /// }
    /// </summary>
    public class UndoBlock : IDisposable
    {
        string m_UndoLabel;
        int m_UndoGroup;
        bool m_DisposedValue; // To detect redundant calls of Dispose
        bool m_TestMode;

#if UNITY_EDITOR
        bool m_Dirty;
#endif

        public UndoBlock(string undoLabel, bool testMode = false)
        {
#if UNITY_EDITOR
            m_Dirty = false;
            m_TestMode = testMode;
            if (!Application.isPlaying && !m_TestMode)
            {
                Undo.IncrementCurrentGroup();
                m_UndoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(undoLabel);
                m_UndoLabel = undoLabel;
            }
            else
                m_UndoGroup = -1;
#else
            m_UndoGroup = -1;
#endif
        }

        public void RegisterCreatedObject(UnityObject objectToUndo)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !m_TestMode)
            {
                Undo.RegisterCreatedObjectUndo(objectToUndo, m_UndoLabel);
                m_Dirty = true;
            }
#endif
        }

        public void RecordObject(UnityObject objectToUndo)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !m_TestMode)
                Undo.RecordObject(objectToUndo, m_UndoLabel);
#endif
        }

        public void SetTransformParent(Transform transform, Transform newParent)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !m_TestMode)
                Undo.SetTransformParent(transform, newParent, m_UndoLabel);
            else
                transform.parent = newParent;
#else
            transform.parent = newParent;
#endif
        }

        public T AddComponent<T>(GameObject gameObject) where T : Component
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !m_TestMode)
            {
                m_Dirty = true;
                return Undo.AddComponent<T>(gameObject);
            }
#endif

            return gameObject.AddComponent<T>();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_DisposedValue)
            {
                if (disposing && m_UndoGroup > -1)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying && !m_TestMode)
                    {
                        Undo.CollapseUndoOperations(m_UndoGroup);
                        if (m_Dirty)
                        {
                            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                        }
                    }

                    m_Dirty = false;
#endif
                }

                m_DisposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
    }
}
