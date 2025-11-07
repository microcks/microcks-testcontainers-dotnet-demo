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

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Order.Service.UseCases.Model;

namespace Order.Service.UseCases;

public class OrderEventPublisher : IEventPublisher
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<OrderEventPublisher> _logger;
    private const string Topic = "orders-created";

    public OrderEventPublisher(IProducer<string, string> producer, ILogger<OrderEventPublisher> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    public async Task PublishOrderCreatedAsync(OrderEvent orderEvent, CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string>
        {
            Key = orderEvent.Order.CustomerId,
            Value = JsonSerializer.Serialize(orderEvent)
        };
        try
        {
            var result = await _producer.ProduceAsync(Topic, message, cancellationToken);
            _logger.LogInformation("OrderEvent published to Kafka: {Offset}", result.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to publish OrderEvent to Kafka");
            throw;
        }
    }
}
