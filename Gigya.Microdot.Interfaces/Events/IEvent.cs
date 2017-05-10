using System;

using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.Interfaces.Events
{
    public interface IEvent
    {
        string FlumeType { get; }

        bool ShouldAudit { get; }
        
        DateTime Timestamp { get;  }

        IEventConfiguration Configuration { get; set; }

        IEnvironmentVariableProvider EnvironmentVariableProvider { get; set; }
    }
}