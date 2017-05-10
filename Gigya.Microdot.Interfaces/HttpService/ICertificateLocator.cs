using System.Security.Cryptography.X509Certificates;

namespace Gigya.Microdot.Interfaces.HttpService
{
    public interface ICertificateLocator
    {
        X509Certificate2 GetCertificate(string certName);
    }
}