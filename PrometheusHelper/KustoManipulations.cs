using System.Collections.Generic;
using Newtonsoft.Json;
using PromADX.PrometheusHelper.Models;
using Prometheus;

namespace PromADX.PrometheusHelper
{
    public static class KustoManipulations
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


        public static string ToKustoExpression(string name, LabelMatcher.Types.Type type, string value)
        {
            var keyMap = new Dictionary<string, string>()
            {
                {"__name__", "Name"},
                {"job", "Job"},
                {"instance", "Instance"}
            };

            var resultName = keyMap.ContainsKey(name) ? keyMap[name] : $"tostring(Labels.{name})";

            const string queryTemplate = "( {0} {1} '{2}' )";

            return type switch
            {
                LabelMatcher.Types.Type.Eq => string.Format(queryTemplate, resultName, "==", value),
                LabelMatcher.Types.Type.Neq => string.Format(queryTemplate, resultName, "!=", value),
                LabelMatcher.Types.Type.Re => string.Format(queryTemplate, resultName, "matches regex", value),
                LabelMatcher.Types.Type.Nre => string.Format(queryTemplate, resultName, "!contains", value),
                _ => string.Empty
            };
        }
    }
}