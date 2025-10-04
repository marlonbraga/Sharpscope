using System.Runtime.Serialization;
using Sharpscope.Domain.Models;

namespace Sharpscope.Test.TestUtils;

public static class MetricsResultStub
{
    public static MetricsResult Create()
        => (MetricsResult)FormatterServices.GetUninitializedObject(typeof(MetricsResult));
}
