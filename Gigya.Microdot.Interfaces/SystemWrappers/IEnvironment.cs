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

using Gigya.Microdot.SharedLogic;
using System;
using System.IO;

namespace Gigya.Microdot.Interfaces.SystemWrappers
{
    // TODO: Remove in favor of property in app instance
    public interface IEnvironment
    {
        string this[string key] { get; }

        /// <summary>
        /// The current Region this application runs in, e.g. "us1", "eu2".
        /// Initialized from the environment variable "REGION".
        /// </summary>
        string Region { get; }

        /// <summary>
        /// The current Zone this application runs in, e.g. "us1a" or "eu2c". Initialized from the environment variable "ZONE".
        /// </summary>
        string Zone { get; }

        /// <summary>
        /// The current environment this application runs in, e.g. "prod", "st1" or "canary". Initialized from the environment variable "ENV".
        /// </summary>        
        string DeploymentEnvironment { get; }

        // TODO: Abstract away
        string ConsulAddress { get; }

        /// <summary>
        /// Logical instance name for the current application, which can be used to differentiate between
        /// multiple identical applications running on the same host.
        /// </summary>
        string InstanceName { get; }
        
        /// <summary>
        /// The configuration root directory.
        /// </summary>
        DirectoryInfo ConfigRoot { get; }

        /// <summary>
        /// Gets the current application information.
        /// </summary>
        CurrentApplicationInfo ApplicationInfo { get; }

        /// <summary>
        /// The load paths file.
        /// </summary>
        FileInfo LoadPathsFile { get; }
    }
}