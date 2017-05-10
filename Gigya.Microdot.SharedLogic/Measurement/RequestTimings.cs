using System;
using System.Collections.Concurrent;
using System.Runtime.Remoting.Messaging;

namespace Gigya.Microdot.SharedLogic.Measurement
{
    /// <summary>This class enables measuring the time it took to perform various operations (read, write, etc) against
    /// various data sources (mysql, mongo, etc), the time it took to perform calls to providers, and the total time spent
    /// processing the current request.</summary>
    [Serializable]
    public class RequestTimings:MarshalByRefObject
    {
        internal readonly ConcurrentDictionary<string, Aggregator> UserStats = new ConcurrentDictionary<string, Aggregator>();

        /// <summary>Time of the ongoing request.</summary>
        public ConcurrentStopwatch Request = new ConcurrentStopwatch();

        /// <summary>A collection of stopwatches for each type of data source (MySql, Mongo, Memcache, etc) and operation
        /// (read, write, etc).</summary>
        public DataSourceStopwatches DataSource = new DataSourceStopwatches();

        /// <summary>Timings for the request currently being processed.</summary>
        public static RequestTimings Current => GetOrCreate();


        public static RequestTimings GetOrCreate()
        {
            RequestTimings timings = (RequestTimings)CallContext.LogicalGetData("request timings");            
            if (timings==null)
            {
                timings = new RequestTimings();
                CallContext.LogicalSetData("request timings", timings);
            }

            return timings;
        }

        /// <summary>Clears all timings for the request currently being processed. BEWARE!</summary>
        public static void ClearCurrentTimings()
        {
            CallContext.FreeNamedDataSlot("request timings");
        }


        /// <summary>Starts measuring the top-level processing of the current request. Handy when it's inconvenient for you
        /// to call Request.Measure()</summary>
        public void MarkRequestStartTime() { Request.Start(); }


    }
}
