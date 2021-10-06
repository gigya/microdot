﻿using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic;
using System;
using System.IO;

namespace Gigya.Microdot.UnitTests
{
    internal class NullEnvironment : IEnvironment
    {
        public string this[string key] => throw new NotImplementedException();

        public string Zone => nameof(Zone);
        public string Region => nameof(Region);
        public string DeploymentEnvironment => nameof(DeploymentEnvironment);
        public string ConsulAddress => nameof(ConsulAddress);
        public string InstanceName => nameof(InstanceName);
        public DirectoryInfo ConfigRoot => new DirectoryInfo(Directory.GetCurrentDirectory());
        public FileInfo LoadPathsFile => new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), "loadPaths.json"));

        public CurrentApplicationInfo ApplicationInfo => throw new NotImplementedException();

        [Obsolete("To be deleted on version 2.0")]
        public string GetEnvironmentVariable(string name) => name;
        [Obsolete("To be deleted on version 2.0")]
        public void SetEnvironmentVariableForProcess(string name, string value) {}
    }
}
