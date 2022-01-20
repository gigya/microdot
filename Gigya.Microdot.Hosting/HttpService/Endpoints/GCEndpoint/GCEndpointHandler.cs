using System;
using System.Collections.Specialized;
using System.Net;
using System.Threading.Tasks;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces.Logging;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints.GCEndpoint
{
    public interface IGCEndpointHandler
    {
        Task<GCHandlingResult> Handle(Uri url, NameValueCollection queryString, IPAddress ipAddress);
    }
    
    public class GCEndpointHandler : IGCEndpointHandler
    {
        private readonly Func<MicrodotHostingConfig> _microdotHostingConfigFactory;
        private readonly ILog _logger;
        private readonly IGCEndpointHandlerUtils _gcEndpointHandlerUtils;

        public GCEndpointHandler(Func<MicrodotHostingConfig> microdotHostingConfigFactory, 
            ILog logger, 
            IGCEndpointHandlerUtils gcEndpointHandlerUtils)
        {
            _microdotHostingConfigFactory = microdotHostingConfigFactory;
            _logger = logger;
            _gcEndpointHandlerUtils = gcEndpointHandlerUtils;
        }
        
        public async Task<GCHandlingResult> Handle(Uri url, NameValueCollection queryString, IPAddress ipAddress)
        {
            if (url.AbsolutePath != "/force-traffic-affecting-gc")
                return new GCHandlingResult(false);

            var config = _microdotHostingConfigFactory();

            if (config.GCEndpointEnabled)
            {
                
                if (_gcEndpointHandlerUtils.TryProcessAsTokenGenerationRequest(queryString, ipAddress, out var additionalInfo))
                    return new GCHandlingResult(true, additionalInfo);

                if (false == _gcEndpointHandlerUtils.ValidateToken(queryString, out additionalInfo))
                    return new GCHandlingResult(true, additionalInfo);

                if (false == _gcEndpointHandlerUtils.ValidateGcType(queryString, out additionalInfo, out var gcType))
                    return new GCHandlingResult(true, additionalInfo);

                var gcCollectionResult = _gcEndpointHandlerUtils.Collect(gcType);

                _logger.Warn(log=>log("GC endpoint was called",unencryptedTags:new
                {
                    GcType = gcType,
                    TotalMemoryAfterGC = gcCollectionResult.TotalMemoryAfterGC,
                    TotalMemoryBeforeGC = gcCollectionResult.TotalMemoryBeforeGC,
                    GCDuration = gcCollectionResult.ElapsedMilliseconds,
                    IPAddress = ipAddress.ToString()
                }));

                return new GCHandlingResult(
                    successful:true, 
                    message:"GC ran successfully",
                    gcCollectionResult: gcCollectionResult);
                
            }
            else
            {
                return new GCHandlingResult(false);
            }
        }
    }
}