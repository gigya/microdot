using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Gigya.Microdot.Interfaces.Logging;

namespace Gigya.Microdot.SharedLogic
{
    /// <summary>
    /// Provides a list of assemblies that should be used for discovery via reflection.
    /// </summary>
    public interface IAssemblyProvider
    {
        /// <summary>
        /// Get a list of assemblies hat should be used for discovery via reflection.
        /// </summary>
        /// <returns></returns>
        Assembly[] GetAssemblies();


        Type[] GetAllTypes();
    }


    /// <summary>
    /// Provides a list of assemblies that should be used for discovery via reflection. Loads all assemblies in the
    /// current directory, except blacklisted assemblies.
    /// </summary>
    public class AssemblyProvider : IAssemblyProvider
    {
        private Assembly[] Assemblies { get; set; }
        private Type[] AllTypes { get; set; }
        private ILog Log { get; }


        /// <summary>
        /// Initializes a new <see cref="AssemblyProvider"/> that uses an assembly scanning blacklist from the provided
        /// <see cref="BaseCommonConfig"/>.
        /// </summary>
        /// <param name="commonConfig"></param>
        public AssemblyProvider(IApplicationDirectoryProvider directoryProvider, BaseCommonConfig commonConfig, ILog log)
        {
            Log = log;
            var filesToScan = Directory.GetFiles(directoryProvider.GetApplicationDirectory(), "*.dll")
                                       .Where(p => (commonConfig.AssemblyScanningBlacklist ?? new string[0])
                                       .Contains(Path.GetFileName(p)) == false);

            foreach (string assemblyPath in filesToScan)
            {
                try
                {
                    Assembly.LoadFrom(assemblyPath);
                }
                catch (FileNotFoundException) { }
                catch (FileLoadException) { }
                catch (BadImageFormatException) { }
                catch (ReflectionTypeLoadException) { }
            }

            Assemblies = AppDomain.CurrentDomain
                  .GetAssemblies()
                  .Where(a => a.IsDynamic == false)
                  .ToArray();
        }


        public Assembly[] GetAssemblies()
        {
            return Assemblies;
        }


        public Type[] GetAllTypes()
        {
            if (AllTypes != null)
                return AllTypes;

            AllTypes = GetAssemblies().SelectMany(GetTypes).ToArray();

            return AllTypes;
        }

        private IEnumerable<Type> GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetExportedTypes();
            }
            catch (Exception ex)
            {
                if (ex is TypeLoadException || ex is FileNotFoundException)
                {
                    Log.Warn(_ => _("Failed to retrieve the exported types from a specific assembly, " +
                        "probably due to a missing optional dependency. Skipping assembly. " +
                        "See tags and exceptions for details.",
                        exception: ex,
                        unencryptedTags: new { assemblyName = assembly.GetName().Name }));

                    return Enumerable.Empty<Type>();
                }

                throw new TypeLoadException($"Failed to load types from assembly \"{assembly.FullName}\"", ex);
            }
        }
    }
}
