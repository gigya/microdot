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
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.HttpService;
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
		private CurrentApplicationInfo AppInfo { get; }

		public WindowsStoreCertificateLocator(Func<HttpsConfiguration> httpsConfigurationFactory, CurrentApplicationInfo appInfo)
		{
			HttpsConfigurationFactory = httpsConfigurationFactory;
			AppInfo = appInfo;
		}

		public X509Certificate2 GetCertificate(string certName)
		{
			var config = HttpsConfigurationFactory();
			if (!config.Certificates.TryGetValue(certName, out CertificateConfig certificateConfig))
			{
				throw new ConfigurationException($"No certificate configuration is found for {certName}" );
			}

			string certPath = certificateConfig.CertificatePath;
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

			errorPrefix += " and process runs under user '" + AppInfo.OsUser + "'";
			GAssert.IsTrue(foundCert != null, errorPrefix + ", but certificate was not found.");
			GAssert.IsTrue(foundCert.HasPrivateKey, errorPrefix + ", but certificate does not contain a private key.");
			return foundCert;
		}

	}
}