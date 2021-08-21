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

            //var req = new CertificateRequest("cn=foobar", ecdsa, HashAlgorithmName.SHA256);
            //return req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));

            return CreateSelfSignedRequest("cn=foobar", ecdsa, HashAlgorithmName.SHA256);
        }

        private X509Certificate2 CreateSelfSignedRequest(string subjectName, ECDsa key, HashAlgorithmName hashAlgorithm)
        {
            Type certificateRequestType = Type.GetType("System.Security.Cryptography.X509Certificates.CertificateRequest");

            object request = certificateRequestType?.GetConstructor(new[] { subjectName.GetType(), key.GetType(), hashAlgorithm.GetType() })
                ?.Invoke(new object[] { subjectName, key, hashAlgorithm });

            return (X509Certificate2)certificateRequestType?.GetMethod("CreateSelfSigned")?.Invoke(request, new object[] { DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5) });
        }
    }
}
