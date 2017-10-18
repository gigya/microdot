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

namespace Gigya.Common.Contracts.HttpService
{

    /// <summary>
    /// Specifies that this method is exposed to the external world, and can be called via Gator.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class PublicEndpointAttribute : Attribute
    {

        /// <summary>
        /// Full endpoint name (e.g. "accounts.getPolicies")
        /// </summary>
        public string EndpointName { get; set; }

        /// <summary>
        /// Whether Gator should reject requests from the outside world that were passed over http and not https, and
        /// not forward them to the service.
        /// </summary>
        public bool RequireHTTPS { get; set; } = true;

        /// <summary>
        /// Defines how to map the response from this method to the response returned by Gator. If not specified, this
        /// method's response will be returned as-is to the outside world, along with Gigya's standard response fields
        /// (statusCode, errorCode, statusReason, callId, etc.), unless your response already includes them, or some of
        /// them. If you do specify a name, all of your response will be put under that json property name, and the
        /// standard response fields will be next to it.
        /// </summary>
        public string PropertyNameForResponseBody { get; set; }

        /// <summary>
        /// Specifies the routing regex, only matching url paths(exploding domain and query string) will be forwarded.
        /// </summary>
        public string UrlPathRegex { get; set; }

        /// <summary>
        /// True if calls to this method should bypass authentication checks, otherwise false.
        /// </summary>
        public string SkipPermissionChecks { get; set; }

        /// <summary>
        /// Whether method has one single parameter which its fields represent the request parameters
        /// </summary>
        public bool UsingRequestObject { get; set; }
        
        ///  <param name="endpointName">Full endpoint name (e.g. "accounts.getPolicies")</param>
        ///  <param name="requireHTTPS">
        ///  Whether Gator should reject requests from the outside world that were passed over http and not https, and
        ///  not forward them to the service.
        /// </param>
        ///  <param name="propertyNameForResponseBody">
        ///  Defines how to map the response from this method to the response returned by Gator. If not specified, this
        ///  method's response will be returned as-is to the outside world, along with Gigya's standard response fields
        ///  (statusCode, errorCode, statusReason, callId, etc.), unless your response already includes them, or some of
        ///  them. If you do specify a name, all of your response will be put under that json property name, and the
        ///  standard response fields will be next to it.
        ///  </param>
        [Obsolete("Please use the other constructor overload that accepts only an 'endpoint' parameter, and specify all other paramters with the attributes optional named parameter syntax (MyProp = 5)")]
        public PublicEndpointAttribute(string endpointName, bool requireHTTPS = true, string propertyNameForResponseBody = null)
        {
            EndpointName = endpointName;
            RequireHTTPS = requireHTTPS;
            PropertyNameForResponseBody = propertyNameForResponseBody;
        }

        ///  <param name="endpointName">Full endpoint name (e.g. "accounts.getPolicies")</param>
        public PublicEndpointAttribute(string endpointName)
        {
            EndpointName = endpointName;
        }

        internal PublicEndpointAttribute()
        {
            
        }
    }
}
