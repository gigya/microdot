#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

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
            CouchDb = new DataSourceOperationStopwatches();
        }


        public DataSourceOperationStopwatches MySql;
        public DataSourceOperationStopwatches Mongo;
        public DataSourceOperationStopwatches File;
        public DataSourceOperationStopwatches Memcached;
        public DataSourceOperationStopwatches Hades;
        public DataSourceOperationStopwatches CouchDb;

        public DataSourceOperationStopwatches this[DataSourceType type] {
            get {
                switch (type) {
                    case DataSourceType.MySql:     return MySql;
                    case DataSourceType.Mongo:     return Mongo;
                    case DataSourceType.File:      return File;
                    case DataSourceType.Memcached: return Memcached;
                    case DataSourceType.Hades:     return Hades;
                    case DataSourceType.CouchDb:   return CouchDb;
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

    public enum DataSourceType { MySql, Mongo, File, Memcached, Hades, CouchDb }
    public enum DataSourceOperation { Read, Write, Delete }
}