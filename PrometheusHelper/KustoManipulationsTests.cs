using NUnit.Framework;
using PromADX.PrometheusHelper;
using Prometheus;

namespace PromADX.Tests.PrometheusHelper
{
    public class KustoManipulationsTests
    {
        [TestCase("up", LabelMatcher.Types.Type.Eq, "0","up==0")]
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