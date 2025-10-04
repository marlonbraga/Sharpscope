using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Sharpscope.Infrastructure.Sources;
using Xunit;

namespace sharpscope.test.InfrastructureTests.Sources;

public sealed class ProcessRunnerTests
{
    [Fact(DisplayName = "RunAsync executes a simple command and returns stdout with exit code 0")]
    public async Task RunAsync_SimpleCommand_Succeeds()
    {
        // Arrange
        var runner = new ProcessRunner();

        // Use "dotnet --version" as a harmless, cross-platform command
        var file = "dotnet";
        var args = "--version";

        // Act
        var result = await runner.RunAsync(file, args, workingDirectory: null, timeout: TimeSpan.FromMinutes(1), ct: CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.ExitCode.ShouldBe(0);
        result.StdOut.ShouldNotBeNullOrWhiteSpace();
        result.StdErr.ShouldNotBeNull(); // may be empty
    }
}
