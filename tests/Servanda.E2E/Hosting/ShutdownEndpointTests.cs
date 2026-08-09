using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Servanda.App.Hosting;

namespace Servanda.E2E.Hosting;

public sealed class ShutdownEndpointTests
{
    [Fact]
    public async Task ShutdownRouteRequiresPostAndAntiforgery()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAntiforgery();
        await using var app = builder.Build();
        app.MapShutdown();

        var route = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == ShutdownEndpoint.Path);

        var methods = route.Metadata.GetRequiredMetadata<HttpMethodMetadata>();
        var antiforgery = route.Metadata.GetRequiredMetadata<IAntiforgeryMetadata>();

        Assert.Equal([HttpMethods.Post], methods.HttpMethods);
        Assert.True(antiforgery.RequiresValidation);
    }
}
