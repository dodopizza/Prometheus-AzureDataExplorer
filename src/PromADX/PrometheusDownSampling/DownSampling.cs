using System;
using Kusto.Data;
using Kusto.Data.Net.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace PromADX.PrometheusDownSampling
{
    public static class DownSampling
    {
        [FunctionName("DownSampling")]
        public static void Run([TimerTrigger("0 15,45 * * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            var tagMinute = DateTime.Now.Minute / 30 * 30;

            var timeTagTemplate = @"MM/dd/yyyy HH:{0}:00";

            var execTime = DateTime.Now.AddDays(-1).ToString(string.Format(timeTagTemplate, tagMinute));

            var connection =
                new KustoConnectionStringBuilder(Environment.GetEnvironmentVariable("kustoUrl", EnvironmentVariableTarget.Process)).WithAadApplicationKeyAuthentication(
                        applicationClientId: Environment.GetEnvironmentVariable("appClientId", EnvironmentVariableTarget.Process),
                        applicationKey: Environment.GetEnvironmentVariable("appClientSecret", EnvironmentVariableTarget.Process),
                        authority: Environment.GetEnvironmentVariable("tenantId", EnvironmentVariableTarget.Process));

            var adx = KustoClientFactory.CreateCslAdminProvider(connection);

            string query = @"
                .set-or-append Metrics with (ingestIfNotExists = '[""{0}""]', tags='[""ingest-by:{0}""]') <|
                let PeriodStart = bin(now(-1d), 30m) - 30m;
                let PeriodEnd = bin(now(-1d), 30m);
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

            var kql = string.Format(query, execTime);

            log.LogInformation($"KQL: {kql}");

            adx.ExecuteControlCommand(
                Environment.GetEnvironmentVariable("kustoDatabase"),
                kql);

        }
    }
}
