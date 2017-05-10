using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Gigya.Microdot.SharedLogic.Security
{
    internal static class CertificateHelper
    {
        public static byte[] GetHashOfRootCertificate(this X509Certificate2 certificate)
        {
            X509Chain chain = new X509Chain();
            chain.Build(certificate);
            return chain.ChainElements.Cast<X509ChainElement>().Last().Certificate.GetCertHash();
        }

        public static bool HasSameRootCertificateHash(this X509Certificate2 externalCertificate, byte[] internalRooCertHash)
        {
            X509Chain chain = new X509Chain { ChainPolicy = { RevocationMode = X509RevocationMode.NoCheck } };
            chain.Build(externalCertificate);

            return chain.HasSameRootCertificateHash(internalRooCertHash);            
        }

        public static bool HasSameRootCertificateHash(this X509Chain chain, byte[] internalRooCertHash)
        {
			byte[] externalRootHash = chain.ChainElements.Cast<X509ChainElement>().Last().Certificate.GetCertHash();
            return externalRootHash.SequenceEqual(internalRooCertHash);
        }
    }
}