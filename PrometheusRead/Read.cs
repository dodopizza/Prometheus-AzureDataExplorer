using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PromADX.PrometheusHelper;
using Prometheus;
using Snappy.Sharp;

namespace PromADX.PrometheusRead
{
    public static class Read
    {
        private static readonly object Lock = new object();
        private static ICslQueryProvider _adx;
        private static bool _isInitialized = false;

        private static readonly SnappyCompressor Compressor = new SnappyCompressor();

        [FunctionName("Read")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]
            HttpRequest req,
            ILogger log)
        {
            var decompressed = HttpConversion.DecompressBody(req.Body);
            if (decompressed != null)
            {
                InitializeKustoClient(log);

                var readRequest = ReadRequest.Parser.ParseFrom(decompressed);
                log.LogMetric("querycount", readRequest.Queries.Count,
                    new Dictionary<string, object>() { { "type", "count" } });

                var response = CreateResponse(readRequest, log);

                log.LogMetric("result", response.Results.Count, new Dictionary<string, object>() { { "type", "count" } });
                log.LogMetric("timeseriesread", response.Results.Select(_ => _.Timeseries.Count).Sum(__ => __),
                    new Dictionary<string, object>() { { "type", "count" } });

                var ms = new MemoryStream();
                var output = new Google.Protobuf.CodedOutputStream(ms);
                response.WriteTo(output);

                output.Flush();

                var resultUncompressed = ms.ToArray();

                if (resultUncompressed.Length > 0)
                {
                    // Should be at least the size of the uncompressed one
                    byte[] resultCompressed = new byte[resultUncompressed.Length * 2];

                    var compressedSize = Compressor.Compress(resultUncompressed, 0, resultUncompressed.Length,
                        resultCompressed);

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
            lock (Lock)
            {
                if (!_isInitialized)
                {
                    log.LogInformation("[PrometheusRead] [InitializeKustoClient] Initialize");
                    var connection =
                        new KustoConnectionStringBuilder(
                                Environment.GetEnvironmentVariable("kustoUrl", EnvironmentVariableTarget.Process))
                            .WithAadApplicationKeyAuthentication(
                                applicationClientId: Environment.GetEnvironmentVariable("appClientId",
                                    EnvironmentVariableTarget.Process),
                                applicationKey: Environment.GetEnvironmentVariable("appClientSecret",
                                    EnvironmentVariableTarget.Process),
                                authority: Environment.GetEnvironmentVariable("tenantId",
                                    EnvironmentVariableTarget.Process));

                    _adx = KustoClientFactory.CreateCslQueryProvider(connection);

                    _isInitialized = true;
                }

                log.LogInformation("[PrometheusRead] [InitializeKustoClient] Ok");
            }
        } // - InitializeKustoClient


        private static ReadResponse CreateResponse(ReadRequest readrequest, ILogger log)
        {
            var result = new ReadResponse();
            var taskList = new List<Task<IDataReader>>();

            const string metricQueryTemplate = @"
                Metrics
                | where (EndDatetime >= unixtime_milliseconds_todatetime({0})) and (StartDatetime <= unixtime_milliseconds_todatetime({1})) and ( {2} )
                | summarize Labels=tostring(any(Labels)), Samples=make_list( Samples ) by LabelsHash
                | mv-apply Samples = Samples on
                (
                    order by tolong(Samples['Timestamp']) asc
                    | summarize Samples=make_list(pack('Timestamp', Samples['Timestamp'], 'Value', Samples['Value']))
                )
            ";

            var timer = new Stopwatch();
            timer.Start();

            foreach (var aQuery in readrequest.Queries)
            {
                var kql = string.Format(
                    metricQueryTemplate,
                    aQuery.StartTimestampMs,
                    aQuery.EndTimestampMs,
                    string.Join(
                        " and ",
                        aQuery.Matchers.Select(
                            item => KustoManipulations.ToKustoExpression(item.Name, item.Type, item.Value)
                        )
                    )
                );

                log.LogInformation($"KQL: {kql}");

                taskList.Add(
                    _adx.ExecuteQueryAsync(
                        Environment.GetEnvironmentVariable("kustoDatabase"),
                        kql,
                        null
                    )
                );
            } // - foreach readrequest.Queries

            log.LogInformation("[PrometheusRead] [ExecuteQueries] Queries count: " + taskList.Count());
            Task.WaitAll(taskList.ToArray());

            timer.Stop();
            log.LogInformation("[PrometheusRead] [ExecuteQueries] Execution time: " + timer.Elapsed);

            log.LogInformation("[PrometheusRead] [CreateQueryResult] Start serializing results");
            timer.Reset();

            result.Results.AddRange(taskList.Select(aTask => CreateQueryResult(aTask.Result, log)));

            timer.Stop();
            log.LogInformation(
                "[PrometheusRead] [CreateQueryResult] Serializing done. Execution time: " + timer.Elapsed);

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
                var tsItem = new Prometheus.TimeSeries();
                var labels = (string)reader["Labels"];
                var samples = (JArray)reader["Samples"];

                var samplesRange =
                    JsonConvert.DeserializeObject<IEnumerable<Prometheus.Sample>>(samples.ToString(),
                        jsonSerializerSettings);
                tsItem.Samples.AddRange(samplesRange);

                var labelsKv = JsonConvert.DeserializeObject<Dictionary<string, string>>(labels);

                foreach (var label in labelsKv.Select(
                    item => new Prometheus.Label { Name = item.Key, Value = item.Value }))
                {
                    tsItem.Labels.Add(label);
                }

                result.Timeseries.Add(tsItem);
            }

            reader.Close();

            return result;
        } // - QueryResult
        
    } // - class Read
} // - namespace PrometheusRead