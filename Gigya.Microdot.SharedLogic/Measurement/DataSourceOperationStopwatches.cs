using System;
using System.Linq;

using Gigya.Microdot.SharedLogic.Utils;

namespace Gigya.Microdot.SharedLogic.Measurement
{
    /// <summary>A collection of stopwatches for typical data source operations (read, write, etc).</summary>
    [Serializable]
    public class DataSourceOperationStopwatches {

        public DataSourceOperationStopwatches() {
            Read   = new ConcurrentStopwatch();
            Write  = new ConcurrentStopwatch();
            Delete = new ConcurrentStopwatch();
            Total = new Sum(this);
        }

        public ConcurrentStopwatch Read;
        public ConcurrentStopwatch Write;
        public ConcurrentStopwatch Delete;
        public Sum                 Total;

        public ConcurrentStopwatch this[DataSourceOperation op] {
            get {
                switch (op) {
                    case DataSourceOperation.Read:          return Read;
                    case DataSourceOperation.Write:         return Write;
                    case DataSourceOperation.Delete:        return Delete;
                    default:                                throw GAssert.LogAndMakeFailureException();
                }
            }
        }

        [Serializable]
        public class Sum: IMeasurement {
            readonly DataSourceOperationStopwatches parent;

            public Sum(DataSourceOperationStopwatches parent) { this.parent = parent; }
            public double? ElapsedMS
            {
                get
                {
                    var res = new[] {parent.Read.ElapsedMS, parent.Write.ElapsedMS, parent.Delete.ElapsedMS}.Sum();
                    return res == 0 ? null : res;
                }
            }
            public long? Calls
            {
                get
                {
                    var res = new[] { parent.Read.Calls, parent.Write.Calls, parent.Delete.Calls }.Sum();
                    return res == 0 ? null:res;
                }
            }
        }
    }
}