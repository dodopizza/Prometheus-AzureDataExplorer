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

            var execTime = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");

            KustoConnectionStringBuilder connection =
                new KustoConnectionStringBuilder(Environment.GetEnvironmentVariable("kustoUrl", EnvironmentVariableTarget.Process)).WithAadApplicationKeyAuthentication(
                        applicationClientId: Environment.GetEnvironmentVariable("appClientId", EnvironmentVariableTarget.Process),
                        applicationKey: Environment.GetEnvironmentVariable("appClientSecret", EnvironmentVariableTarget.Process),
                        authority: Environment.GetEnvironmentVariable("tenantId", EnvironmentVariableTarget.Process));

            adx = KustoClientFactory.CreateCslAdminProvider(connection);

            string query = @"
                .set-or-append MetricsNew with (ingestIfNotExists = '[""{0}""]', tags='[""ingest-by:{0}""]') <|
                let PeriodStart = bin(now(-2d), 1h) - 3h;
                let PeriodEnd = bin(now(-2d), 1h);
                let Period = 1m;
                Metrics
                | where Datetime between ( PeriodStart .. PeriodEnd ) and isnan(Value) == false
                | summarize
                    Timestamp=bin(min(Timestamp), 60000), // start of minute
                    Name=any(Name),
                    Instance=any(Instance),
                    Job=any(Job),
                    Labels=any(Labels),
                    Value=avg(Value) by bin(Datetime, Period), LabelsHash
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
