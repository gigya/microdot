using System;
using System.Reflection;

namespace Gigya.Microdot.Interfaces
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
}