using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gigya.Microdot.SharedLogic.Measurement
{    
    public class ServicesCallsDictionary : ConcurrentDictionary<string, ConcurrentStopwatch>
    {
        public new ConcurrentStopwatch this[string serviceName] { get { return GetOrAdd(serviceName, x => new ConcurrentStopwatch()); } }
    }
}
