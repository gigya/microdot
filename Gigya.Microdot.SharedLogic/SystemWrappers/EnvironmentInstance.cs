using System;

using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.SharedLogic.SystemWrappers
{
    public class EnvironmentInstance : IEnvironment
    {
        public EnvironmentInstance()
        {
            PlatformID = Environment.OSVersion.Platform;
            PlatformSpecificPathPrefix = PlatformID == PlatformID.Unix ? "/etc" : @"D:";
        }

        public PlatformID PlatformID { get; }

        public string PlatformSpecificPathPrefix { get; }

        public void SetEnvironmentVariableForProcess(string name, string value)
        {
            Environment.SetEnvironmentVariable(name, value.ToLower(), EnvironmentVariableTarget.Process);
        }

        public string GetEnvironmentVariable(string name) { return Environment.GetEnvironmentVariable(name)?.ToLower(); }
    }
}