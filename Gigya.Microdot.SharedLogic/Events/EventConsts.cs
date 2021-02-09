#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion


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
        public const string unknownTracingData = "unknownTracingData";
        public const string statsTotalTime = "stats.total.time";
        public const string statsServerTime = "stats.server.time";
        public const string statsNetworkTime = "stats.network.time";
        public const string statsNetworkPostTime = "stats.network.post.time";
        public const string clientReadResponseTime = "stats.network.read.time";

        public const string srvSystem = "srv.system";
        public const string srvService = "srv.service";
        public const string srvSystemInstance = "srv.systemInstance";
        public const string srvVersion = "srv.version";
        public const string infrVersion = "srv.infraVersion";

        public const string message = "message";
        public const string details = "details";

        public const string errCode = "errCode";
        public const string exMessage = "ex.message";
        public const string exOneWordMessage = "ex.oneWordMessage";
        public const string exInnerMessages = "ex.inner.messages";
        public const string exOneWordInnerMessages = "ex.inner.oneWordMessages";
        public const string exInnerType = "ex.inner.type";
        public const string exStackTrace = "ex.stackTrace";
        public const string exStackTraceUnclean = "ex.stackTraceUnclean";
        public const string exType = "ex.type";
        public const string tags = "tags";
        public const string context = "ctx";

        public const string targetService="target.service";
        public const string targetHost = "target.host";
        public const string targetPort = "target.port";
        public const string targetType = "target.type";
        public const string targetMethod = "target.method";

        public const string targetEnvironment = "target.env";

        public const string protocolMethod = "protocol.Method";
        public const string protocolParams = "protocol.Params";
        public const string protocolSchema = "protocol.Schema";
        public const string clnSendTimestamp = "cln.sendTimestamp";

        public const string runtimeHost = "runtime.host";
        public const string runtimeREGION = "runtime.region";
        public const string runtimeZONE = "runtime.zone";
        public const string runtimeDC = "runtime.dc";
        public const string runtimeENV = "runtime.env";

        public const string siteID = "siteID";
        public const string apikey = "apikey";
        public const string partnerID = "partnerID";
        public const string grainKeyExtention = "grain.keyExtention";
        public const string grainPrimaryKey = "grain.primaryKey";
        public const string siloAddress="silo.address";
        public const string siloDeploymentId="silo.deploymentId";

        public const string SuppressCaching = "overrides.suppressCaching";
        
        public const string statsRetryCount = "debug.retryCount";
        public const string above10KmsgLength = "debug.above10KmsgLength";
        public const string isNewClientCreated = "debug.isNewClientCreated";
        public const string postDateTicks = "debug.post.date.ticks";
        public const string RecvDateTicks = "debug.recv.date.ticks";
        public const string ReqStartupDeltaTicks = "debug.reqStartupDelta.ticks";
        public const string TimeFromLastReq = "debug.timeFromLastReq.time";
        public const string OutstandingRecvRequests = "debug.OutstandingRecvRequests";
        public const string CollectionCountGen0 = "debug.CollectionCountGen0";
        public const string CollectionCountGen1 = "debug.CollectionCountGen1";
        public const string CollectionCountGen2 = "debug.CollectionCountGen2";
        public const string outstandingSentRequests = "debug.OutstandingSentRequests";
    }
}