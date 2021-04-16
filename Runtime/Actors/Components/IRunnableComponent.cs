using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Reflect.Actor
{
    public interface IRunnableComponent
    {
        bool Tick(TimeSpan endTime, CancellationToken token);
    }

    public interface IAsyncComponent : IRunnableComponent
    {
        Task WaitAsync(CancellationToken token);
    }
}
