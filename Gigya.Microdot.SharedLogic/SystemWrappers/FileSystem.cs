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
    }
}