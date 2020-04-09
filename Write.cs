using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

// using Microsoft.WindowsAzure.Storage;

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

// using Google.Protobuf.Collections;

using PrometheusHelper.Helper;



namespace PrometheusWrite
{
    public static class Write
    {
        [FunctionName("Write")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];


            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }


        public class KustoRow
        {
            [Index(0)]
            public long Timestamp { get; set; }
            [Index(1)]
            public string Name { get; set; }
            [Index(2)]
            public string Instance { get; set; }
            [Index(3)]
            public string Job { get; set; }
            [Index(4)]
            public string Labels { get; set; }
            [Index(5)]
            public double Value { get; set; }
        }
        
        public static class KustoRowConverter
        {
            public static KustoRow ToKustoRow(TimeSeries timeseries)
            {
                KustoRow kustoRow = new KustoRow();
                kustoRow.Timestamp = timeseries.Samples[0].Timestamp;
                kustoRow.Value = timeseries.Samples[0].Value;
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
                }

                kustoRow.Labels = JsonConvert.SerializeObject(timeseries.Labels);

                return kustoRow;
            }
        }
    }
}
