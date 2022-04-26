using Gigya.Microdot.Interfaces.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Gigya.Microdot.Testing.Shared.Utils
{
    [Serializable]
    [ConfigurationRoot("ServiceTester", RootStrategy.ReplaceClassNameWithPath)]
    public class ServiceTesterConfig : IConfigObject
    {
        public bool ShouldHandleHttpsConnection { get; set; } = true;

        public string HttpsConnectionCertHash { get; set; } = "33ccad5538d93c2af131a3281ba5539ae80e0483";
    }
}
