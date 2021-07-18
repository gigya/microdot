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
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Text;
using Gigya.ServiceContract.Exceptions;

namespace Gigya.Common.Contracts.Exceptions
{
	/// <summary>
	/// Abstract base class for all exceptions which support advanced serialization features, such as automatic property
	/// serialization and tags. When writing custom exceptions, please read the remarks section.
	/// </summary>
	/// 
	/// <remarks>
	/// When writing custom exceptions, make sure you do the following:
	/// <list type="number">
	///		<item>
	///			Mark the exception with the SerializableAttribute.
	///		</item>
	///		<item>
	///			Do not inherit from SerializableException, but instead inherit from one of the three concrete
	///         implementations:
	///			<see cref="ProgrammaticException"/>, <see cref="EnvironmentException"/>, <see cref="RequestException"/>.
	///		</item>
	///		<item>
	///			Make sure to call the appropriate base constructor from your constructors.
	///		</item>
	///		<item>
	///			Include a protected constructor with parameters (<see cref="SerializationInfo"/>,
	///         <see cref="StreamingContext"/>) which calls the base constructor with same parameters.
	///		</item>
	/// </list>
	///
	/// Exception that correctly inherit as described above, have all their public properties that have a setter
	/// (including a non-public setter) serialized and deserialized automatically, in addition to tags (fields are not
	/// serialized). The values of those properties must be marked with a SerializableAttribute since the
	/// serialization is sometimes performed by <see cref="BinaryFormatter"/> (within Orleans). Also, the values of
	/// these properties should be immutable.
	/// 
	/// If a getter of a property returns null or throws an exception during serialization, it will not be serialized.
	/// However, if a setter of a property throws an exception during deserialization, it will throw a
	/// <see cref="SerializationException"/>.
	/// 
	/// <note>There is no need to override the GetObjectData method in your custom exception to have your properties
	/// automatically serialized.</note>
	/// 
	/// When an attempt is made to deserialize an exception, but the exact type of the exception is not available in
	/// that context (e.g. a derived exception type that only exists on the serializing side, not the deserializing
	/// side), then an attempt is made to deserialize that exception as it's base-type (the inheritance hierarchy is
	/// serialized together with the exception), and if that fails, with the base-base-type, etc. When deserializing an 
	/// exception into a base type, there could be some properties that existed on the original derived type but do not
	/// exist on the base type. Those properties were serialized, but there is no corresponding property to deserialize
	/// them into. These values will be available in the the <see cref="ExtendedProperties"/> property. This can also
	/// occur when an exception class changes (e.g. a property is renamed or removed), but only the serializing side is
	/// using the new version, therefore there will be properties that will not match and will be accessible on the 
	/// deserializing side via the <see cref="ExtendedProperties"/> property.
	/// 
	/// Those differences can also mean that data could be missing for a certain property (if that property was removed
	/// from the class definition on the serializing side). In such a case, the property will be left uninitialized
	/// (with its default value), unless it is marked with a <see cref="RequiredAttribute"/>, in which case the
	/// deserialization will fail with a <see cref="SerializationException"/> specifying which required property values
	/// are missing.
	/// </remarks>
	/// 
	/// <example>
	/// The following is an example of a correctly implemented exception:
	/// <code><![CDATA[
	/// [Serializable]
	/// public class DemoServiceException : RequestException
	/// {
	/// 	public string AccountName { get; set; }
	/// 	public IUser User { get; private set; }
	/// 
	/// 	public DemoServiceException(string message, string accountName, IUser user, Exception innerException = null, Tags encrypted = null, Tags unencrypted = null)
	/// 		: base(message, innerException, encrypted, unencrypted)
	/// 	{
	/// 		AccountName = accountName;
	/// 		User = user;
	/// 	}
	/// 
	/// 	protected DemoServiceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	/// }
	/// ]]></code>
	/// </example>
	[Serializable]
	public abstract class SerializableException : Exception
	{
	    private const string EXTENDED_PROPERTIES_NAMES_KEY = "ExtendedPropertiesNames";
	    private const string EXTENDED_PROPERTIES_VALUES_KEY = "ExtendedPropertiesValues";
	    private const string BREADCRUMBS_KEY = "Breadcrumbs";

