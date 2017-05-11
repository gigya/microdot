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
        public string EndpointName { get; }

        /// <summary>
        /// Whether Gator should reject requests from the outside world that were passed over http and not https, and
        /// not forward them to the service.
        /// </summary>
        public bool RequireHTTPS { get; } = true;

        /// <summary>
        /// Defines how to map the response from this method to the response returned by Gator. If not specified, this
        /// method's response will be returned as-is to the outside world, along with Gigya's standard response fields
        /// (statusCode, errorCode, statusReason, callId, etc.), unless your response already includes them, or some of
        /// them. If you do specify a name, all of your response will be put under that json property name, and the
        /// standard response fields will be next to it.
        /// </summary>
        public string PropertyNameForResponseBody { get; } = null;


        /// <param name="endpointName">Full endpoint name (e.g. "accounts.getPolicies")</param>
        /// <param name="requireHTTPS">
        /// Whether Gator should reject requests from the outside world that were passed over http and not https, and
        /// not forward them to the service.
        ///</param>
        /// <param name="propertyNameForResponseBody">
        /// Defines how to map the response from this method to the response returned by Gator. If not specified, this
        /// method's response will be returned as-is to the outside world, along with Gigya's standard response fields
        /// (statusCode, errorCode, statusReason, callId, etc.), unless your response already includes them, or some of
        /// them. If you do specify a name, all of your response will be put under that json property name, and the
        /// standard response fields will be next to it.
        /// </param>
        public PublicEndpointAttribute(string endpointName, bool requireHTTPS = true, string propertyNameForResponseBody = null)
        {
            EndpointName = endpointName;
            RequireHTTPS = requireHTTPS;
            PropertyNameForResponseBody = propertyNameForResponseBody;
        }
    }
}
