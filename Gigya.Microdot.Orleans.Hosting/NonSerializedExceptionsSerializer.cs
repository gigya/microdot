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
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;

// ReSharper disable AssignNullToNotNullAttribute

namespace Gigya.Microdot.Orleans.Hosting
{
    /// <summary>
    /// Serializes exceptions that aren't marked with <see cref="SerializableAttribute"/>
    /// </summary>
    /// <remarks>
    /// In netcore some exceptions may not have the <see cref="SerializableAttribute"/> which
    /// makes their binary serialization impossible with the framework itself. To tackle that
    /// this serializer reroutes the serializetion to <see cref="ILBasedSerializer"/> and 
    /// forces the exception stack trace field to be initialized.
    /// The serializer also handles <see cref="HttpRequestException"/> since it failes to
    /// serialize the stack trace correctly (net472).
    /// </remarks>
    public class NonSerializedExceptionsSerializer : IExternalSerializer
    {
        public object DeepCopy(object source, ICopyContext context)
        {
            return ((ILBasedSerializer)context.ServiceProvider.GetService(typeof(ILBasedSerializer))).DeepCopy(source, context);
        }

        public object Deserialize(Type expectedType, IDeserializationContext context)
        {
            return ((ILBasedSerializer)context.ServiceProvider.GetService(typeof(ILBasedSerializer))).Deserialize(expectedType, context);
        }

        public bool IsSupportedType(Type itemType)
        {
            return
                typeof(Exception).IsAssignableFrom(itemType)
                && itemType.GetCustomAttributes(typeof(SerializableAttribute), false).Length == 0 ||
                typeof(HttpRequestException).IsAssignableFrom(itemType);
        }

        public void Serialize(object item, ISerializationContext context, Type expectedType)
        {
            getStateTraceStringField(item.GetType())
                .SetValue(item, ((Exception)item).StackTrace);

            ((ILBasedSerializer)context.ServiceProvider.GetService(typeof(ILBasedSerializer))).Serialize(item, context, expectedType);

            FieldInfo getStateTraceStringField(Type type)
            {
                if (type == typeof(Exception))
                    return type.GetField("_stackTraceString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                return getStateTraceStringField(type.BaseType);
            }
        }
    }
}