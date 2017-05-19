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