using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Data;
using System.Linq;
using System.Collections.Generic;


using Newtonsoft.Json.Linq;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using Snappy.Sharp;

using Prometheus;
using PrometheusHelper.Helper;


using Kusto.Data.Common;
using Kusto.Data;
using Kusto.Data.Net.Client;


namespace PrometheusRead
{
    public static class Read
    {
        private static Object _lock = new object();
        private static ICslQueryProvider adx;
        private static Boolean _isInitialized = false;

        [FunctionName("Read")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {

            var decompressed = Conversion.DecompressBody(req.Body);
            if (decompressed != null)
            {
                InitializeKustoClient(log);

                var readrequest = ReadRequest.Parser.ParseFrom(decompressed);
                log.LogMetric("querycount", readrequest.Queries.Count, new Dictionary<String, object>() { { "type", "count" } });

                ReadResponse response = CreateResponse(readrequest, log);
            }







            // log.LogInformation("C# HTTP trigger function processed a request.");

            // string name = req.Query["name"];

            // string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            // dynamic data = JsonConvert.DeserializeObject(requestBody);
            // name = name ?? data?.name;

            // string responseMessage = string.IsNullOrEmpty(name)
            //     ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
            //     : $"Hello, {name}. This HTTP triggered function executed successfully.";

            // return new OkObjectResult(responseMessage);

        } // - function Read

        private static void InitializeKustoClient(ILogger log)
        {
            lock (_lock)
            {
                if(!_isInitialized)
                {
                    log.LogInformation("[PrometehusRead] [InitializeKustoClient] Initialize");
                    KustoConnectionStringBuilder connection =
                        new KustoConnectionStringBuilder(Environment.GetEnvironmentVariable("kustoUrl", EnvironmentVariableTarget.Process)).WithAadApplicationKeyAuthentication(
                        applicationClientId: Environment.GetEnvironmentVariable("appClientId", EnvironmentVariableTarget.Process),
                        applicationKey: Environment.GetEnvironmentVariable("appClientSecret", EnvironmentVariableTarget.Process),
                        authority: Environment.GetEnvironmentVariable("tenantId", EnvironmentVariableTarget.Process));

                    adx = KustoClientFactory.CreateCslQueryProvider(connection);

                    _isInitialized = true;
                }
                log.LogInformation("[PrometehusRead] [InitializeKustoClient] Ok");
            }
        } // - InitializeKustoClient

        private static ReadResponse CreateResponse(ReadRequest readrequest, ILogger log)
        {
            ReadResponse result = new ReadResponse();

            List<Task<IDataReader>> tasklist = new List<Task<IDataReader>>();

            foreach (var aQuery in readrequest.Queries)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"SearchTimeseries({aQuery.StartTimestampMs}, {aQuery.EndTimestampMs})");
                sb.AppendLine("| where " + String.Join(" and ", aQuery.Matchers.Select(aMatch => $"({GenerateValueExpression(aMatch.Name, aMatch.Type, aMatch.Value)})")));
                sb.AppendLine("| summarize Samples=make_list(timeseries.Samples) by Labels=tostring(timeseries.Labels)");
                sb.AppendLine("| extend timeseries=pack(\"Samples\", Samples, \"Labels\", parse_json(Labels))");
                sb.AppendLine("| project timeseries");

                log.LogInformation($"KQL: {sb.ToString()}");

                tasklist.Add(adx.ExecuteQueryAsync(databaseName: Environment.GetEnvironmentVariable("DATABASE"), query: sb.ToString(), null));
            }

            Task.WaitAll(tasklist.ToArray());

            result.Results.AddRange(tasklist.Select(aTask => CreateQueryResult(aTask.Result)));

            return result;
        } // - ReadResponse

        private static QueryResult CreateQueryResult(IDataReader reader)
        {
            var result = new QueryResult();

            while (reader.Read())
            {
                var timeSeriesObject = (JObject)reader.GetValue(0);

                result.Timeseries.Add(JsonConvert.DeserializeObject<TimeSeries>(timeSeriesObject.ToString()));
            }

            reader.Close();

            return result;
        }


    }
}
