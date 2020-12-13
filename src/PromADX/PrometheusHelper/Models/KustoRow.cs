using System;
using CsvHelper.Configuration.Attributes;

namespace PromADX.PrometheusHelper.Models
{
    public class KustoRow
    {
        // Representing unixtime milliseconds as ISO 8601 string datetime
        // https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/scalar-data-types/datetime
        [Index(0)]
        public string Datetime => DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff");
        [Index(1)]
        public long Timestamp { get; set; }
        [Index(2)]
        public string Name { get; set; }
        [Index(3)]
        public string Instance { get; set; }
        [Index(4)]
        public string Job { get; set; }
        [Index(5)]
        public string Labels { get; set; }
        [Index(6)]
        public long LabelsHash => Hash.SDBMHash(Labels);
        [Index(7)]
        public double Value { get; set; }
    }
}