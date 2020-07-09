using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Gigya.Microdot.SharedLogic.HttpService
{
    public struct HttpClientConfiguration
    {
        public HttpClientConfiguration(
            bool useHttps,
            string securityRole,
            TimeSpan? timeout,
            ServerCertificateVerificationMode verificationMode,
            bool supplyClientCertificate)
        {
            UseHttps = useHttps;
            SecurityRole = securityRole;
            Timeout = timeout;
            VerificationMode = verificationMode;
            SupplyClientCertificate = supplyClientCertificate;
        }

        /// <summary>
        /// Indicates whether or not the client should supply a certificate to the server
        /// </summary>
        public bool SupplyClientCertificate { get; set; }

        /// <summary>
        /// Controls whether an https client should be constructed or not
        /// </summary>
        public bool UseHttps { get; }
        /// <summary>
        /// The security role that should be used for this connection
        /// </summary>
        public string SecurityRole { get; }
        /// <summary>
        /// The http/s request timeout for the connection
        /// </summary>
        public TimeSpan? Timeout { get; }

        /// <summary>
        /// Controls which verification rules will be applied to the client certificate in case this is a secured connection
        /// </summary>
        public ServerCertificateVerificationMode VerificationMode { get; }
    }

    [Flags]
    [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum ServerCertificateVerificationMode
    {
        Disable = 0, //don't supply client certificate, ignore all errors
        VerifyDomain = 1, //verify certificate domain match
        VerifyIdenticalRootCertificate = 2//verify that the client and server certificate both have the same root 
    }
    
    [Flags]
    [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum ClientCertificateVerificationMode
    {
        Disable = 0, // don't read client certificate hence no validation
        VerifyIdenticalRootCertificate = 1//verify that the client and server certificate both have the same root 
    }
}
