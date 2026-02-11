using Sharpscope.Infrastructure.Integrations;
using Shouldly;
using Xunit;

namespace Sharpscope.Test.InfrastructureTests.Integrations;

public sealed class IntegrationSecretRedactorTests
{
    [Theory]
    [InlineData("Server=localhost;Database=Db;User Id=sa;Password=SuperSecret123!;", "Password=***")]
    [InlineData("Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SuperSecretKey=", "SharedAccessKey=***")]
    [InlineData("DefaultEndpointsProtocol=https;AccountName=acc;AccountKey=SuperSecretKey==;EndpointSuffix=core.windows.net", "AccountKey=***")]
    [InlineData("localhost:6379,password=RedisSecret!", "password=***")]
    [InlineData("AccountEndpoint=https://example.documents.azure.com:443/;AccountKey=CosmosSecretKey==;", "AccountKey=***")]
    [InlineData("Endpoint=https://example.com;Key=ApiSecretKey;", "Key=***")]
    public void Redacts_ConnectionStrings(string input, string expectedFragment)
    {
        var redacted = IntegrationSecretRedactor.Redact(input);

        redacted.ShouldNotBeNull();
        redacted!.ShouldContain(expectedFragment, Case.Insensitive);
        redacted.ShouldNotContain("SuperSecret", Case.Insensitive);
        redacted.ShouldNotContain("RedisSecret", Case.Insensitive);
    }

    [Fact]
    public void Redacts_UserPass_In_Uri()
    {
        var input = "mongodb://user:MongoSecret@localhost:27017";
        var redacted = IntegrationSecretRedactor.Redact(input);

        redacted.ShouldNotBeNull();
        redacted!.ShouldBe("mongodb://user:***@localhost:27017");
    }

    [Fact]
    public void Redacts_Query_Params()
    {
        var input = "https://example.com?sig=super-secret&se=2020-01-01";
        var redacted = IntegrationSecretRedactor.Redact(input);

        redacted.ShouldNotBeNull();
        redacted!.ShouldContain("sig=***", Case.Insensitive);
        redacted.ShouldNotContain("super-secret", Case.Insensitive);
    }
}
