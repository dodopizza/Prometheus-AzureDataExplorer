using System;
using System.IO;
using System.IO.Compression;

using System.Collections.Generic;

using System.Globalization;

using System.Threading.Tasks;

using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using Microsoft.WindowsAzure.Storage;

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

using Google.Protobuf.Collections;

using Prometheus;
using PrometheusHelper.Helper;

namespace PrometheusWrite
{

    public class KustoRow
    {
        [Index(0)]
        public string Datetime
        {
            get {
                // Representing unixtime milliseconds as ISO 8601 string datetime
                // https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/scalar-data-types/datetime
                return DateTimeOffset.FromUnixTimeMilliseconds( this.Timestamp ).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff");
            }
            set { }
        }
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
        public long LabelsHash { get; set; }
        [Index(7)]
        public double Value { get; set; }
    }

    public static class Write
    {
        [FunctionName("Write")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("[PrometehusWrite] HTTP trigger function processed a Write request.");

            var decompressed = Conversion.DecompressBody(req.Body);
            var writerequest = WriteRequest.Parser.ParseFrom(decompressed);

            //

            log.LogInformation("[PrometehusWrite] Connecting to storage account");

            var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("blobStorageConnectionString"));
            var client = storageAccount.CreateCloudBlobClient();
            var container = client.GetContainerReference( Environment.GetEnvironmentVariable("blobStorageContainerName") );
            await container.CreateIfNotExistsAsync();

            //

            log.LogInformation("[PrometehusWrite] Prepare blob reference");

            var name = Guid.NewGuid().ToString("n");
            var blob = container.GetBlockBlobReference($"{name}.csv.gz");
            blob.Properties.ContentType = "application/gzip";

            //

            log.LogInformation("[PrometehusWrite] Create a kustoRows List with data");

            var kustoRows = new List<KustoRow>();
            foreach (var timeseries in writerequest.Timeseries)
            {
                kustoRows.Add(KustoRowConverter.ToKustoRow(timeseries));
            }

            //

            log.LogInformation("[PrometehusWrite] Write to blob");

            using (var blobStream = blob.OpenWriteAsync().Result)
            using (var gstream = new GZipStream(blobStream, CompressionMode.Compress))
            {
                using (var writer = new StreamWriter(gstream))
                {
                    using (var csv = new CsvWriter( writer, new CsvConfiguration(CultureInfo.InvariantCulture) {HasHeaderRecord = false}) )
                    {
                        csv.WriteRecords(kustoRows);
                    }
                }
            }

            log.LogInformation("[PrometehusWrite] Done");
            return new OkResult();
        }

    }

    public static class KustoRowConverter
    {
        public static KustoRow ToKustoRow(TimeSeries timeseries)
        {
            KustoRow kustoRow = new KustoRow();
            kustoRow.Timestamp = timeseries.Samples[0].Timestamp;
            kustoRow.Value = timeseries.Samples[0].Value;
            var labelsDict = new SortedDictionary<string, string>();
            foreach (var label in timeseries.Labels)
            {
                switch (label.Name)
                {
                    case "__name__":
                        kustoRow.Name = label.Value;
                        break;
                    case "job":
                        kustoRow.Job = label.Value;
                        break;
                    case "instance":
                        kustoRow.Instance = label.Value;
                        break;
                }
                labelsDict[ label.Name ] = label.Value;
            }

            kustoRow.Labels = JsonConvert.SerializeObject(labelsDict);
            kustoRow.LabelsHash = SDBMHash(kustoRow.Labels);

            return kustoRow;
        }

        static public long SDBMHash(String str)
            {
                long hash = 0;

                for (Int32 i = 0; i < str.Length; i++)
                {
                    hash = str[i] + (hash << 6) + (hash << 16) - hash;
                }

                return hash;
            }
    }

}