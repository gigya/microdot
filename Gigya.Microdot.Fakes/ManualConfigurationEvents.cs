using System.Threading.Tasks.Dataflow;

using Gigya.Microdot.Configuration;

namespace Gigya.Microdot.Fakes
{
    public class ManualConfigurationEvents : IConfigurationDataWatcher
    {
        private readonly BroadcastBlock<bool> block  = new BroadcastBlock<bool>(null);


        public void RaiseChangeEvent()
        {
            block.Post(true);
           // Task.Delay(100).Wait();
        }


        public ISourceBlock<bool> DataChanges => block;
    }
}