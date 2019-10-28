using Unity.Labs.ModuleLoader;

namespace Unity.Labs.EditorXR.Interfaces
{
    /// <summary>
    /// Gives decorated class access to the ability to reset workspaces
    /// </summary>
    public interface IUsesResetWorkspaces : IFunctionalitySubscriber<IProvidesResetWorkspaces>
    {
    }

    public static class UsesResetWorkspacesMethods
    {
        /// <summary>
        /// Reset all open workspaces
        /// </summary>
        /// <param name="user">The functionality user</param>
        public static void ResetWorkspaceRotations(this IUsesResetWorkspaces user)
        {
#if !FI_AUTOFILL
            user.provider.ResetWorkspaceRotations();
#endif
        }
    }
}
