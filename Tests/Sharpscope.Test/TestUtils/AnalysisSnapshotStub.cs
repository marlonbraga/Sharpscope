using System;
using Sharpscope.Domain.Models;

namespace Sharpscope.Test.TestUtils;

public static class AnalysisSnapshotStub
{
    public static AnalysisSnapshot Create()
        => new(
            Metadata: new AnalysisMetadata(
                RepoUrlOrPath: "stub",
                CommitSha: null,
                Branch: null,
                TimestampUtc: DateTimeOffset.UtcNow,
                ToolVersion: "test",
                MetricsSchemaVersion: "1",
                IntegrationsSchemaVersion: "1",
                IntegrationProfile: "work"
            ),
            Graph: CodeGraph.Empty,
            Metrics: MetricsSnapshot.Empty,
            Integrations: IntegrationsSnapshot.Empty
        );
}
