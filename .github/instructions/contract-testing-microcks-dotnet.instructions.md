---
applyTo: 'tests/**/*.cs'
description: Guidelines for writing contract tests with Microcks and Testcontainers in .NET.
---

# Contract Testing with Microcks in .NET

## Purpose
This instruction defines how to implement contract tests for REST/SOAP APIs using Microcks and Testcontainers in .NET projects. It ensures that your API implementation conforms to the contract and that contract changes are detected early.

## Rules or Guidelines
- Use `Testcontainers` to start a Microcks container ensemble and a Kafka container in your test setup.
- **CRITICAL**: Use `TestcontainersSettings.ExposeHostPortsAsync()` to expose the dynamically allocated application port before building Microcks containers.
- Always allocate a free port for Kestrel using a socket, and pass it to `UseKestrel(port)` before exposing host ports and starting containers.
- Expose host ports **before** creating MicrocksContainerEnsemble or KafkaContainer to ensure proper communication.
   - Don't call `.Build()` on properties if you need to expose host ports.
   - Don't call `.Build()` before exposing host ports methods.
- Start a message broker container (e.g., Kafka or AMQP) and pass its connection to Microcks using the appropriate method (e.g., `.WithKafkaConnection(...)` or `.WithAMQPConnection(...)`).
- Import all relevant contract and collection artifacts into Microcks at container startup using the provided methods (e.g., `.WithMainArtifacts()`, `.WithSecondaryArtifacts()`).
- Note: If you use AMQP or another broker, adapt the connection setup accordingly (do not use Kafka-specific methods).
- Configure your application under test to call the Microcks mock endpoint, including third-party APIs (e.g., Pastry API) using `builder.UseSetting("PastryApi:BaseUrl", ...)` in `ConfigureWebHost`.
- Use `KestrelWebApplicationFactory` for .NET versions before .NET 10 to enable real HTTP server testing.
- For .NET 10+, use the built-in Kestrel support in WebApplicationFactory, but always call `UseKestrel()` for consistency.
- Write tests that call your API and assert responses against the contract served by Microcks.
- Clean up containers after tests using `IAsyncLifetime`.
- Document the contract files, third-party artifacts, and Microcks/Kafka versions used in the test file header.

## Best Practices
- Use clear, explicit test names describing the contract being validated.
- Use `IAsyncLifetime` in xUnit for container lifecycle management.
- Keep contract files versioned and reviewed in your repository.
- Prefer isolated, repeatable tests that do not depend on external state.
- Use network aliases for container-to-container communication.

## Examples

### Example: Order API Contract Test
This example demonstrates how to set up a contract test for an Order API using Microcks and Testcontainers in .NET.
```csharp
public class OrderApiContractTest : BaseIntegrationTest
{
    private readonly ITestOutputHelper TestOutputHelper;

    public OrderApiContractTest(ITestOutputHelper testOutputHelper, MicrocksWebApplicationFactory<Program> factory)
        : base(factory)
    {
        TestOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task TestOpenApiContract()
    {
        TestRequest request = new()
        {
            ServiceId = "Order Service API:0.1.0",
            RunnerType = TestRunnerType.OPEN_API_SCHEMA,
            TestEndpoint = "http://host.testcontainers.internal:" + Port + "/api",
            // FilteredOperations can be used to limit the operations to test
            // FilteredOperations = ["GET /orders", "POST /orders"]
        };

        var testResult = await this.MicrocksContainer.TestEndpointAsync(request);

        // You may inspect complete response object with following:
        var json = JsonSerializer.Serialize(testResult, new JsonSerializerOptions { WriteIndented = true });
        TestOutputHelper.WriteLine(json);

        Assert.False(testResult.InProgress, "Test should not be in progress");
        Assert.True(testResult.Success, "Test should be successful");
    }
}
```

### Custom WebApplicationFactory: .NET 10+ and below

For both .NET 10+ and below, you must call `UseKestrel()` in your `MicrocksWebApplicationFactory` to ensure real HTTP server testing for contract tests. The only difference is the base class:

For .NET 10 and above, inherit from `WebApplicationFactory<TProgram>`:
```csharp
public class MicrocksWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    public MicrocksWebApplicationFactory()
    {
        // Even in .NET 10+, call UseKestrel() for consistency and explicitness
        UseKestrel();
    }
}
```

