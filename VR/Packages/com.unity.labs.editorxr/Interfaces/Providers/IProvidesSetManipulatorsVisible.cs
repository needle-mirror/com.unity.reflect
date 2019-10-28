using Unity.Labs.ModuleLoader;
using UnityEditor;
using UnityEngine;

namespace Unity.Labs.EditorXR.Interfaces
{
    /// <summary>
    /// Provide access to the spatial hash
    /// </summary>
    public interface IProvidesSetManipulatorsVisible : IFunctionalityProvider
    {
      /// <summary>
      /// Show or hide the manipulator(s)
      /// </summary>
      /// <param name="requester">The requesting object that is wanting to set all manipulators visible or hidden</param>
      /// <param name="visibility">Whether the manipulators should be shown or hidden</param>
      void SetManipulatorsVisible(IUsesSetManipulatorsVisible requester, bool visibility);
    }
}
