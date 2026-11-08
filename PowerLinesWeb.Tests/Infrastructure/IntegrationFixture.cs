using NUnit.Framework;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace PowerLinesWeb.Tests.Infrastructure;

[Category("Integration")]
public abstract class IntegrationFixture
{
    readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:11.4-alpine")
        .WithDatabase("power_lines_test").WithUsername("postgres").WithPassword("postgres").Build();
    RabbitMqContainer rabbit;
    protected virtual bool NeedsRabbit => false;
    protected string DatabaseConnection => postgres.GetConnectionString();
    protected string RabbitConnection => rabbit.GetConnectionString();

    [OneTimeSetUp]
    public async Task StartInfrastructure()
    {
        try
        {
            await postgres.StartAsync();
            if (NeedsRabbit)
            {
                rabbit = new RabbitMqBuilder("rabbitmq:3-management-alpine").Build();
                await rabbit.StartAsync();
            }
        }
        catch
        {
            await StopInfrastructure();
            throw;
        }
    }

    [OneTimeTearDown]
    public async Task StopInfrastructure()
    {
        try { if (rabbit != null) await rabbit.DisposeAsync(); }
        finally { await postgres.DisposeAsync(); }
    }
}
