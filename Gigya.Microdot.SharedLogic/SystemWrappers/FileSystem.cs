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
using System.IO;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.SharedLogic.SystemWrappers
{
    public class FileSystem : IFileSystem
    {
        public bool Exists(string dirName)
        {
            return dirName != null && Directory.Exists(dirName);
        }


        public string ReadAllTextFromFile(string filePath)
        {
            return !File.Exists(filePath) ? null:File.ReadAllText(filePath);
        }


        public string TryReadAllTextFromFile(string filePath)
        {
            try
            {
                return ReadAllTextFromFile(filePath);
            }
            catch
            {
                return null;
            }
        }


        public IEnumerable<string> GetFilesInFolder(string folderPath, string searchPatern = "*")
        {
            return Directory.GetFiles(folderPath, searchPatern);
        }

        public async Task<IEnumerable<string>> GetFilesInFolderAsync(string folderPath, string searchPatern = null)
        {
            return await Task.Run(() => Directory.GetFiles(folderPath, searchPatern)); 
        }

        public async Task<string> ReadAllTextFromFileAsync(string filePath)
        {
            using (var streamer = File.OpenText(filePath))
                return await streamer.ReadToEndAsync().ConfigureAwait(false);
        }

        public Task<DateTime> GetFileLastModified(string filePath)
        {
            return Task.FromResult(File.GetLastWriteTimeUtc(filePath)); // no async equivalent
        }
    }
}