using System.Threading.Tasks.Dataflow;

namespace Gigya.Microdot.Configuration
{
    public interface IConfigurationDataWatcher
    {
        ISourceBlock<bool> DataChanges { get; }
    }
}