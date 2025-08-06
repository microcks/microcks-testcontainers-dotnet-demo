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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Order.Service.UseCases;
using Order.Service.UseCases.Model;
using OrderModel = Order.Service.UseCases.Model.Order;

namespace Order.Service.Endpoints;

public static class OrderEndpoints
{
    public static WebApplication MapOrderEndpoints(this WebApplication app)
    {
        var root = app.MapGroup("/api/orders")
            .WithTags("Orders")
            .WithDescription("Order management endpoints");

        _ = root.MapPost("/", CreateOrder)
            .Produces<OrderModel>();
        return app;
    }


    private static async Task<IResult> CreateOrder(
        OrderInfo orderInfo,
        OrderUseCase orderUseCase)
    {
        OrderModel createdOrder;
        try
        {
            createdOrder = await orderUseCase.PlaceOrderAsync(orderInfo);

            return Results.Created($"/api/orders/{createdOrder.Id}", createdOrder);
        }
        catch (UnavailablePastryException upe)
        {
            // We have to return a 422 (unprocessable) with correct expected type.
            return Results.UnprocessableEntity(new UnavailableProduct(upe.Product, upe.Message));
        }
        catch (Exception e)
        {
            return Results.Problem(e.Message, statusCode: StatusCodes.Status500InternalServerError);
        }

    }

}
