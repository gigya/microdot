using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.HttpService;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.Utils;

namespace Gigya.Microdot.SharedLogic.Security
{

    public class CertificateConfig
    {
        [Required]
        public string CertificatePath { get; set; }
    }

    [ConfigurationRoot("Https",RootStrategy.ReplaceClassNameWithPath)]
    public class HttpsConfiguration : IConfigObject
    {
        public Dictionary<string, CertificateConfig>  Certificates { get; set; }    
    }

	public class WindowsStoreCertificateLocator : ICertificateLocator
	{
	    private Func<HttpsConfiguration> HttpsConfigurationFactory { get; }


	    public WindowsStoreCertificateLocator(Func<HttpsConfiguration> httpsConfigurationFactory)
		{
		    HttpsConfigurationFactory = httpsConfigurationFactory;		   
		}


		public X509Certificate2 GetCertificate(string certName)
		{
		    var config = HttpsConfigurationFactory();
		    CertificateConfig certificateConfigconfig;
		    if(!config.Certificates.TryGetValue(certName, out certificateConfigconfig))
		    {
		        throw new ConfigurationException($"No configuration is found for "+ certName);
		    }

		    string certPath = certificateConfigconfig.CertificatePath;
            string errorPrefix = $"Config entry '{certName}.CertificatePath' specifies '{certPath}'";
			var parts = certPath.Split('\\');
			var storeLocation = StoreLocation.CurrentUser;
			var storeName = StoreName.My;

			GAssert.IsTrue(parts.Length == 3 && Enum.TryParse(parts[0], true, out storeLocation) && Enum.TryParse(parts[1], true, out storeName),
				string.Format("{0}; invalid format; expecting <{1}>\\<{2}>\\<certificate subject>",
					errorPrefix, 
                    string.Join("|", Enum.GetNames(typeof(StoreLocation))),string.Join("|", Enum.GetNames(typeof(StoreName)))
                    ));

			var store = new X509Store(storeName, storeLocation);
			store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);
			var certs = store.Certificates.Find(X509FindType.FindBySubjectName, parts[2], false);

            var foundCert = certs.Cast<X509Certificate2>().FirstOrDefault(cer => cer.GetNameInfo(X509NameType.SimpleName, false) == parts[2]);

			errorPrefix += " and process runs under user '" + CurrentApplicationInfo.OsUser + "'";
			GAssert.IsTrue(foundCert != null, errorPrefix + ", but certificate was not found.");
			GAssert.IsTrue(foundCert.HasPrivateKey, errorPrefix + ", but certificate does not contain a private key.");
			return foundCert;
		}

	}
}