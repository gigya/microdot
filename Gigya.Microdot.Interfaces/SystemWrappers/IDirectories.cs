using System.Collections.Generic;
using System.Threading.Tasks;

namespace Gigya.Microdot.Interfaces.SystemWrappers
{
    public interface IFileSystem
    {
        bool Exists(string dirName);

        string ReadAllTextFromFile(string filePath);

        string TryReadAllTextFromFile(string filePath);

        IEnumerable<string> GetFilesInFolder(string folderPath, string searchPatern = null);

        Task<IEnumerable<string>> GetFilesInFolderAsync(string folderPath, string searchPatern = null);

        Task<string> ReadAllTextFromFileAsync(string filePath);
    }
}