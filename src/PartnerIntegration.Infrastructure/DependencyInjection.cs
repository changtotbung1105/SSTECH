using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PartnerIntegration.Application.Partners;
using PartnerIntegration.Application.Transactions;
using PartnerIntegration.Infrastructure.Partners;
using PartnerIntegration.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace PartnerIntegration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            Uri = new Uri(configuration["RabbitMq:Uri"]
                ?? throw new InvalidOperationException("RabbitMq:Uri is required.")),
            AutomaticRecoveryEnabled = true
        });
        services.AddSingleton<ITransactionPublisher, RabbitMqPublisher>();
        services.AddHttpClient<IPartnerVerifier, PartnerVerifier>(client =>
        {
            client.BaseAddress = new Uri(configuration["PartnerApi:BaseUrl"] ?? "http://localhost:8080/");
            client.Timeout = Timeout.InfiniteTimeSpan;
        }).AddStandardResilienceHandler(options => PartnerResilience.Configure(options));
        return services;
    }
}
