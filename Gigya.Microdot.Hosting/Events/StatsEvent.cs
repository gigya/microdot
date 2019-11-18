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

using System;
using System.Collections.Generic;
using System.Globalization;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Measurement;

namespace Gigya.Microdot.Hosting.Events
{
    public abstract class StatsEvent : Event
    {
        private Lazy<RequestTimings> Timings { get; } = new Lazy<RequestTimings>(() => RequestTimings.Current);

        [EventField("stats.custom", OmitFromAudit = true)]
        public IEnumerable<KeyValuePair<string, string>> UserStats
        {
            get
            {
                var userMeasurements = new Dictionary<string, string>();

                foreach (var value in Timings.Value.UserStats)
                {
                    userMeasurements.Add(value.Key + ".count", value.Value.TotalInstances.ToString());
                    userMeasurements.Add(value.Key + ".avgMs", (value.Value.TotalTime.TotalMilliseconds / value.Value.TotalInstances).ToString(CultureInfo.InvariantCulture));
                }

                return userMeasurements;
            }
        }


        [EventField(EventConsts.statsTotalTime, OmitFromAudit = true)]
        public virtual double? TotalTime => Timings.Value.Request.ElapsedMS;

        [EventField("stats.mysql.time", OmitFromAudit = true)]
        public double? MySqlTime => Timings.Value.DataSource.MySql.Total.ElapsedMS;

        [EventField("stats.mysql.readTime", OmitFromAudit = true)]
        public double? TimeMySqlRead => Timings.Value.DataSource.MySql.Read.ElapsedMS;

        [EventField("stats.mysql.writeTime", OmitFromAudit = true)]
        public double? TimeMySqlWrite => Timings.Value.DataSource.MySql.Write.ElapsedMS;

        [EventField("stats.mysql.deleteTime", OmitFromAudit = true)]
        public double? TimeMySqlDelete => Timings.Value.DataSource.MySql.Delete.ElapsedMS;

        [EventField("stats.mysql.calls", OmitFromAudit = true)]
        public long? CallsMySql => Timings.Value.DataSource.MySql.Total.Calls;

        [EventField("stats.mysql.reads", OmitFromAudit = true)]
        public long? CallsMySqlRead => Timings.Value.DataSource.MySql.Read.Calls;

        [EventField("stats.mysql.writes", OmitFromAudit = true)]
        public long? CallsMySqlWrite => Timings.Value.DataSource.MySql.Write.Calls;

        [EventField("stats.mysql.deletes", OmitFromAudit = true)]
        public long? CallsMySqlDelete => Timings.Value.DataSource.MySql.Delete.Calls;


        [EventField("stats.file.time", OmitFromAudit = true)]
        public double? FileTime => Timings.Value.DataSource.File.Total.ElapsedMS;

        [EventField("stats.file.readTime", OmitFromAudit = true)]
        public double? TimeFileRead => Timings.Value.DataSource.File.Read.ElapsedMS;

        [EventField("stats.file.writeTime", OmitFromAudit = true)]
        public double? TimeFileWrite => Timings.Value.DataSource.File.Write.ElapsedMS;

        [EventField("stats.file.deleteTime", OmitFromAudit = true)]
        public double? TimeFileDelete => Timings.Value.DataSource.File.Delete.ElapsedMS;

        [EventField("stats.file.calls", OmitFromAudit = true)]
        public long? CallsFile => Timings.Value.DataSource.File.Total.Calls;

        [EventField("stats.file.reads", OmitFromAudit = true)]
        public long? CallsFileRead => Timings.Value.DataSource.File.Read.Calls;

        [EventField("stats.file.writes", OmitFromAudit = true)]
        public long? CallsFileWrite => Timings.Value.DataSource.File.Write.Calls;

        [EventField("stats.file.deletes", OmitFromAudit = true)]
        public long? CallsFileDelete => Timings.Value.DataSource.File.Delete.Calls;


        [EventField("stats.mongo.time", OmitFromAudit = true)]
        public double? TimeMongo => Timings.Value.DataSource.Mongo.Total.ElapsedMS;