        private readonly Dictionary<string, object> _extendedProperties;
	    private Breadcrumb[] _breadcrumbs;

		/// <summary>
		/// A read-only dictionary of tags that must be encrypted when stored. They are, however, serialized (and
		/// therefore transmitted) without encryption.
		/// </summary>
		public IDictionary<string, string> EncryptedTags { get; private set; }
		/// <summary>
		/// A read-only dictionary of tags that needn't be encrypted when stored.
		/// </summary>
		public IDictionary<string, string> UnencryptedTags { get; private set; }

		/// <summary>
		/// A read-only dictionary of property values that failed to deserialize into a matching property (either
		/// because there was no property with a matching name, or the setter of the property threw an exception). Each
		/// key is the name of the serialized property and the value is the 
		/// serialized property's value.
		/// </summary>
		// note: no need to create new object each time we access the property
		public IReadOnlyDictionary<string, object> ExtendedProperties => _extendedProperties;

        // note: no need to create new object each time we access the property
        public IReadOnlyList<Breadcrumb> Breadcrumbs => _breadcrumbs;

	    /// <summary>
        /// Initializes a new instance of the <see cref="SerializableException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">Optional. The exception that is the cause of the current exception.</param>
        /// <param name="encrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data
        /// about the exception, which must be encrypted when stored.</param>
        /// <param name="unencrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data
        /// about the exception, which needn't be encrypted when stored.</param>
        protected SerializableException(string message, Exception innerException = null, Tags encrypted = null, Tags unencrypted = null) : base(message, innerException)
		{
            EncryptedTags = encrypted;
			UnencryptedTags = unencrypted;

			_extendedProperties = new Dictionary<string, object>();
            _breadcrumbs = new Breadcrumb[0];
		}

		/// <summary>Initializes a new instance of the <see cref="SerializableException"/> class with serialized
		/// data.</summary>
		/// <param name="info"> The <see cref="SerializationInfo"/> that holds the serialized object data about the
		/// exception being thrown.</param>
		/// <param name="context">The <see cref="StreamingContext"/> that contains  contextual information about the
		/// source or destination.</param>
		/// <exception cref="ArgumentNullException">The <paramref name="info"/> parameter is null.</exception>
		/// <exception cref="SerializationException">The class name is null, <see cref="Exception.HResult"/> is zero (0)
		///  or deserialization of extended properties encountered an error.</exception>
		protected SerializableException(SerializationInfo info, StreamingContext context) : base(info, new StreamingContext(StreamingContextStates.CrossAppDomain, context.State))
		{
            _extendedProperties = new Dictionary<string, object>();

            var extendedPropertiesNames = (string[])info.GetValue(EXTENDED_PROPERTIES_NAMES_KEY, typeof(string[]));
			var extendedPropertiesValues = (object[])info.GetValue(EXTENDED_PROPERTIES_VALUES_KEY, typeof(object[]));

            if (extendedPropertiesNames == null && extendedPropertiesValues == null)
                return;

            if (extendedPropertiesNames == null || extendedPropertiesValues == null || extendedPropertiesNames.Length != extendedPropertiesValues.Length)
                throw new SerializationException("Failed to deserialize exception - bad extended properties data.");

		    for (int i = 0; i < extendedPropertiesNames.Length; i++)
		        _extendedProperties.Add(extendedPropertiesNames[i], extendedPropertiesValues[i]);

            _extendedProperties.TryGetValue(nameof(EncryptedTags), out object tags);
            EncryptedTags = tags as IDictionary<string, string>;
		    _extendedProperties.Remove(nameof(EncryptedTags));

            _extendedProperties.TryGetValue(nameof(UnencryptedTags), out tags);
            UnencryptedTags = tags as IDictionary<string, string>;
            _extendedProperties.Remove(nameof(UnencryptedTags));

            var properties = GetProperties().ToDictionary(p => p.Name);

            foreach (var extendedProperty in _extendedProperties.ToArray())
            {

                if (properties.TryGetValue(extendedProperty.Key, out PropertyInfo property))
                {
                    try
                    {
                        property.SetValue(this, JsonHelper.ConvertWeaklyTypedValue(extendedProperty.Value, property.PropertyType));
                        _extendedProperties.Remove(extendedProperty.Key);
                        properties.Remove(extendedProperty.Key);
                    }
                    catch (Exception ex)
                    {
                        throw new SerializationException($"Failed to deserialize exception - failed to populate extended property '{property.Name}'. See InnerException for details.", ex);
                    }
                }
            }

		    try
		    {
		        _breadcrumbs = (Breadcrumb[])info.GetValue(BREADCRUMBS_KEY, typeof(Breadcrumb[]));
		    }
		    catch (SerializationException)
		    {
		        _breadcrumbs = new Breadcrumb[0];
            }
        }


