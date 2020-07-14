using System.Threading.Tasks;

namespace Metrics.Utils
{
    internal static class TaskEx
    {
        public static readonly Task CompletedTask = Task.FromResult(0);
    }
}