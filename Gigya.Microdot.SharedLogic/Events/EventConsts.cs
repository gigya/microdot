namespace Gigya.Microdot.SharedLogic.Events
{
    public static class EventConsts
    {
        
        public const string type = "type";
        public const string ServerReqType = "serverReq";
        public const string ClientReqType = "clientReq";
        public const string GrainReqType = "grainReq";
        public const string BaseEventType = "event";

        public const string callID = "callID";
        public const string spanID = "spanID";
        public const string parentSpanID = "pspanID";
        public const string statsTotalTime = "stats.total.time";
        public const string statsServerTime = "stats.server.time";
        public const string statsNetworkTime = "stats.network.time";

        public const string srvSystem = "srv.system";
        public const string srvService = "srv.service";
        public const string srvSystemInstance = "srv.systemInstance";
        public const string srvVersion = "srv.version";
        public const string infrVersion = "infr.version";

        public const string message = "message";
        public const string details = "details";

        public const string errCode = "errCode";
        public const string exMessage = "ex.message";
        public const string exOneWordMessage = "ex.oneWordMessage";
        public const string exInnerMessages = "ex.innerMessages";
        public const string exStackTrace = "ex.stackTrace";
        public const string exType = "ex.type";
        public const string tags = "tags";

        public const string targetService="target.service";
        public const string targetHost = "target.host";
        public const string targetType = "target.type";
        public const string targetMethod = "target.method";

        public const string protocolMethod = "protocol.Method";
        public const string protocolParams = "protocol.Params";
        public const string clnSendTimestamp = "cln.sendTimestamp";

        public const string runtimeHost = "runtime.host";
        public const string runtimeDC = "runtime.dc";
        public const string runtimeENV = "runtime.env";

        public const string siteID = "siteID";
        public const string apikey = "apikey";
        public const string partnerID = "partnerID";

    }
}