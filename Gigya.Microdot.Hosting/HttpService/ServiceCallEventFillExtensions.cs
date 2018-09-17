using System;
using Gigya.Microdot.Hosting.Events;
using Gigya.Microdot.Interfaces.HttpService;

namespace Gigya.Microdot.Hosting.HttpService
{
    public static class ServiceCallEventFillExtensions
    {
        public static void FillRequestData(this ServiceCallEvent callEvent, HttpServiceRequest requestData)
        {
            callEvent.ClientMetadata = requestData.TracingData;
            callEvent.ServiceMethod = requestData.Target?.MethodName;
        }

        public static void FillException(this ServiceCallEvent callEvent, Exception actualException)
        {
            callEvent.Exception = actualException;
            callEvent.ErrCode = actualException != null ? null : (int?)0;
        }

        public static void FillByServiceMethod(this ServiceCallEvent callEvent, ServiceMethod serviceMethod)
        {
            callEvent.CalledServiceName = serviceMethod?.GrainInterfaceType.Name;
        }

        public static void FillActualTotalTime(this ServiceCallEvent callEvent, double totalTime)
        {
            callEvent.ActualTotalTime = totalTime;
        }
    }
}
