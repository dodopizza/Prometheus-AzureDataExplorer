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

    public static class Write
    {
        [FunctionName("Write")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("[PrometheusWrite] HTTP trigger function processed a Write request.");

            var decompressed = HttpConversion.DecompressBody(req.Body);
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
            var kustoRows = writeRequest.Timeseries.Select(KustoManipulations.ToKustoRow).ToList();

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

}