//
// Copyright The Microcks Authors.
//
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0 
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Order.Service.UseCases;
using Order.Service.UseCases.Model;
using OrderModel = Order.Service.UseCases.Model.Order;

using Xunit;

namespace Order.Service.Tests.UseCases;

public class OrderEventListenerTests : BaseIntegrationTest
{
    private readonly ITestOutputHelper TestOutputHelper;

    public OrderEventListenerTests(
        ITestOutputHelper testOutputHelper,
        MicrocksWebApplicationFactory<Program> factory)
        : base(factory)
    {
        TestOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task TestEventIsConsumedAndProcessedByService()
    {
        // Arrange
        const string expectedOrderId = "123-456-789";
        const string expectedCustomerId = "lbroudoux";
        const int expectedProductCount = 2;

        // Start the HostedService manually for the test
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var consumerTask = StartOrderEventConsumerAsync(cts.Token);

        OrderModel? order = null;
        // Act & Assert - Polling pattern similar to the Java example
        try
        {
            order = await PollForOrderProcessingAsync(expectedOrderId, cts);

            Assert.NotNull(order);
            // Verify the order properties match expected values
            Assert.Equal(expectedCustomerId, order.CustomerId);
            Assert.Equal(OrderStatus.Validated, order.Status);
            Assert.Equal(expectedProductCount, order.ProductQuantities.Count);
        }
        catch (TimeoutException)
        {
            Assert.Fail("The expected Order was not received/processed in expected delay");
        }
        finally
        {
            // Stop the consumer - if not already stopped
            cts.Cancel();
            try
            {
                await consumerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when we cancel
                TestOutputHelper.WriteLine("Order event consumer stopped");
            }
        }
    }

    private async Task<OrderModel> PollForOrderProcessingAsync(
        string expectedOrderId,
        CancellationTokenSource cts)
    {
        var pollDelay = TimeSpan.FromMilliseconds(400);
        var pollInterval = TimeSpan.FromMilliseconds(400);
        var startTime = DateTime.UtcNow;

        var orderUseCase = Factory.Services.GetRequiredService<OrderUseCase>();
        // Initial delay
        await Task.Delay(pollDelay, TestContext.Current.CancellationToken);

        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(4))
        {
            try
            {
                // Simulate getting order by ID (this would normally call a GET endpoint)
                var order = await orderUseCase.GetOrderAsync(expectedOrderId, TestContext.Current.CancellationToken);

                if (order is not null)
                {
                    TestOutputHelper.WriteLine($"Order {order.Id} successfully processed!");
                    cts.Cancel(); // Cancel the consumer after successful processing
                    return order;
                }
            }
            catch (OrderNotFoundException)
            {
                // Continue polling until timeout
                TestOutputHelper.WriteLine($"Order {expectedOrderId} not found yet, continuing to poll...");
            }

            await Task.Delay(pollInterval, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Order {expectedOrderId} was not processed within the expected timeout.");
    }

    private async Task StartOrderEventConsumerAsync(CancellationToken cancellationToken)
    {
        // Create the OrderEventConsumer manually
        var logger = Factory.Services.GetRequiredService<ILogger<OrderEventConsumerHostedService>>();
        var orderEventConsumer = new OrderEventConsumerHostedService(logger, Factory.Services);

        TestOutputHelper.WriteLine("Starting OrderEventConsumer for test");

        try
        {
            await orderEventConsumer.StartAsync(cancellationToken);

            // Keep it running until cancelled
            var tcs = new TaskCompletionSource<bool>();
            using var registration = cancellationToken.Register(() => tcs.SetResult(true));
            await tcs.Task;
        }
        finally
        {
            await orderEventConsumer.StopAsync(CancellationToken.None);
            orderEventConsumer.Dispose();
            TestOutputHelper.WriteLine("OrderEventConsumer stopped");
        }
    }
}
