using System;

using Gigya.Microdot.SharedLogic.Utils;

namespace Gigya.Microdot.SharedLogic.Measurement
{
    /// <summary>A collection of stopwatches for each type of data source (MySql, Mongo, Memcache, etc) and operation (read,
    /// write, etc).</summary>
    [Serializable]
    public class DataSourceStopwatches {

        public DataSourceStopwatches() {
            MySql     = new DataSourceOperationStopwatches();
            Mongo = new DataSourceOperationStopwatches();
            File      = new DataSourceOperationStopwatches();
            Memcached = new DataSourceOperationStopwatches();
            Hades     = new DataSourceOperationStopwatches();
        }


        public DataSourceOperationStopwatches MySql;
        public DataSourceOperationStopwatches Mongo;
        public DataSourceOperationStopwatches File;
        public DataSourceOperationStopwatches Memcached;
        public DataSourceOperationStopwatches Hades;

        public DataSourceOperationStopwatches this[DataSourceType type] {
            get {
                switch (type) {
                    case DataSourceType.MySql:     return MySql;
                    case DataSourceType.Mongo: return Mongo;
                    case DataSourceType.File:      return File;
                    case DataSourceType.Memcached: return Memcached;
                    case DataSourceType.Hades:     return Hades;
                    default: throw GAssert.LogAndMakeFailureException();
                }
            }
        }


        /// <summary>Convenience method. Measures the time if type is non-null, otherwise doesn't measure
        /// anything.</summary>
        /// 
        public IDisposable OptionalMeasure(DataSourceOperation op, DataSourceType? type = null) {
            if (type != null)
                return this[type.Value][op].Measure();
            else return new ConcurrentStopwatch.Closure { action = () => {}};
        }
    }

    public enum DataSourceType { MySql, Mongo, File, Memcached, Hades }
    public enum DataSourceOperation { Read, Write, Delete };
}