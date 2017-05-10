using System.Threading.Tasks;

using Gigya.Microdot.Interfaces.Events;

namespace Gigya.Microdot.Fakes
{
    public class NullEventPublisher:IEventPublisher
    {
        static readonly FlumePublishingTasks flumePublishingTasks = new FlumePublishingTasks { ToFlume = Task.FromResult(true), ToFlumeAudit = Task.FromResult(true) };

        public FlumePublishingTasks TryPublish(IEvent evt)
        {            
            return flumePublishingTasks;
        }
    }
}