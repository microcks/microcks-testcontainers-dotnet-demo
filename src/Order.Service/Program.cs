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
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Order.Service.Client;
using Order.Service.Endpoints;
using Order.Service.UseCases;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<OrderUseCase>();
var configuration = builder.Configuration;
var pastryApiSection = configuration.GetRequiredSection("PastryApi");
var pastryApiUrl = pastryApiSection.GetValue<string>("BaseUrl");
if (string.IsNullOrWhiteSpace(pastryApiUrl))
{
    throw new InvalidOperationException("PastryApi:BaseUrl configuration is required and cannot be null or empty.");
}


builder.Services.AddHttpClient<PastryAPIClient>(opt =>
{
    opt.BaseAddress = new Uri(pastryApiUrl + "/");
});

// Services for API metadata
builder.Services.AddOpenApi();

builder.Services.AddProblemDetails();
// ⬆️ Add problem details for better error handling

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapOrderEndpoints();

app.Run();

public partial class Program { }
