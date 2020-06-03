using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Kusto.Data.Common;
using Kusto.Data;
using Kusto.Data.Net.Client;

namespace PrometheusDownSampling
{
    public static class DownSampling
    {
        private static ICslAdminProvider adx;
        [FunctionName("DownSampling")]
        public static void Run([TimerTrigger("0 30 */3 * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            var execTime = DateTime.Now.AddDays(-1).ToString("MM/dd/yyyy HH:00:00");

            KustoConnectionStringBuilder connection =
                new KustoConnectionStringBuilder(Environment.GetEnvironmentVariable("kustoUrl", EnvironmentVariableTarget.Process)).WithAadApplicationKeyAuthentication(
                        applicationClientId: Environment.GetEnvironmentVariable("appClientId", EnvironmentVariableTarget.Process),
                        applicationKey: Environment.GetEnvironmentVariable("appClientSecret", EnvironmentVariableTarget.Process),
                        authority: Environment.GetEnvironmentVariable("tenantId", EnvironmentVariableTarget.Process));

            adx = KustoClientFactory.CreateCslAdminProvider(connection);

            string query = @"
                .set-or-append Metrics with (ingestIfNotExists = '[""{0}""]', tags='[""ingest-by:{0}""]') <|
                let PeriodStart = bin(now(-1d), 1h) - 3h;
                let PeriodEnd = bin(now(-1d), 1h);
                let Step = 1m;
                RawData
                | where Datetime between ( PeriodStart .. PeriodEnd )
                | summarize
                    Timestamp=bin(min(Timestamp), 60000), // start of minute
                    Name=any(Name),
                    Instance=any(Instance),
                    Job=any(Job),
                    Labels=any(Labels),
                    Value=avg(Value) by bin(Datetime, Step), LabelsHash
                | summarize
                    StartDatetime=min(Datetime),
                    EndDatetime=max(Datetime),
                    Name=any(Name),
                    Instance=any(Instance),
                    Job=any(Job),
                    Labels=any(Labels),
                    Samples=make_list( pack( 'Timestamp', Timestamp, 'Value', Value ) ) by LabelsHash
            ";

            var kustosql = string.Format(query, execTime);

            log.LogInformation($"KQL: {kustosql}");

            Console.WriteLine("Executing query to Kusto...");

            adx.ExecuteControlCommand(
                databaseName: Environment.GetEnvironmentVariable("kustoDatabase"),
                command: kustosql,
                null);

        }
    }
}
