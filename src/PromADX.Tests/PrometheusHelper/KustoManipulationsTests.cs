using System.Reflection;
using NUnit.Framework;
using PromADX.PrometheusHelper;
using PromADX.PrometheusHelper.Models;
using Prometheus;

namespace PromADX.Tests.PrometheusHelper
{
    public class KustoManipulationsTests
    {
        [Test]
        public void ToKustoRow_PrometheusTimeSeriesObjectShouldReturnValidKustoRowObject()
        {
            // Arrange
            var ts = new Prometheus.TimeSeries()
            {
                Labels =
                {
                    new Prometheus.Label()
                    {
                        Name = "__name__",
                        Value = "TestName"
                    },
                    new Prometheus.Label()
                    {
                        Name = "job",
                        Value = "TestJob"
                    }
                },
                Samples =
                {
                    new Prometheus.Sample()
                    {
                        Timestamp = 123,
                        Value = 0
                    }
                }
            };
            var expectedKustoRow = new KustoRow()
            {
                Instance = null,
                Job = "TestJob",
                Labels = @"{""__name__"":""TestName"",""job"":""TestJob""}",
                Name = "TestName",
                Timestamp = 123,
                Value = 0,
            };
            // Act
            var resultKustoRow = KustoManipulations.ToKustoRow(ts);
            // Assert
            foreach (PropertyInfo property in typeof(KustoRow).GetProperties())
            {
                Assert.AreEqual(
                    property.GetValue(expectedKustoRow, null), 
                    property.GetValue(resultKustoRow, null),
                    $"KustoRow property '{property.Name}' are not equal"
                    );
            }
        }

        [TestCase("__name__", LabelMatcher.Types.Type.Eq, "1", "( Name == '1' )")]
        [TestCase("up", LabelMatcher.Types.Type.Eq, "0", "( tostring(Labels.up) == '0' )")]
        [TestCase("up", LabelMatcher.Types.Type.Nre, "0", "( tostring(Labels.up) !contains '0' )")]
        public void ToKustoExpression_PrometheusQueryShouldReturnValidKustoQuery(string name,
            LabelMatcher.Types.Type type, string val, string expected)
        {
            // Arrange
            // Act
            var kql = KustoManipulations.ToKustoExpression(name, type, val);
            // Assert
            CollectionAssert.AreEqual(expected, kql);
        }
    }
}