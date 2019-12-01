using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Gigya.Microdot.SharedLogic.HttpService;

namespace Gigya.Microdot.Common.Tests
{
    public class DummyCertificateLocator : ICertificateLocator
    {
        public X509Certificate2 GetCertificate(string certName)
        {
            var ecdsa = ECDsa.Create(); // generate asymmetric key pair
            var req = new CertificateRequest("cn=foobar", ecdsa, HashAlgorithmName.SHA256);
            return req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));
        }
    }
}
