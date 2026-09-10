using PartnerIntegration.Application.Transactions;
using PartnerIntegration.Domain.Transactions;
using PartnerIntegration.Infrastructure.Messaging;
using Xunit;

namespace PartnerIntegration.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_has_no_application_or_framework_dependencies()
    {
        var references = typeof(PartnerTransaction).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => reference.Name!.StartsWith("PartnerIntegration.")
            || reference.Name.StartsWith("Microsoft.AspNetCore") || reference.Name.StartsWith("RabbitMQ")
            || reference.Name.StartsWith("Polly"));
    }

    [Fact]
    public void Application_depends_inward_on_domain_only()
    {
        var references = typeof(SubmitTransaction).Assembly.GetReferencedAssemblies();
        Assert.Contains(references, reference => reference.Name == "PartnerIntegration.Domain");
        Assert.DoesNotContain(references, reference => reference.Name == "PartnerIntegration.Api"
            || reference.Name == "PartnerIntegration.Infrastructure" || reference.Name!.StartsWith("Microsoft.AspNetCore")
            || reference.Name.StartsWith("RabbitMQ") || reference.Name.StartsWith("Polly"));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        Assert.DoesNotContain(typeof(RabbitMqPublisher).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == "PartnerIntegration.Api");
    }
}
