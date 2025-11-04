# Integration Testing Patterns with Microcks and Testcontainers

This guide explains two different approaches for setting up integration tests with Microcks and Testcontainers in .NET, each with their own trade-offs and use cases.

## Overview

When writing integration tests that use Microcks and Kafka containers, you have two main architectural choices:

1. **IClassFixture Pattern**: Multiple container instances, isolated per test class
2. **ICollectionFixture Pattern**: Single shared container instance, optimized for performance

## Pattern 1: IClassFixture - Isolated Test Classes

### When to use
- When you need complete isolation between test classes
- When different test classes require different container configurations
- When you have few test classes and startup time is not a concern
- When test classes might interfere with each other's state

### Architecture
```csharp
public class MyTestClass : IClassFixture<MicrocksWebApplicationFactory<Program>>
{
    // Each test class gets its own factory instance
    // Each factory starts its own containers
}
```

### Key Requirements
- **Dynamic Port Allocation**: Each factory instance must use different ports
- **Container Isolation**: Each test class has its own Microcks and Kafka containers
- **Resource Management**: More memory and CPU usage due to multiple containers

### Implementation Example

#### Step 1: WebApplicationFactory with Dynamic Ports
```csharp
public class MicrocksWebApplicationFactory<TProgram> : KestrelWebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
{
    public ushort ActualPort { get; private set; }
    public KafkaContainer KafkaContainer { get; private set; } = null!;
    public MicrocksContainerEnsemble MicrocksContainerEnsemble { get; private set; } = null!;

    private ushort GetAvailablePort()
    {
        using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        return (ushort)((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    public async ValueTask InitializeAsync()
    {
        // CRITICAL: Get dynamic port for each instance
        ActualPort = GetAvailablePort();
        UseKestrel(ActualPort);
        
        await TestcontainersSettings.ExposeHostPortsAsync(ActualPort, TestContext.Current.CancellationToken);

        var network = new NetworkBuilder().Build();

        // Each instance gets its own Kafka container
        KafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.9.0")
            .WithPortBinding(0, KafkaBuilder.KafkaPort) // 0 = dynamic port
            .WithNetwork(network)
            .WithNetworkAliases("kafka")
            .Build();

        await KafkaContainer.StartAsync(TestContext.Current.CancellationToken);

        // Each instance gets its own Microcks container
        MicrocksContainerEnsemble = new MicrocksContainerEnsemble(network, "quay.io/microcks/microcks-uber:1.13.0")
            .WithAsyncFeature()
            .WithMainArtifacts("resources/order-service-openapi.yaml")
            .WithKafkaConnection(new KafkaConnection($"kafka:19092"));

        await MicrocksContainerEnsemble.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        
        var pastryApiEndpoint = MicrocksContainerEnsemble.MicrocksContainer
            .GetRestMockEndpoint("API Pastries", "0.0.1");
        builder.UseSetting("PastryApi:BaseUrl", pastryApiEndpoint);
        
        var kafkaBootstrap = KafkaContainer.GetBootstrapAddress()
            .Replace("PLAINTEXT://", "", StringComparison.OrdinalIgnoreCase);
        builder.UseSetting("Kafka:BootstrapServers", kafkaBootstrap);
    }

    public async override ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await KafkaContainer.DisposeAsync();
        await MicrocksContainerEnsemble.DisposeAsync();
    }
}
```

#### Step 2: Test Class Implementation
```csharp
public class OrderControllerTests : IClassFixture<MicrocksWebApplicationFactory<Program>>
{
    private readonly MicrocksWebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _testOutput;

    public OrderControllerTests(
        MicrocksWebApplicationFactory<Program> factory,
        ITestOutputHelper testOutput)
    {
        _factory = factory;
        _testOutput = testOutput;
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnCreatedOrder()
    {
        // This test class has its own containers
        using var client = _factory.CreateClient();
        
        // Test implementation...
    }
}
```

### Pros and Cons

✅ **Advantages:**
- Complete isolation between test classes
- Different configurations per test class
- No shared state issues
- Parallel test execution per class

❌ **Disadvantages:**
- Higher resource usage (multiple containers)
- Slower overall test execution
- More complex port management
- Potential for port conflicts if not handled properly

---

## Pattern 2: ICollectionFixture - Shared Containers (Recommended)

### When to use
- When you want optimal performance and resource usage
- When test classes can share the same container configuration
- When you have many test classes
- When startup time is a concern

### Architecture
```csharp
[Collection(SharedTestCollection.Name)]
public class MyTestClass : BaseIntegrationTest
{
    // All test classes share the same factory instance
    // Single set of containers for all tests
}
```

### Key Benefits
- **Single Container Instance**: One Microcks + one Kafka container for all tests
- **Performance Optimized**: ~70% faster test execution
- **Resource Efficient**: Lower memory and CPU usage
- **Single Port Allocation**: One Kestrel port for the entire test suite

