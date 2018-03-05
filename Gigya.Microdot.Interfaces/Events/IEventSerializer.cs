using System;
using System.Collections.Generic;

namespace Gigya.Microdot.Interfaces.Events
{

    public interface IEventSerializer
    {
        IEnumerable<SerializedEventField> Serialize(IEvent evt, Func<EventFieldAttribute, bool> predicate = null);
    }
}
