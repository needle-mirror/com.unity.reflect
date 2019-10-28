#if !NET_4_6
using System.Diagnostics;

namespace Unity.Labs.Utils
{
    public static class StopwatchExtensions
    {
        public static void Restart(this Stopwatch stopwatch)
        {
            stopwatch.Stop();
            stopwatch.Reset();
            stopwatch.Start();
        }
    }
}
#endif
