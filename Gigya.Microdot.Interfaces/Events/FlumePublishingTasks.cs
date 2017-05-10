using System.Threading.Tasks;

namespace Gigya.Microdot.Interfaces.Events
{
    public class FlumePublishingTasks
    {
        public Task<bool> ToFlume = null;
        public Task<bool> ToFlumeAudit = null;
    }
}