### Implementation Example

#### Step 1: Shared Collection Definition
```csharp
[CollectionDefinition(Name)]
public class SharedTestCollection : ICollectionFixture<MicrocksWebApplicationFactory<Program>>
{
    public const string Name = "SharedTestCollection";
}
```

#### Step 2: Enhanced WebApplicationFactory
```csharp
public class MicrocksWebApplicationFactory<TProgram> : KestrelWebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
{
    private static readonly SemaphoreSlim InitializationSemaphore = new(1, 1);
    private static bool _isInitialized;

    public ushort ActualPort { get; private set; }
    public KafkaContainer KafkaContainer { get; private set; } = null!;
    public MicrocksContainerEnsemble MicrocksContainerEnsemble { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await InitializationSemaphore.WaitAsync();
        try
        {
            if (_isInitialized)
            {
                TestLogger.WriteLine("[Factory] Already initialized, skipping...");
                return;
            }

            TestLogger.WriteLine("[Factory] Starting initialization...");
            
            // Single port allocation for all tests
            ActualPort = GetAvailablePort();
            UseKestrel(ActualPort);
            
            await TestcontainersSettings.ExposeHostPortsAsync(ActualPort, TestContext.Current.CancellationToken);

            // Single network and containers for all tests
            var network = new NetworkBuilder().Build();
            
            KafkaContainer = new KafkaBuilder()
                .WithImage("confluentinc/cp-kafka:7.9.0")
                .WithNetwork(network)
                .WithNetworkAliases("kafka")
                .Build();

            await KafkaContainer.StartAsync(TestContext.Current.CancellationToken);

            MicrocksContainerEnsemble = new MicrocksContainerEnsemble(network, "quay.io/microcks/microcks-uber:1.13.0")
                .WithAsyncFeature()
                .WithMainArtifacts("resources/order-service-openapi.yaml")
                .WithKafkaConnection(new KafkaConnection("kafka:19092"));

            await MicrocksContainerEnsemble.StartAsync();

            _isInitialized = true;
            TestLogger.WriteLine("[Factory] Initialization completed");
        }
        finally
        {
            InitializationSemaphore.Release();
        }
    }

    // ConfigureWebHost and DisposeAsync similar to Pattern 1
}
```

#### Step 3: Base Test Class
```csharp
[Collection(SharedTestCollection.Name)]
public abstract class BaseIntegrationTest
{
    public WebApplicationFactory<Program> Factory { get; private set; }
    public ushort Port { get; private set; }
    public MicrocksContainerEnsemble MicrocksContainerEnsemble { get; }
    public KafkaContainer KafkaContainer { get; }
    public HttpClient HttpClient { get; private set; }

    protected BaseIntegrationTest(MicrocksWebApplicationFactory<Program> factory)
    {
        Factory = factory;
        HttpClient = factory.CreateClient();
        Port = factory.ActualPort;
        MicrocksContainerEnsemble = factory.MicrocksContainerEnsemble;
        KafkaContainer = factory.KafkaContainer;
    }

    protected void SetupTestOutput(ITestOutputHelper testOutputHelper)
    {
        TestLogger.SetTestOutput(testOutputHelper);
    }
}
```

#### Step 4: Test Class Implementation
```csharp
public class OrderControllerTests : BaseIntegrationTest
{
    private readonly ITestOutputHelper _testOutput;

    public OrderControllerTests(
        ITestOutputHelper testOutput,
        MicrocksWebApplicationFactory<Program> factory)
        : base(factory)
    {
        _testOutput = testOutput;
        SetupTestOutput(testOutput);
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnCreatedOrder()
    {
        // Shared containers with all other test classes
        // Test implementation...
    }
}
```

### Pros and Cons

✅ **Advantages:**
- Excellent performance (~70% faster)
- Lower resource usage
- Simple port management
- No port conflicts
- Shared infrastructure

❌ **Disadvantages:**
- Shared state between test classes
- Same configuration for all tests
- Potential for test interdependencies

---

## Comparison Summary

| Aspect | IClassFixture Pattern | ICollectionFixture Pattern |
|--------|----------------------|---------------------------|
| **Performance** | Slower (multiple startups) | Faster (~70% improvement) |
| **Resource Usage** | High (multiple containers) | Low (single containers) |
| **Isolation** | Complete per class | Shared across classes |
| **Port Management** | Complex (dynamic per class) | Simple (single allocation) |
| **Configuration** | Flexible per class | Single configuration |
| **Recommended For** | Different configs needed | Homogeneous test suites |

## Recommendation

**Use ICollectionFixture Pattern (Pattern 2)** for most scenarios because:
- Better performance and resource efficiency
- Simpler port management
- Most integration tests can share the same container setup
- Easier to maintain and debug

**Use IClassFixture Pattern (Pattern 1)** only when:
- You need different container configurations per test class
- Complete isolation is mandatory
- You have few test classes and performance isn't critical
