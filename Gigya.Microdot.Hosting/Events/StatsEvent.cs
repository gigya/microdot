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

        [FlumeField("stats.custom", OmitFromAudit = true)]
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


        [FlumeField(EventConsts.statsTotalTime, OmitFromAudit = true)]
        public virtual double? TotalTime => Timings.Value.Request.ElapsedMS;

        [FlumeField("stats.mysql.time", OmitFromAudit = true)]
        public double? MySqlTime => Timings.Value.DataSource.MySql.Total.ElapsedMS;

        [FlumeField("stats.mysql.readTime", OmitFromAudit = true)]
        public double? TimeMySqlRead => Timings.Value.DataSource.MySql.Read.ElapsedMS;

        [FlumeField("stats.mysql.writeTime", OmitFromAudit = true)]
        public double? TimeMySqlWrite => Timings.Value.DataSource.MySql.Write.ElapsedMS;

        [FlumeField("stats.mysql.deleteTime", OmitFromAudit = true)]
        public double? TimeMySqlDelete => Timings.Value.DataSource.MySql.Delete.ElapsedMS;

        [FlumeField("stats.mysql.calls", OmitFromAudit = true)]
        public long? CallsMySql => Timings.Value.DataSource.MySql.Total.Calls;

        [FlumeField("stats.mysql.reads", OmitFromAudit = true)]
        public long? CallsMySqlRead => Timings.Value.DataSource.MySql.Read.Calls;

        [FlumeField("stats.mysql.writes", OmitFromAudit = true)]
        public long? CallsMySqlWrite => Timings.Value.DataSource.MySql.Write.Calls;

        [FlumeField("stats.mysql.deletes", OmitFromAudit = true)]
        public long? CallsMySqlDelete => Timings.Value.DataSource.MySql.Delete.Calls;


        [FlumeField("stats.mongo.time", OmitFromAudit = true)]
        public double? TimeMongo => Timings.Value.DataSource.Mongo.Total.ElapsedMS;

        [FlumeField("stats.mongo.readTime", OmitFromAudit = true)]
        public double? TimeMongoRead => Timings.Value.DataSource.Mongo.Read.ElapsedMS;

        [FlumeField("stats.mongo.writeTime", OmitFromAudit = true)]
        public double? TimeMongoWrite => Timings.Value.DataSource.Mongo.Write.ElapsedMS;

        [FlumeField("stats.mongo.deleteTime", OmitFromAudit = true)]
        public double? TimeMongoDelete => Timings.Value.DataSource.Mongo.Delete.ElapsedMS;

        [FlumeField("stats.mongo.calls", OmitFromAudit = true)]
        public long? CallsMongo => Timings.Value.DataSource.Mongo.Total.Calls;

        [FlumeField("stats.mongo.reads", OmitFromAudit = true)]
        public long? CallsMongoRead => Timings.Value.DataSource.Mongo.Read.Calls;

        [FlumeField("stats.mongo.writes", OmitFromAudit = true)]
        public long? CallsMongoWrite => Timings.Value.DataSource.Mongo.Write.Calls;

        [FlumeField("stats.mongo.deletes", OmitFromAudit = true)]
        public long? CallsMongoDelete => Timings.Value.DataSource.Mongo.Delete.Calls;



        [FlumeField("stats.memcache.time", OmitFromAudit = true)]
        public double? TimeMemcache => Timings.Value.DataSource.Memcached.Total.ElapsedMS;

        [FlumeField("stats.memcache.readTime", OmitFromAudit = true)]
        public double? TimeMemcacheRead => Timings.Value.DataSource.Memcached.Read.ElapsedMS;

        [FlumeField("stats.memcache.writeTime", OmitFromAudit = true)]
        public double? TimeMemcacheWrite => Timings.Value.DataSource.Memcached.Write.ElapsedMS;

        [FlumeField("stats.memcache.deleteTime", OmitFromAudit = true)]
        public double? TimeMemcacheDelete => Timings.Value.DataSource.Memcached.Delete.ElapsedMS;

        [FlumeField("stats.memcache.calls", OmitFromAudit = true)]
        public long? CallsMemcache => Timings.Value.DataSource.Memcached.Total.Calls;

        [FlumeField("stats.memcache.reads", OmitFromAudit = true)]
        public long? CallsMemcacheRead => Timings.Value.DataSource.Memcached.Read.Calls;

        [FlumeField("stats.memcache.writes", OmitFromAudit = true)]
        public long? CallsMemcacheWrite => Timings.Value.DataSource.Memcached.Write.Calls;

        [FlumeField("stats.memcache.deletes", OmitFromAudit = true)]
        public long? CallsMemcacheDelete => Timings.Value.DataSource.Memcached.Delete.Calls;



        [FlumeField("stats.hades.time", OmitFromAudit = true)]
        public double? TimeHades => Timings.Value.DataSource.Hades.Total.ElapsedMS;

        [FlumeField("stats.hades.readTime", OmitFromAudit = true)]
        public double? TimeHadesRead => Timings.Value.DataSource.Hades.Read.ElapsedMS;

        [FlumeField("stats.hades.writeTime", OmitFromAudit = true)]
        public double? TimeHadesWrite => Timings.Value.DataSource.Hades.Write.ElapsedMS;

        [FlumeField("stats.hades.deleteTime", OmitFromAudit = true)]
        public double? TimeHadesDelete => Timings.Value.DataSource.Hades.Delete.ElapsedMS;

        [FlumeField("stats.hades.calls", OmitFromAudit = true)]
        public long? CallsHades => Timings.Value.DataSource.Hades.Total.Calls;

        [FlumeField("stats.hades.reads", OmitFromAudit = true)]
        public long? CallsHadesRead => Timings.Value.DataSource.Hades.Read.Calls;

        [FlumeField("stats.hades.writes", OmitFromAudit = true)]
        public long? CallsHadesWrite => Timings.Value.DataSource.Hades.Write.Calls;

        [FlumeField("stats.hades.deletes", OmitFromAudit = true)]
        public long? CallsHadesDelete => Timings.Value.DataSource.Hades.Delete.Calls;



    }
}