        [EventField("stats.mongo.readTime", OmitFromAudit = true)]
        public double? TimeMongoRead => Timings.Value.DataSource.Mongo.Read.ElapsedMS;

        [EventField("stats.mongo.writeTime", OmitFromAudit = true)]
        public double? TimeMongoWrite => Timings.Value.DataSource.Mongo.Write.ElapsedMS;

        [EventField("stats.mongo.deleteTime", OmitFromAudit = true)]
        public double? TimeMongoDelete => Timings.Value.DataSource.Mongo.Delete.ElapsedMS;

        [EventField("stats.mongo.calls", OmitFromAudit = true)]
        public long? CallsMongo => Timings.Value.DataSource.Mongo.Total.Calls;

        [EventField("stats.mongo.reads", OmitFromAudit = true)]
        public long? CallsMongoRead => Timings.Value.DataSource.Mongo.Read.Calls;

        [EventField("stats.mongo.writes", OmitFromAudit = true)]
        public long? CallsMongoWrite => Timings.Value.DataSource.Mongo.Write.Calls;

        [EventField("stats.mongo.deletes", OmitFromAudit = true)]
        public long? CallsMongoDelete => Timings.Value.DataSource.Mongo.Delete.Calls;



        [EventField("stats.memcache.time", OmitFromAudit = true)]
        public double? TimeMemcache => Timings.Value.DataSource.Memcached.Total.ElapsedMS;

        [EventField("stats.memcache.readTime", OmitFromAudit = true)]
        public double? TimeMemcacheRead => Timings.Value.DataSource.Memcached.Read.ElapsedMS;

        [EventField("stats.memcache.writeTime", OmitFromAudit = true)]
        public double? TimeMemcacheWrite => Timings.Value.DataSource.Memcached.Write.ElapsedMS;

        [EventField("stats.memcache.deleteTime", OmitFromAudit = true)]
        public double? TimeMemcacheDelete => Timings.Value.DataSource.Memcached.Delete.ElapsedMS;

        [EventField("stats.memcache.calls", OmitFromAudit = true)]
        public long? CallsMemcache => Timings.Value.DataSource.Memcached.Total.Calls;

        [EventField("stats.memcache.reads", OmitFromAudit = true)]
        public long? CallsMemcacheRead => Timings.Value.DataSource.Memcached.Read.Calls;

        [EventField("stats.memcache.writes", OmitFromAudit = true)]
        public long? CallsMemcacheWrite => Timings.Value.DataSource.Memcached.Write.Calls;

        [EventField("stats.memcache.deletes", OmitFromAudit = true)]
        public long? CallsMemcacheDelete => Timings.Value.DataSource.Memcached.Delete.Calls;



        [EventField("stats.hades.time", OmitFromAudit = true)]
        public double? TimeHades => Timings.Value.DataSource.Hades.Total.ElapsedMS;

        [EventField("stats.hades.readTime", OmitFromAudit = true)]
        public double? TimeHadesRead => Timings.Value.DataSource.Hades.Read.ElapsedMS;

        [EventField("stats.hades.writeTime", OmitFromAudit = true)]
        public double? TimeHadesWrite => Timings.Value.DataSource.Hades.Write.ElapsedMS;

        [EventField("stats.hades.deleteTime", OmitFromAudit = true)]
        public double? TimeHadesDelete => Timings.Value.DataSource.Hades.Delete.ElapsedMS;

        [EventField("stats.hades.calls", OmitFromAudit = true)]
        public long? CallsHades => Timings.Value.DataSource.Hades.Total.Calls;

        [EventField("stats.hades.reads", OmitFromAudit = true)]
        public long? CallsHadesRead => Timings.Value.DataSource.Hades.Read.Calls;

        [EventField("stats.hades.writes", OmitFromAudit = true)]
        public long? CallsHadesWrite => Timings.Value.DataSource.Hades.Write.Calls;

        [EventField("stats.hades.deletes", OmitFromAudit = true)]
        public long? CallsHadesDelete => Timings.Value.DataSource.Hades.Delete.Calls;



    }
}