        /// <summary>
        /// When overridden in a derived class, sets the <see cref="T:System.Runtime.Serialization.SerializationInfo"/>
        /// with information about the exception.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> that holds the
        /// serialized object data about the exception being thrown. </param>
        /// <param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext"/> that contains
        /// contextual information about the source or destination. </param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="info"/> parameter is a null reference
        /// (Nothing in Visual Basic). </exception>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("_Messages", string.Join(" --> ", GetAllExceptions(this).Reverse().Select(e => $"[{e.GetType().Name}] {e.Message}")));
			
			base.GetObjectData(info, context);

            var properties = GetCustomAndExtendedProperties();
            properties.Add(nameof(EncryptedTags), EncryptedTags);
            properties.Add(nameof(UnencryptedTags), UnencryptedTags);

            // Must split dictionary into keys and values because dictionary implements ISerializable itself and
            // different serializers (BinaryFormatter, JSON.NET) behave differently for non-root ISerializable 
            // objects in the object graph. See http://stackoverflow.com/a/18379360/149265.
            info.AddValue(EXTENDED_PROPERTIES_NAMES_KEY, properties.Keys.ToArray());
            info.AddValue(EXTENDED_PROPERTIES_VALUES_KEY, properties.Values.ToArray());

		    info.AddValue(BREADCRUMBS_KEY, _breadcrumbs);
        }

        private static IEnumerable<Exception> GetAllExceptions(Exception ex)
        {
            while (ex != null)
            {
                yield return ex;
                ex = ex.InnerException;
            }
        }
    

        /// <summary>
        /// Returns a dictionary with all the custom properties and any values in <see cref="ExtendedProperties"/>.
        /// </summary>
        /// <returns>A <see cref="Dictionary{TKey,TValue}"/> where the key, of type <see cref="string"/>, is the name of
        ///  the property and the value, of type <see cref="object"/>, is the property value.</returns>
        /// <remarks>this is a reflection based call and it is VERY EXPENSIVE</remarks>
        public Dictionary<string, object> GetCustomAndExtendedProperties()
        {
            var properties = new Dictionary<string, object>(_extendedProperties);

            foreach (var prop in GetProperties())
            {
                object value = null;

                try
                {
                    value = prop.GetValue(this);
                }
                catch { }

                if (value != null)
                    properties[prop.Name] = value;
            }

            return properties;
        }

        private IEnumerable<PropertyInfo> GetProperties()
		{
			return GetType()
				.GetProperties()
				.Select(p => p.DeclaringType.GetProperty(p.Name))
				.Where(p => p.DeclaringType != typeof(Exception) && p.DeclaringType != typeof(SerializableException) && p.CanWrite);
		}


	    public override string Message =>
            base.Message
            + GetCustomAndExtendedProperties().Select(_ => new KeyValuePair<string, string>(_.Key, _.Value.ToString()))
                                              .Concat((EncryptedTags?.Keys ?? Enumerable.Empty<string>()).Select(_ => new KeyValuePair<string, string>(_, "<encrypted>")))
                                              .Concat(UnencryptedTags ?? Enumerable.Empty<KeyValuePair<string, string>>())
                                              .Aggregate(new StringBuilder(), (sb, pair) => sb.Append($"{(sb.Length == 0 ? "; " : ", ")}{pair.Key}={pair.Value}"));

	    public string RawMessage => base.Message;

	    internal void AddBreadcrumb(Breadcrumb breadcrumb)
	    {
	        _breadcrumbs = _breadcrumbs.Concat(new[] { breadcrumb }).ToArray();
	    }
	}

	[Serializable]
	public class Tags : Dictionary<string, string>
	{
		public Tags()
		{
			
		}

		protected Tags(SerializationInfo info, StreamingContext context):base(info,context)
		{
			
		}
	}
}
