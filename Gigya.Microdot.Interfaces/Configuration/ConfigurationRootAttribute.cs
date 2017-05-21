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

namespace Gigya.Microdot.Interfaces.Configuration
{
    public enum RootStrategy
    {
        AppendClassNameToPath,
        ReplaceClassNameWithPath
    }

    /// <summary>
    /// Should be placed on configuration objects if you want to control a path in configuration where object is created from,
    /// the path property is path, the second argument determines eather the path should be appended to class name or it should replace it entirely.  
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigurationRootAttribute:Attribute
    {
        /// <param name="path">Configuration path.</param>
        /// <param name="buildingStrategy">Determines the strategy for usage of path should it be appended to class name as prefix or should it replace the class name.</param>
        public ConfigurationRootAttribute(string path, RootStrategy buildingStrategy)
        {
            if(string.IsNullOrEmpty(path))
            {
                throw new ArgumentOutOfRangeException(nameof(path));
            }
            BuildingStrategy = buildingStrategy;
            Path = path;
        }
        public RootStrategy BuildingStrategy { get; }
        public string Path { get; }
    }
}