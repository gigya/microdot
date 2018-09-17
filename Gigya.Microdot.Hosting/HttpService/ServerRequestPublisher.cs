using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Gigya.Microdot.Hosting.Events;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.HttpService;
using Gigya.Microdot.SharedLogic.Events;

namespace Gigya.Microdot.Hosting.HttpService
{
    public interface IServerRequestPublisher
    {
        void TryPublish(HttpServiceRequest requestData, Exception ex, ServiceMethod serviceMethod, double requestTime, double? responseTime);
        void TryPublish(ServiceCallEvent callEvent, IEnumerable<DictionaryEntry> arguments, ServiceMethod serviceMethod);
        ServiceCallEvent GetNewCallEvent();
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

        public ServiceCallEvent GetNewCallEvent()
        {
            return _eventPublisher.CreateEvent();
        }

        public void TryPublish(HttpServiceRequest requestData, Exception ex, ServiceMethod serviceMethod, double requestTime, double? responseTime)
        {
            ServiceCallEvent callEvent = GetNewCallEvent();

            callEvent.CalledServiceName = serviceMethod?.GrainInterfaceType.Name;
            callEvent.ClientMetadata = requestData.TracingData;
            callEvent.ServiceMethod = requestData.Target?.MethodName;

            var arguments = (requestData.Arguments ?? new OrderedDictionary()).Cast<DictionaryEntry>();

            callEvent.Exception = ex;
            callEvent.ActualTotalTime = requestTime;
            callEvent.ErrCode = ex != null ? null : (int?)0;
            callEvent.ResponseTime = responseTime;

            TryPublish(callEvent, arguments, serviceMethod);
        }

        public void TryPublish(ServiceCallEvent callEvent, IEnumerable<DictionaryEntry> arguments, ServiceMethod serviceMethod)
        {
            EndPointMetadata metaData = _serviceEndPointDefinition.GetMetaData(serviceMethod);
            callEvent.Params = arguments.SelectMany(_ => ExtractParamValues(_, metaData)).Select(_ => new Param
            {
                Name = _.name,
                Value = _.value,
                Sensitivity = _.sensitivity,
            });

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
