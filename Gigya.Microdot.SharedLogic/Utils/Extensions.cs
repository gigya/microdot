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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Events;

namespace Gigya.Microdot.SharedLogic.Utils
{
    public static class Extensions
    {
        public static string RawMessage(this Exception ex) => (ex as SerializableException)?.RawMessage ?? ex.Message;

        public static IEnumerable<KeyValuePair<string, object>> GetUnencryptedTags(this Dictionary<string, ContextTag> tags)
        {
            return tags.Where(e => !e.Value.IsEncrypted)
                       .Select(e => new KeyValuePair<string, object>(e.Key, e.Value.Value));
        }

        public static IEnumerable<KeyValuePair<string, object>> GetEncryptedTags(this Dictionary<string, ContextTag> tags)
        {
            return tags.Where(e => e.Value.IsEncrypted)
                       .Select(e => new KeyValuePair<string, object>(e.Key, e.Value.Value));
        }
    }
}
