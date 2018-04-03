using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Gigya.Microdot.Hosting.Events;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.HttpService;
using Gigya.Microdot.SharedLogic.Events;
using Newtonsoft.Json;

namespace Gigya.Microdot.Hosting.HttpService
{
    public interface IServerRequestPublisher
    {
        void TryPublish(HttpServiceRequest requestData, Exception ex, ServiceMethod serviceMethod, double requestTime);
    }

    public class ServerRequestPublisher : IServerRequestPublisher
    {
        private readonly IEventPublisher<ServiceCallEvent> _eventPublisher;
        private readonly IPropertiesMetadataPropertiesCache _metadataPropertiesCache;
        private readonly IServiceEndPointDefinition _serviceEndPointDefinition;

        public ServerRequestPublisher(IEventPublisher<ServiceCallEvent> eventPublisher,
                                      IPropertiesMetadataPropertiesCache metadataPropertiesCache,
                                      IServiceEndPointDefinition serviceEndPointDefinition)
        {
            _eventPublisher = eventPublisher;
            _metadataPropertiesCache = metadataPropertiesCache;
            _serviceEndPointDefinition = serviceEndPointDefinition;
        }

        public void TryPublish(HttpServiceRequest requestData, Exception ex, ServiceMethod serviceMethod,
            double requestTime)
        {
            var callEvent = _eventPublisher.CreateEvent();

            callEvent.CalledServiceName = serviceMethod?.GrainInterfaceType.Name;
            callEvent.ClientMetadata = requestData.TracingData;
            callEvent.ServiceMethod = requestData.Target?.MethodName;

            var metaData = _serviceEndPointDefinition.GetMetaData(serviceMethod);
            var arguments = (requestData.Arguments ?? new OrderedDictionary()).Cast<DictionaryEntry>();

            callEvent.Params = arguments.SelectMany(_ => ExtractParamValues(_, metaData)).Select(_ => new Param{
                Name = _.name,
                Value = _.value,
                Sensitivity = _.sensitivity,
            });
            callEvent.Exception = ex;
            callEvent.ActualTotalTime = requestTime;
            callEvent.ErrCode = ex != null ? null : (int?)0;

            _eventPublisher.TryPublish(callEvent);
        }


        private IEnumerable<(string name, object value, Sensitivity sensitivity)> ExtractParamValues(DictionaryEntry pair, EndPointMetadata metaData)
        {
            var key = pair.Key.ToString();
            var sensitivity = metaData.ParameterAttributes[key].Sensitivity ?? metaData.MethodSensitivity ?? Sensitivity.Sensitive;
            if (metaData.ParameterAttributes[key].IsLogFieldAttributeExists == true && (pair.Value is string) == false)
            {
                var type = pair.Value?.GetType();
                if (type?.IsClass==true)
                {
                    var metaParams = _metadataPropertiesCache.ParseIntoParams(pair.Value);

                    foreach (var metaParam in metaParams)
                    {
                        var tuple = (
                            name: $"{key}_{metaParam.Name}",
                            value: metaParam.Value,
                            sensitivity: metaParam.Sensitivity ?? sensitivity);

                        yield return tuple;
                    }
                }
            }
            else
            {
                yield return (key, pair.Value, sensitivity);
            }
        }

    }
}
