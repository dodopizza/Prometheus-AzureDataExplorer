using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using PromADX.PrometheusHelper;
using Prometheus;

namespace PromADX.PrometheusWrite
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
        public long LabelsHash => Hash.SdbmHash(Labels);

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
            log.LogInformation("[PrometheusWrite] HTTP trigger function processed a Write request.");

            var decompressed = Conversion.DecompressBody(req.Body);
            var writeRequest = WriteRequest.Parser.ParseFrom(decompressed);

            //

            log.LogInformation("[PrometheusWrite] Connecting to storage account");

            var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("blobStorageConnectionString"));
            var client = storageAccount.CreateCloudBlobClient();
            var container = client.GetContainerReference(Environment.GetEnvironmentVariable("blobStorageContainerName"));
            await container.CreateIfNotExistsAsync();

            //

            log.LogInformation("[PrometheusWrite] Prepare blob reference");

            var name = Guid.NewGuid().ToString("n");
            var blob = container.GetBlockBlobReference($"{name}.csv.gz");
            blob.Properties.ContentType = "application/gzip";

            //

            log.LogInformation("[PrometheusWrite] Create a kustoRows List with data");

            var kustoRows = writeRequest.Timeseries.Select(KustoRowConverter.ToKustoRow).ToList();

            //

            log.LogInformation("[PrometheusWrite] Write to blob");

            using (var blobStream = blob.OpenWriteAsync().Result)
            using (var gstream = new GZipStream(blobStream, CompressionMode.Compress))
            {
                using (var writer = new StreamWriter(gstream))
                {
                    using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false }))
                    {
                        csv.WriteRecords(kustoRows);
                    }
                }
            }

            log.LogInformation("[PrometheusWrite] Done");
            return new OkResult();
        }

    }

    public static class KustoRowConverter
    {
        public static KustoRow ToKustoRow(TimeSeries timeseries)
        {
            var kustoRow = new KustoRow
            {
                Timestamp = timeseries.Samples[0].Timestamp,
                Value = timeseries.Samples[0].Value
            };
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
                labelsDict[label.Name] = label.Value;
            }

            kustoRow.Labels = JsonConvert.SerializeObject(labelsDict);
            return kustoRow;
        }


    }

}