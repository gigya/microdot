using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints.GCEndpoint
{
    public interface IGCEndpointHandler
    {
        Task<GCHandlingResult> Handle(Uri url, NameValueCollection queryString);
    }
    
    public class GCEndpointHandler : IGCEndpointHandler
    {
        private readonly Func<MicrodotHostingConfig> _microdotHostingConfigFactory;
        private readonly ILog _logger;
        private readonly IGCCollectionRunner _gcCollectionRunner;
        private readonly IGCTokenHandler _gcTokenHandler;

        public GCEndpointHandler(Func<MicrodotHostingConfig> microdotHostingConfigFactory, 
            ILog logger, 
            IGCCollectionRunner gcCollectionRunner, 
            IGCTokenHandler gcTokenHandler)
        {
            _microdotHostingConfigFactory = microdotHostingConfigFactory;
            _logger = logger;
            _gcCollectionRunner = gcCollectionRunner;
            _gcTokenHandler = gcTokenHandler;
        }

        public async Task<GCHandlingResult> Handle(Uri url, NameValueCollection queryString)
        {
            if (url.AbsolutePath != "/force-traffic-affecting-gc")
                return new GCHandlingResult(false);

            var config = _microdotHostingConfigFactory();

            if (config.GCEndpointEnabled)
            {
                
                if (_gcTokenHandler.TryProcessAsTokenGenerationRequest(queryString, out var additionalInfo))
                    return new GCHandlingResult(true, additionalInfo);

                if (false == _gcTokenHandler.ValidateToken(queryString, out additionalInfo))
                    return new GCHandlingResult(true, additionalInfo);

                if (false == _gcTokenHandler.ValidateGcType(queryString, out additionalInfo, out var gcType))
                    return new GCHandlingResult(true, additionalInfo);

                var gcCollectionResult = _gcCollectionRunner.Collect(gcType);

                _logger.Info(log=>log("GC endpoint was called",unencryptedTags:new
                {
                    GcType = gcType,
                    TotalMemoryAfterGC = gcCollectionResult.TotalMemoryAfterGC,
                    TotalMemoryBeforeGC = gcCollectionResult.TotalMemoryBeforeGC,
                    GCDuration = gcCollectionResult.ElapsedMilliseconds
                    
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