using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;


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

        private static SnappyCompressor compressor = new SnappyCompressor();

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

                log.LogMetric("result", response.Results.Count , new Dictionary<String, object>() { { "type", "count" } });
                log.LogMetric("timeseriesread", response.Results.Select(_ => _.Timeseries.Count).Sum(__ => __), new Dictionary<String, object>() { { "type", "count" } });

                MemoryStream ms = new MemoryStream();
                Google.Protobuf.CodedOutputStream output = new Google.Protobuf.CodedOutputStream(ms);
                response.WriteTo(output);

                output.Flush();

                var resultUncompressed = ms.ToArray();

                if (resultUncompressed.Length > 0)
                {
                    //should be at least the size of the uncompressed one
                    byte[] resultCompressed = new byte[resultUncompressed.Length*2];

                    var compressedSize = compressor.Compress(resultUncompressed, 0, resultUncompressed.Length, resultCompressed);

                    Array.Resize(ref resultCompressed, compressedSize);

                    return new FileContentResult(resultCompressed, "application/x-protobuf");
                }
                else
                {
                    return new FileContentResult(resultUncompressed, "application/x-protobuf");
                }
            }

            return null;

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

            string MetricaQueryTemplate = @"
                Metrics
                | where Timestamp between ( {0} .. {1} ) and ( {2} )
                | order by Timestamp asc
                | summarize Samples=make_list( pack( 'Timestamp', Timestamp, 'Value', Value ) ) by tostring( Labels )
            ";

            Stopwatch timer = new Stopwatch();
            timer.Start();

            foreach (var aQuery in readrequest.Queries)
            {

                var kustosql = string.Format(
                    MetricaQueryTemplate,
                    aQuery.StartTimestampMs,
                    aQuery.EndTimestampMs,
                    String.Join(
                        " and ",
                        aQuery.Matchers.Select(
                            item => GenerateValueExpression(item.Name, item.Type, item.Value)
                        )
                    )
                );

                log.LogInformation($"KQL: {kustosql}");

                tasklist.Add(
                    adx.ExecuteQueryAsync(
                        databaseName: Environment.GetEnvironmentVariable("kustoDatabase"),
                        query: kustosql,
                        null
                    )
                );

            } // - foreach readrequest.Queries

            log.LogInformation( "[PrometehusRead] [ExecuteQueries] Queries count: " + tasklist.Count() );
            Task.WaitAll(tasklist.ToArray());
            
            timer.Stop();
            log.LogInformation( "[PrometehusRead] [ExecuteQueries] Execution time: " + timer.Elapsed );

            log.LogInformation( "[PrometehusRead] [CreateQueryResult] Start serializing results" );
            timer.Reset();

            result.Results.AddRange(tasklist.Select(aTask => CreateQueryResult(aTask.Result, log)));

            timer.Stop();
            log.LogInformation( "[PrometehusRead] [CreateQueryResult] Serializing done. Execution time: " + timer.Elapsed );

            return result;
        } // - ReadResponse

        private static QueryResult CreateQueryResult(IDataReader reader, ILogger log)
        {
            var result = new QueryResult();

            var jsonSerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };

            while (reader.Read())
            {
                var timeseriesItem = new Prometheus.TimeSeries();
                var Labels = (string)reader["Labels"];
                var Samples = (JArray)reader["Samples"];

                timeseriesItem.Samples.AddRange(
                    JsonConvert.DeserializeObject<IEnumerable<Prometheus.Sample>>( Samples.ToString(), jsonSerializerSettings )
                );

                var labelsKV = JsonConvert.DeserializeObject<Dictionary<string,string>>(Labels);
                var LabelsProm = new List<Prometheus.Label>();

                foreach ( KeyValuePair<string, string> item in labelsKV )
                {
                    var label = new Prometheus.Label();
                    label.Name = item.Key;
                    label.Value = item.Value;

                    timeseriesItem.Labels.Add(label);
                }

                result.Timeseries.Add( timeseriesItem );
            }

            reader.Close();

            return result;
        } // - QueryResult

        private static String GenerateValueExpression(string name, LabelMatcher.Types.Type type, string value)
        {
            var KeyMap = new Dictionary<String, String>(){
                { "__name__", "Name" },
                { "job",      "Job" },
                { "instance", "Instance" }
            };

            string resultName = KeyMap.ContainsKey(name) ? KeyMap[name] : $"tostring(Labels.{name})";

            string queryTemplate = "( {0} {1} '{2}' )";

            switch (type)
            {
                case LabelMatcher.Types.Type.Eq:
                    return string.Format(queryTemplate, resultName, "==", value );
                case LabelMatcher.Types.Type.Neq:
                    return string.Format(queryTemplate, resultName, "!=", value );
                case LabelMatcher.Types.Type.Re:
                    return string.Format(queryTemplate, resultName, "matches regex", value );
                case LabelMatcher.Types.Type.Nre:
                    return string.Format(queryTemplate, resultName, "!contains", value );
                default:
                    return String.Empty;
            }

        } // - GenerateValueExpression

    } // - class Read

} // - namespace PrometheusRead
