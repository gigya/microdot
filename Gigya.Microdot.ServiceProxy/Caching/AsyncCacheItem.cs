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
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    public class AsyncCacheItem
    {
        public object Lock { get; } = new object();
        public DateTime NextRefreshTime { get; set; }
        public Task<object> CurrentValueTask { get; set; }
        public Task RefreshTask { get; set; }

        /// <summary>
        /// Group name of this cache item (e.g. method name). 
        /// The group name is used to configure whether extra logData should be written for items of this group.
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// Extra data for log purposes (e.g. arguments list)
        /// </summary>
        public string LogData { get; set; }

        /// <summary>
        /// Should not be cached, as was revoked while calling to factory
        /// </summary>
        public volatile bool AlreadyRevoked;
    }
}