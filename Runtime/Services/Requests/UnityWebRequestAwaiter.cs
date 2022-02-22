using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace UnityEngine.Reflect
{
    internal static class ExtensionMethods
    {
        internal static TaskAwaiter GetAwaiter(this UnityWebRequestAsyncOperation operation)
        {
            var taskCompletionSource = new TaskCompletionSource<object>();
            operation.completed += obj => { taskCompletionSource.SetResult(null); };
            var resultTask = (Task) taskCompletionSource.Task;
            return resultTask.GetAwaiter();
        }
    }
}