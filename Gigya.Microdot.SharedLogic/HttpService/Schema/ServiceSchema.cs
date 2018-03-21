using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace Gigya.Microdot.SharedLogic.HttpService.Schema
{
    /// <summary>
    /// Contains a collection of interfaces, methods and method parameters, along with their attributes. Parameter types
    /// and attributes are both weakly and strongly deserialized, so clients can convenienetly work with real objects if
    /// they reference the needed assemblies, or work against strings/JObjects if they don't.
    /// </summary>
    public class ServiceSchema
    {
        public InterfaceSchema[] Interfaces { get; set; }

        public ServiceSchema() { }

        public ServiceSchema(Type[] interfaces)
        {
            Interfaces = interfaces.Select(_ => new InterfaceSchema(_)).ToArray();
            SetHashCode();
        }

        public string Hash { get; set; }

        private void SetHashCode()
        {
            var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream) { AutoFlush = true })
            using (SHA1 sha = new SHA1CryptoServiceProvider())
            {
                JsonSerializer.Create().Serialize(writer, this);
                stream.Seek(0, SeekOrigin.Begin);
                Hash = Convert.ToBase64String(sha.ComputeHash(stream));
            }
        }

        public MethodSchema TryFindMethod(InvocationTarget target)
        {
            return Interfaces
                .SingleOrDefault(i => i.Name == target.TypeName)
                ?.Methods
                .SingleOrDefault(m => m.Name == target.MethodName);
        }
    }
}