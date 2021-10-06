﻿
using Metrics.Utils;
using System;
using System.Diagnostics;
using System.Globalization;
namespace Metrics
{
    [DebuggerDisplay("{Name}")]
    public struct Unit : IHideObjectMembers
    {
        public static readonly Unit None = new Unit(string.Empty);
        public static readonly Unit Requests = new Unit("Requests");
        public static readonly Unit Commands = new Unit("Commands");
        public static readonly Unit Calls = new Unit("Calls");
        public static readonly Unit Events = new Unit("Events");
        public static readonly Unit Errors = new Unit("Errors");
        public static readonly Unit Results = new Unit("Results");
        public static readonly Unit Items = new Unit("Items");
        public static readonly Unit MegaBytes = new Unit("Mb");
        public static readonly Unit KiloBytes = new Unit("Kb");
        public static readonly Unit Bytes = new Unit("bytes");
        public static readonly Unit Percent = new Unit("%");
        public static readonly Unit Threads = new Unit("Threads");

        public static Unit Custom(string name)
        {
            return new Unit(name);
        }

        public static implicit operator Unit(string name)
        {
            return Unit.Custom(name);
        }

        public readonly string Name;

        private Unit(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            this.Name = name;
        }

        public override string ToString()
        {
            return this.Name;
        }

        public string FormatCount(long value)
        {
            if (!string.IsNullOrEmpty(this.Name))
            {
                return $"{value.ToString(CultureInfo.InvariantCulture)} {this.Name}";
            }
            return value.ToString();
        }

        public string FormatValue(double value)
        {
            if (!string.IsNullOrEmpty(this.Name))
            {
                return $"{value.ToString("F2", CultureInfo.InvariantCulture)} {this.Name}";
            }
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }

        public string FormatRate(double value, TimeUnit timeUnit)
        {
            return $"{value.ToString("F2", CultureInfo.InvariantCulture)} {this.Name}/{timeUnit.Unit()}";
        }

        public string FormatDuration(double value, TimeUnit? timeUnit)
        {
            return $"{value.ToString("F2", CultureInfo.InvariantCulture)} {(timeUnit.HasValue ? timeUnit.Value.Unit() : this.Name)}";
        }
    }
}
