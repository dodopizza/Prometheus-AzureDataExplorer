using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace PrometheusDownSampling
{
    public static class DownSampling
    {
        [FunctionName("DownSampling")]
        public static void Run([TimerTrigger("*/5 * * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

                // let PeriodStart = startofday(now(-3d));
                // let PeriodEnd = endofday(PeriodStart);
                // let Period = 1h;
                // Metrics
                // | where Datetime between ( PeriodStart .. PeriodEnd ) and isnan(Value) == false
                // | summarize Timestamp=max(Timestamp), Name=any(Name), Instance=any(Instance), Job=any(Job), Labels=any(Labels), Value=avg(Value) by bin(Datetime, Period)
                // Metrics
                // | where Datetime between ( now(-10m) .. now() )
                // | order by Datetime asc
                // | summarize StartW=min(Datetime), EndW=max(Datetime), Labels=tostring(any(Labels)), Samples=make_list( pack( 'Timestamp', Timestamp, 'Value', Value ) ) by LabelsHash
                // | take 10

        }
    }
}
