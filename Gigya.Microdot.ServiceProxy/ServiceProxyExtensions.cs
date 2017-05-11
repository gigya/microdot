using System;
using Gigya.Common.Contracts.HttpService;


namespace Gigya.Microdot.ServiceProxy
{
    public static class ServiceProxyExtensions
    {
        public static string GetServiceName(this Type serviceInterfaceType)
        {
            var attribute = (HttpServiceAttribute)Attribute.GetCustomAttribute(serviceInterfaceType, typeof(HttpServiceAttribute));
            if (attribute?.Name != null)
                return attribute.Name;

            var assemblyName = serviceInterfaceType.Assembly.GetName().Name;
            var endIndex = assemblyName.IndexOf(".Interface", StringComparison.OrdinalIgnoreCase);
            if (endIndex <= 0)
                return serviceInterfaceType.FullName.Replace('+', '-');

            var startIndex = assemblyName.Substring(0, endIndex).LastIndexOf(".", StringComparison.OrdinalIgnoreCase) + 1;
            var length = endIndex - startIndex;
            return assemblyName.Substring(startIndex, length);
        }
    }
}
