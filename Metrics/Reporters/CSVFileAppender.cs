using System;
using System.Collections.Generic;
using System.IO;
namespace Metrics.Reporters
{
    public class CSVFileAppender : CSVAppender
    {
        private readonly string directory;

        public CSVFileAppender(string directory, string delimiter)
            : base(delimiter)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentNullException(nameof(directory));
            }

            Directory.CreateDirectory(directory);
            this.directory = directory;
        }

        protected virtual string FormatFileName(string directory, string metricName, string metricType)
        {
            var name = $"{metricName}.{metricType}.csv";
            return Path.Combine(directory, CleanFileName(name));
        }

        public override void AppendLine(DateTime timestamp, string metricType, string metricName, IEnumerable<CSVReport.Value> values)
        {
            var fileName = FormatFileName(this.directory, metricName, metricType);

            if (!File.Exists(fileName))
            {
                File.AppendAllLines(fileName, new[] { GetHeader(values), GetValues(timestamp, values) });
            }
            else
            {
                File.AppendAllLines(fileName, new[] { GetValues(timestamp, values) });
            }
        }

        protected virtual string CleanFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
