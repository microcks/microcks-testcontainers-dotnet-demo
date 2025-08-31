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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Order.Service.Client;
using Order.Service.Client.Model;
using Order.Service.UseCases.Model;
using OrderModel = Order.Service.UseCases.Model.Order;

namespace Order.Service.UseCases;

public class OrderUseCase
{
    private readonly ILogger<OrderUseCase> _logger;
    private readonly PastryAPIClient _pastryAPIClient;

    public OrderUseCase(ILogger<OrderUseCase> logger, PastryAPIClient pastryAPIClient)
    {
        _logger = logger;
        _pastryAPIClient = pastryAPIClient;
    }

    /// <summary>
    /// This method will check that an Order can be actually placed and persisted. A full implementation
    /// will probably check stocks, customer loyalty, payment methods, shipping details, etc... For
    /// sake of simplicity, we'll just check that products (here pastries) are all available.
    /// </summary>
    /// <param name="orderInfo">The order information.</param>
    /// <returns>A created Order with incoming info, new unique identifier and created status.</returns>
    /// <exception cref="UnavailablePastryException">Thrown when a pastry is unavailable.</exception>
    /// <exception cref="Exception">Thrown for general errors.</exception>
    public async Task<OrderModel> PlaceOrderAsync(OrderInfo orderInfo)
    {
        // For all products in order, check the availability calling the Pastry API.
        var availabilityTasks = new Dictionary<string, Task<bool>>();

        foreach (var productQuantity in orderInfo.ProductQuantities)
        {
            availabilityTasks[productQuantity.ProductName] = CheckPastryAvailabilityAsync(productQuantity.ProductName);
        }

        // Wait for all tasks to finish.
        await Task.WhenAll(availabilityTasks.Values);

        // If one pastry is marked as unavailable, throw a business exception.
        foreach (var kvp in availabilityTasks)
        {
            var productName = kvp.Key;
            var isAvailable = await kvp.Value;

            if (!isAvailable)
            {
                throw new UnavailablePastryException(productName, $"Pastry '{productName}' is unavailable.");
            }
        }

        // Everything is available! Create a new order.
        var order = new OrderModel
        {
            CustomerId = orderInfo.CustomerId,
            ProductQuantities = orderInfo.ProductQuantities,
            TotalPrice = orderInfo.TotalPrice
        };

        return order;
    }

    private async Task<bool> CheckPastryAvailabilityAsync(string pastryName, CancellationToken cancellationToken = default)
    {
        Pastry pastry = await _pastryAPIClient.GetPastryByNameAsync(pastryName, cancellationToken);

        return await Task.FromResult<bool>(pastry.IsAvailable());
    }
}
