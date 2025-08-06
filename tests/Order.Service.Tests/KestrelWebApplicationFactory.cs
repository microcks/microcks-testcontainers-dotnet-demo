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
/// This class is a custom WebApplicationFactory that allows the tests to run against a real Kestrel server
///
/// Normally, In .NET 10, UseKestrel is supported in WebApplicationFactory, but before .NET 10
/// it was not possible to use Kestrel with WebApplicationFactory.
/// </summary>
/// <typeparam name="TProgram"></typeparam>
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
    /// <param name="port">The port to listen on. If not specified or 0, the system will choose an available port automatically.</param>
    /// <returns>The current factory instance for method chaining.</returns>
    public KestrelWebApplicationFactory<TProgram> UseKestrel(ushort port = 0)
    {
        _useKestrel = true;
        _kestrelPort = port;
        return this;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();

        // Only modify the host builder to use Kestrel if UseKestrel() was called
        if (_useKestrel)
        {
            // Modify the host builder to use Kestrel instead
            // of TestServer so we can listen on a real address.
            builder.ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder.UseKestrel(options =>
                {
                    // If port is 0, the system will choose an available port automatically
                    options.Listen(IPAddress.Any, _kestrelPort, listenOptions =>
                    {
                        if (Debugger.IsAttached)
                        {
                            listenOptions.UseConnectionLogging();
                        }
                    });
                });
            });

            // Create and start the Kestrel server before the test server,
            // otherwise due to the way the deferred host builder works
            // for minimal hosting, the server will not get "initialized
            // enough" for the address it is listening on to be available.
            _host = builder.Build();
            _host.Start();

            // Extract the selected dynamic port out of the Kestrel server
            // and assign it onto the client options
            var server = _host.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>();

            ClientOptions.BaseAddress = addresses!.Addresses
                .Select(x => new Uri(x))
                .Last();

            // Return the host that uses TestServer, rather than the real one.
            // Otherwise the internals will complain about the host's server
            // not being an instance of the concrete type TestServer.
            // See https://github.com/dotnet/aspnetcore/pull/34702.
            testHost.Start();

            return testHost;
        }

        // If UseKestrel() was not called, use the standard WebApplicationFactory behavior
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
            // This forces WebApplicationFactory to bootstrap the server
            using var _ = CreateDefaultClient();
        }
    }
}
