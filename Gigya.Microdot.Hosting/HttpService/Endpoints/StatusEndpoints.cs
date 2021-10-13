using System;
using System.Net;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces.Logging;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints
{
    public class StatusEndpoints: ICustomEndpoint
    {
        private readonly Func<MicrodotHostingConfig> _microdotHostingConfigFactory;
        private readonly ILog _logger;

        public StatusEndpoints(Func<MicrodotHostingConfig> microdotHostingConfigFactory, ILog logger)
        {
            _microdotHostingConfigFactory = microdotHostingConfigFactory;
            _logger = logger;
        }
        
        public async Task<bool> TryHandle(HttpListenerContext context, WriteResponseDelegate writeResponse)
        {
            var microdotHostingConfig = _microdotHostingConfigFactory();
            
            foreach (var statusEndpoint in microdotHostingConfig.StatusEndpoints)
            {
                if (context.Request.Url.AbsolutePath.EndsWith(statusEndpoint))
                {
                    if (microdotHostingConfig.ShouldLogStatusEndpoint)
                    {
                        _logger.Info(log =>
                        {
                            log("Status", unencryptedTags: new Tags
                            {
                                { "RemoteIP", context?.Request?.RemoteEndPoint?.Address?.ToString() ?? "0" },
                                { "RemotePort", context?.Request?.RemoteEndPoint?.Port.ToString() }
                            });
                        });
                    }

                    await writeResponse("").ConfigureAwait(false);
                    return true;
                }
            }

            return false;
        }
    }
}