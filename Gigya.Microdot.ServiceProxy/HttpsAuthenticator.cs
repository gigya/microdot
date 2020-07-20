using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.SharedLogic.Security;

namespace Gigya.Microdot.ServiceProxy
{
    public interface IHttpsAuthenticator
    {
        void AddHttpMessageHandlerAuthentication(HttpClientHandler clientHandler, HttpClientConfiguration configuration);
    }

    public class HttpsAuthenticator : IHttpsAuthenticator
    {
        private ILog Log { get; }
        private ICertificateLocator CertificateLocator { get; }

        public HttpsAuthenticator(ILog log, ICertificateLocator certificateLocator)
        {
            Log = log;
            CertificateLocator = certificateLocator;
        }

        public void AddHttpMessageHandlerAuthentication(HttpClientHandler clientHandler, HttpClientConfiguration configuration)
        {
            X509Certificate2 clientCert = null;

            if (configuration.SupplyClientCertificate || 
                configuration.VerificationMode.HasFlag(ServerCertificateVerificationMode.VerifyIdenticalRootCertificate))
            {
                clientCert = CertificateLocator.GetCertificate("Client");
                if (configuration.SupplyClientCertificate)
                {
                    clientHandler.ClientCertificates.Add(clientCert);
                }
            }


            clientHandler.ServerCertificateCustomValidationCallback = (sender, serverCertificate, serverChain, errors) =>
            {
                //This is the case we intentionally ignore SSL errors, should only be used as an hotfix to prevent production downtime 
                if (configuration.VerificationMode == ServerCertificateVerificationMode.Disable)
                {
                    return true;
                }
                switch (errors)
                {
                    case SslPolicyErrors.RemoteCertificateNotAvailable:
                        Log.Error("Remote certificate not available.");
                        return false;
                    case SslPolicyErrors.RemoteCertificateChainErrors:
                        Log.Error(log =>
                        {
                            var sb = new StringBuilder("Certificate error/s.");
                            foreach (var chainStatus in serverChain.ChainStatus)
                            {
                                sb.AppendFormat("Status {0}, status information {1}\n", chainStatus.Status, chainStatus.StatusInformation);
                            }
                            log(sb.ToString());
                        });
                        return false;
                    case SslPolicyErrors.RemoteCertificateNameMismatch: 
                        //We are now using wildcard certificates and expected cert name to match host name e.g. foo.gigya.net matches *.gigya.net
                        if (configuration.VerificationMode.HasFlag(ServerCertificateVerificationMode
                            .VerifyDomain))
                        {
                            Log.Error(_ => _("Server certificate name does not match host name"));
                            return false;
                        }
                        return AdditionalVerifications(serverCertificate, serverChain);
                    case SslPolicyErrors.None:
                        return AdditionalVerifications(serverCertificate, serverChain);
                        
                    default:
                        throw new ArgumentOutOfRangeException(nameof(errors), errors, "The supplied value of SslPolicyErrors is invalid.");
                }
            };

            bool AdditionalVerifications(X509Certificate2 serverCertificate, X509Chain serverChain)
            {
                if (configuration.VerificationMode.HasFlag(ServerCertificateVerificationMode
                    .VerifyIdenticalRootCertificate))
                {
                    var clientRootCertHash = clientCert.GetHashOfRootCertificate();
                    bool hasSameRootCertificateHash = serverChain.HasSameRootCertificateHash(clientRootCertHash);

                    if (hasSameRootCertificateHash == false)
                        Log.Error(_ => _("Server root certificate does not match client root certificate"));

                    if (hasSameRootCertificateHash == false)
                        return false;
                }

                if (configuration.SecurityRole != null)
                {
                    var name = ((X509Certificate2)serverCertificate).GetNameInfo(X509NameType.SimpleName, false);

                    if (name == null || !name.Contains(configuration.SecurityRole))
                    {
                        Log.Error(_ => _("Server certificate doesn't contain required security role"));
                        return false;
                    }
                }

                return true;
            }
        }
    }

    //public interface IHttpMessageHandlerFactory
    //{
    //    HttpMessageHandler CreateMessageHandler(bool isHttps, string securityRole);
    //}

    //public class HttpMessageHandlerFactory : IHttpMessageHandlerFactory
    //{
    //    private readonly IHttpsAuthenticator _httpsAuthenticator;

    //    public HttpMessageHandlerFactory(IHttpsAuthenticator httpsAuthenticator)
    //    {
    //        _httpsAuthenticator = httpsAuthenticator;
    //    }

    //    public HttpMessageHandler CreateMessageHandler(bool isHttps, string securityRole)
    //    {
    //        var handler = new HttpClientHandler();
    //        if (isHttps)
    //            _httpsAuthenticator.AddHttpMessageHandlerAuthentication(handler, securityRole);
    //        return handler;

    //    }
    //}
}
