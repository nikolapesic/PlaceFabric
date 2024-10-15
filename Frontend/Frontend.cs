using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using User;
using Grid;
using Microsoft.ServiceFabric.Actors.Client;
using User.Interfaces;

namespace Frontend
{
    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance.
    /// </summary>
    internal sealed class Frontend : StatelessService
    {
        public Frontend(StatelessServiceContext context)
            : base(context)
        { }

        ServiceProxyFactory proxyFactory = new ServiceProxyFactory(c => new FabricTransportServiceRemotingClientFactory());
        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[]
            {
                new ServiceInstanceListener(serviceContext =>
                    new KestrelCommunicationListener(serviceContext, "ServiceEndpoint", (url, listener) =>
                    {
                        ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting Kestrel on {url}");

                        var builder = WebApplication.CreateBuilder();

                        builder.Services.AddSingleton<StatelessServiceContext>(serviceContext);
                        builder.WebHost
                                    .UseKestrel()
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                    .UseUrls(url);
                        var app = builder.Build();
                        app.UseDefaultFiles(); // Serve index.html by default
                        app.UseStaticFiles(); // Serve main.js, styles.css...
                        app.UseRouting();

                        // User creation
                        app.MapPost("/createUser", async (string firstName, string lastName, string username, string password) =>
                        {
                            var userActor = ActorProxy.Create<IUser>(
                                new Microsoft.ServiceFabric.Actors.ActorId(username),
                                new Uri("fabric:/PlaceApplication/UserActorService"));

                            var result = await userActor.CreateUserAsync(firstName, lastName, password);
                            return result ? Results.Ok("User created") : Results.BadRequest("User already exists");
                        });

                        // User login
                        app.MapPost("/login", async (string username, string password) =>
                        {
                            var userActor = ActorProxy.Create<IUser>(
                                new Microsoft.ServiceFabric.Actors.ActorId(username),
                                new Uri("fabric:/PlaceApplication/UserActorService"));

                            var token = await userActor.LoginUserAsync(password);
                            return token != null ? Results.Ok(token) : Results.BadRequest("Invalid login");
                        });

                        // User logout
                        app.MapPost("/logout", async (string username, string token) => // Token should be passed in secure header, but for simplicity it's passed in query
                        {
                            var userActor = ActorProxy.Create<IUser>(
                                new Microsoft.ServiceFabric.Actors.ActorId(username),
                                new Uri("fabric:/PlaceApplication/UserActorService"));

                            var isAuthenticated = await userActor.IsUserAuthenticatedAsync(token);
                            if (!isAuthenticated)
                            {
                                return Results.Unauthorized();
                            }

                            await userActor.LogoutUserAsync();
                            return Results.Ok("Logged out");
                        });

                        // Remaining cooldown time
                        app.MapGet("/remainingCooldown", async (string username, string token) =>
                        {
                            var userActor = ActorProxy.Create<IUser>(
                                new Microsoft.ServiceFabric.Actors.ActorId(username),
                                new Uri("fabric:/PlaceApplication/UserActorService"));

                            var isAuthenticated = await userActor.IsUserAuthenticatedAsync(token);
                            if (!isAuthenticated)
                            {
                                return Results.Unauthorized();
                            }

                            var remainingTime = await userActor.GetRemainingCooldownTimeAsync();
                            return Results.Ok(remainingTime);
                        });

                        // User info
                        app.MapGet("/userInfo", async (string username, string token) =>
                        {
                            var userActor = ActorProxy.Create<IUser>(
                                new Microsoft.ServiceFabric.Actors.ActorId(username),
                                new Uri("fabric:/PlaceApplication/UserActorService"));

                            var isAuthenticated = await userActor.IsUserAuthenticatedAsync(token);
                            if (!isAuthenticated)
                            {
                                return Results.Unauthorized();
                            }

                            var info = await userActor.GetUserInfo();
                            return Results.Ok(new { FirstName = info.Item1, LastName = info.Item2 });
                        });

                        // Get whole grid
                        app.MapGet("/getGrid", async () =>
                        {
                            var gridService = proxyFactory.CreateServiceProxy<IGridService>(
                                new Uri("fabric:/PlaceApplication/Grid"),
                                new ServicePartitionKey(0));

                            var grid = await gridService.GetGridAsync();
                            return Results.Ok(grid);
                        });

                        // Get updated cells
                        app.MapGet("/getUpdatedCells", async (long unixTimeSeconds) =>
                        {
                            var gridService = proxyFactory.CreateServiceProxy<IGridService>(
                                new Uri("fabric:/PlaceApplication/Grid"),
                                new ServicePartitionKey(0));

                            var grid = await gridService.GetUpdatedCellsAsync(unixTimeSeconds);
                            return Results.Ok(grid);
                        });

                        // Set pixel placement time
                        app.MapPost("/place", async (string username, string token, int x, int y, string color) =>
                        {
                            var userActor = ActorProxy.Create<IUser>(
                                new Microsoft.ServiceFabric.Actors.ActorId(username),
                                new Uri("fabric:/PlaceApplication/UserActorService"));

                            var isAuthenticated = await userActor.IsUserAuthenticatedAsync(token);
                            if (!isAuthenticated)
                            {
                                return Results.Unauthorized();
                            }

                            var remainingTime = await userActor.GetRemainingCooldownTimeAsync();
                            if (remainingTime > 0)
                            {
                                return Results.BadRequest("Cooldown not expired");
                            }

                            var gridService = proxyFactory.CreateServiceProxy<IGridService>(
                                new Uri("fabric:/PlaceApplication/Grid"),
                                new ServicePartitionKey(0));

                            var success = await gridService.UpdateCellAsync(x, y, color);
                            if (!success)
                            {
                                return Results.BadRequest("Invalid coordinates or color");
                            }

                            await userActor.SetPixelPlacementTimeAsync();

                            return Results.Ok("Pixel placed");
                        });

                        return app;

                    }))
            };
        }
    }
}
