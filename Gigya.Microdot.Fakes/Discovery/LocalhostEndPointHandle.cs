using System;

using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.SharedLogic;

namespace Gigya.Microdot.Fakes.Discovery
{
    public class LocalhostEndPointHandle : IEndPointHandle
    {
        public string HostName => CurrentApplicationInfo.HostName;
        public int? Port { get; set; }
        public bool? UseHttps { get; set; }
        public string SecurityRole { get; set; }
        public bool ReportFailure(Exception ex = null) { return false; }
        public void ReportSuccess() { }
    }
}