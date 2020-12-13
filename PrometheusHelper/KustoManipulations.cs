using System.Collections.Generic;
using Newtonsoft.Json;
using PromADX.PrometheusHelper.Models;
using Prometheus;

namespace PromADX.PrometheusHelper
{
    public static class KustoRowConverter
    {
        public static KustoRow ToKustoRow(TimeSeries ts)
        {
            var kustoRow = new KustoRow
            {
                Timestamp = ts.Samples[0].Timestamp,
                Value = ts.Samples[0].Value
            };

            var labelsDict = new SortedDictionary<string, string>();
            foreach (var label in ts.Labels)
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