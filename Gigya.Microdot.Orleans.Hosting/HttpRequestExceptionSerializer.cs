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

#endregion Copyright

using Orleans.Serialization;
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;

// ReSharper disable AssignNullToNotNullAttribute

namespace Gigya.Microdot.Orleans.Hosting
{
    /// <summary>
    /// This class is called by the Orleans runtime to perform serialization for HttpRequestException.
    /// Because with no reason the stack trace of HttpRequestException is not deserialized properly.
    /// See Gigya.Microdot.UnitTests.Serialization.ExceptionSerializationTests
    /// https://github.com/dotnet/orleans/issues/5876
    /// </summary>
    public class HttpRequestExceptionSerializer : IExternalSerializer
    {
        public virtual bool IsSupportedType(Type itemType)
        {
            return itemType == typeof(HttpRequestException);
        }

        public virtual object DeepCopy(object source, ICopyContext context)
        {
            using(var toStream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(toStream, source);
                toStream.Position = 0;
                var obTarget = formatter.Deserialize(toStream);
                toStream.Close();
                return obTarget;
            }
        }

        public virtual void Serialize(object item, ISerializationContext context, Type expectedType)
        {
            using(var fromStream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(fromStream, item);
                fromStream.Position = 0;
                SerializationManager.SerializeInner(fromStream.ToArray(), context, expectedType);
            }
        }

        public virtual object Deserialize(Type expectedType, IDeserializationContext context)
        {
            var bytes = SerializationManager.DeserializeInner<byte[]>(context);
            using(var stream = new MemoryStream())
            {
                stream.Write(bytes, 0, bytes.Length);
                var formatter = new BinaryFormatter();
                stream.Position = 0;
                object obTarget = formatter.Deserialize( stream);
                stream.Close();
                return obTarget;
            }
        }
    }
}