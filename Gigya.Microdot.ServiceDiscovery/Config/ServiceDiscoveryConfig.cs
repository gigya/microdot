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
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.SharedLogic.HttpService;

namespace Gigya.Microdot.ServiceDiscovery.Config
{
    /// <summary>
    /// The discovery configuration for a specific service.
    /// </summary>
    [Serializable]
    public class ServiceDiscoveryConfig
    {
        /// <summary>
        /// Scope where this service is installed.
        /// Some services are installed for current environment only (itg1, prod, etc.)
        /// Other services are installed for entire data-center (e.g. Kafka, Flume, etc.)
        /// </summary>
        public ServiceScope? Scope { get; set; }

        /// <summary>
        /// The time-out trying to send requests to the service.
        /// </summary>
        public TimeSpan? RequestTimeout { get; set; }

        /// <summary>
        /// When we lose connection to some endpoint, we wait this delay till we start trying to reconnect.
        /// </summary>
        [Obsolete("To be deleted after discovery refactoring")]
        public double? FirstAttemptDelaySeconds { get; set; }

        /// <summary>
        /// When retrying to reconnect to an endpoint, we use exponential backoff (e.g. 1,2,4,8ms, etc). Once that
        /// backoff reaches this value, it won't increase any more.
        /// </summary>
        [Obsolete("To be deleted after discovery refactoring")]
        public double? MaxAttemptDelaySeconds { get; set; }

        /// <summary>
        /// The factor of the exponential backoff when retrying connections to endpoints.
        /// </summary>
        [Obsolete("To be deleted after discovery refactoring")]
        public double? DelayMultiplier { get; set; }

        /// <summary>
        /// The discovery mode to use, e.g. whether to use DNS resolving, Consul, etc.
        /// </summary>
        public string Source { get; set; }

        public string Hosts { get; set; }

        /// <summary>
        /// Port of service, to be used in case port is not set by any other method, like Consul or service attribute
        /// </summary>
        public int? DefaultPort { get; set; }

        public int? DefaultSlotNumber { get; set; }

        /// <summary>
        /// GetObject a value indicating if a secure will be used to connect to the remote service. This defaults to the
        /// value that was specified in the <see cref="HttpServiceAttribute"/> decorating <i>TInterface</i>, and can
        /// be overridden by discovery configuration.
        /// </summary>
        public bool? UseHttpsOverride { get; set; }

        /// <summary>
        /// Indicates whether the service should listen for HTTPs traffic
        /// </summary>
        public bool? ServiceHttpsOverride { get; set; } = null;

        /// <summary>
        /// Indicates whether the client should try and elevate to HTTPs traffic even if not explicitly configured to 
        /// </summary>
        public bool? TryHttps { get; set; }

        /// <summary>
        /// The frequency in which the service proxy will try to send an HTTPS request in minutes
        /// </summary>
        public int? TryHttpsIntervalInMinutes { get; set; }

        /// <summary>
        /// Controls the client verification logic for the server certificate.
        /// Defaults to null, will override the global settings for this service if set to anything but null.
        /// </summary>
        public ServerCertificateVerificationMode? ServerCertificateVerification { get; set; } = null;

        /// /// <summary>
        /// Controls the verification logic of the client certificate.
        /// Defaults to null, will override the global settings for this service if set to anything but null.
        /// </summary>
        public ClientCertificateVerificationMode? ClientCertificateVerification { get; set; } = null;

        /// <summary>
        /// Gets or sets the name of server certificate to trust. Defaults to null, which means it will trust a
        /// certificate with any name (but still checks its Certificate Authority).
        /// </summary>
        public string SecurityRole { get; set; }

        public CachingPolicyConfig CachingPolicy { get; set; }

        /// <summary>
        /// If the service was not in use for this amount of time, always report it as healthy.
        /// </summary>
        public TimeSpan SuppressHealthCheckAfterServiceUnused { get; set; } = TimeSpan.FromMinutes(15);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;

            var other = (ServiceDiscoveryConfig)obj;
            return Scope == other.Scope &&
                   RequestTimeout.Equals(other.RequestTimeout) &&
                   FirstAttemptDelaySeconds.Equals(other.FirstAttemptDelaySeconds) &&
                   MaxAttemptDelaySeconds.Equals(other.MaxAttemptDelaySeconds) &&
                   DelayMultiplier.Equals(other.DelayMultiplier) &&
                   Source == other.Source &&
                   string.Equals(Hosts, other.Hosts) &&
                   DefaultPort == other.DefaultPort &&
                   DefaultSlotNumber == other.DefaultSlotNumber &&
                   UseHttpsOverride == other.UseHttpsOverride &&
                   ClientCertificateVerification == other.ClientCertificateVerification &&
                   ServerCertificateVerification == other.ServerCertificateVerification &&
                   string.Equals(SecurityRole, other.SecurityRole) &&
                   Equals(CachingPolicy, other.CachingPolicy) &&
                   SuppressHealthCheckAfterServiceUnused.Equals(other.SuppressHealthCheckAfterServiceUnused);
        }


        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ RequestTimeout.GetHashCode();
                hashCode = (hashCode * 397) ^ FirstAttemptDelaySeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxAttemptDelaySeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ DelayMultiplier.GetHashCode();
                hashCode = (hashCode * 397) ^ Source.GetHashCode();                
                hashCode = (hashCode * 397) ^ (Hosts != null ? Hosts.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ DefaultPort.GetHashCode();
                hashCode = (hashCode * 397) ^ DefaultSlotNumber.GetHashCode();
                hashCode = (hashCode * 397) ^ UseHttpsOverride.GetHashCode();
                hashCode = (hashCode * 397) ^ (SecurityRole != null ? SecurityRole.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (CachingPolicy != null ? CachingPolicy.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ SuppressHealthCheckAfterServiceUnused.GetHashCode();
                hashCode = (hashCode * 397) ^ ServerCertificateVerification.GetHashCode();
                hashCode = (hashCode * 397) ^ ClientCertificateVerification.GetHashCode();
                return hashCode;
            }
        }

    }
}