For .NET 9 and below, inherit from `KestrelWebApplicationFactory<TProgram>`:
```csharp
public class MicrocksWebApplicationFactory<TProgram> : KestrelWebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
{
    private const string MicrocksImage = "quay.io/microcks/microcks-uber:1.12.1";

    public KafkaContainer KafkaContainer { get; private set; } = null!;
    public MicrocksContainerEnsemble MicrocksContainerEnsemble { get; private set; } = null!;
    public ushort ActualPort { get; private set; }
    public HttpClient? HttpClient { get; private set; }

    private ushort GetAvailablePort()
    {
        using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        return (ushort)((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    public async ValueTask InitializeAsync()
    {
        ActualPort = GetAvailablePort();
        UseKestrel(ActualPort);
        await TestcontainersSettings.ExposeHostPortsAsync(ActualPort, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        var network = new NetworkBuilder().Build();
        KafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.9.0")
            .WithNetwork(network)
            .WithNetworkAliases("kafka")
            .WithListener("kafka:19092")
            .Build();

        await this.KafkaContainer.StartAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        this.MicrocksContainerEnsemble = new MicrocksContainerEnsemble(network, MicrocksImage)
            .WithAsyncFeature()
            .WithMainArtifacts("resources/order-service-openapi.yaml", "resources/order-events-asyncapi.yaml", "resources/third-parties/apipastries-openapi.yaml")
            .WithSecondaryArtifacts("resources/order-service-postman-collection.json", "resources/third-parties/apipastries-postman-collection.json")
            .WithKafkaConnection(new KafkaConnection($"kafka:19092"));

        await this.MicrocksContainerEnsemble.StartAsync()
            .ConfigureAwait(true);

        HttpClient = this.CreateClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        var microcksContainer = this.MicrocksContainerEnsemble.MicrocksContainer;
        var pastryApiEndpoint = microcksContainer.GetRestMockEndpoint("API Pastries", "0.0.1");
        builder.UseSetting("PastryApi:BaseUrl", $"{pastryApiEndpoint}/");
    }

    public async override ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await this.KafkaContainer.DisposeAsync();
        await this.MicrocksContainerEnsemble.DisposeAsync();
    }
}
```

> **Note:** In .NET 10+, `WebApplicationFactory` supports Kestrel natively, but calling `UseKestrel()` is **mandatory** so that Testcontainers can access the host application.

### Base Integration Test with Proper Initialization
```csharp
public class BaseIntegrationTest : IClassFixture<MicrocksWebApplicationFactory<Program>>
{
    public WebApplicationFactory<Program> Factory { get; private set; }
    public ushort Port { get; private set; }
    public MicrocksContainerEnsemble MicrocksContainerEnsemble { get; }
    public MicrocksContainer MicrocksContainer => MicrocksContainerEnsemble.MicrocksContainer;

    protected BaseIntegrationTest(MicrocksWebApplicationFactory<Program> factory)
    {
        Factory = factory;
        Port = factory.ActualPort;
        MicrocksContainerEnsemble = factory.MicrocksContainerEnsemble;
    }
}
```

### KestrelWebApplicationFactory for .NET 9 and below (Base Example)

```csharp
// This file is included as a base reference because enabling Kestrel in integration tests for .NET 9 and below is exceptional and non-standard.
// Use this as the canonical example for such scenarios.
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;

namespace Order.Service.Tests;

/// <summary>
/// Custom WebApplicationFactory that enables Kestrel for integration tests in .NET 9 and below.
/// This is included in base as an exceptional case for contract/integration testing.
/// </summary>
public class KestrelWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    private IHost? _host;
    private bool _useKestrel;
    private ushort _kestrelPort = 0;

    public Uri ServerAddress
    {
        get
        {
            EnsureServer();
            return ClientOptions.BaseAddress;
        }
    }

    /// <summary>
    /// Configures the factory to use Kestrel server with the specified port.
    /// </summary>
    public KestrelWebApplicationFactory<TProgram> UseKestrel(ushort port = 0)
    {
        _useKestrel = true;
        _kestrelPort = port;
        return this;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();

        if (_useKestrel)
        {
            builder.ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder.UseKestrel(options =>
                {
                    options.Listen(IPAddress.Any, _kestrelPort, listenOptions =>
                    {
                        if (Debugger.IsAttached)
                        {
                            listenOptions.UseConnectionLogging();
                        }
                    });
                });
            });

            _host = builder.Build();
            _host.Start();

            var server = _host.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>();
            ClientOptions.BaseAddress = addresses!.Addresses.Select(x => new Uri(x)).Last();

            testHost.Start();
            return testHost;
        }

        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _host?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void EnsureServer()
    {
        if (_host is null && _useKestrel)
        {
            using var _ = CreateDefaultClient();
        }
    }
}
```

## References
- https://microcks.io
- https://dotnet.testcontainers.org/
- https://github.com/microcks/microcks-testcontainers-dotnet