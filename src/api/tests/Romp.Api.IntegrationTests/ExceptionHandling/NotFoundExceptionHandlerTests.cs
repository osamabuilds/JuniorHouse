using Microsoft.AspNetCore.Http;
using Romp.Api.ExceptionHandling;
using System.Text.Json;

namespace Romp.Api.IntegrationTests.ExceptionHandling;

public sealed class NotFoundExceptionHandlerTests
{
    private static DefaultHttpContext NewContext() =>
        new() { Response = { Body = new MemoryStream() } };

    [Fact]
    public async Task TryHandle_KeyNotFound_Returns404WithTheHandlersOwnMessage()
    {
        var context = NewContext();
        var handler = new NotFoundExceptionHandler();

        var handled = await handler.TryHandleAsync(
            context, new KeyNotFoundException("File 7 was not found on purchase order 3."), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("File 7 was not found on purchase order 3.", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TryHandle_AnyOtherException_IsLeftForTheNextHandler()
    {
        var handled = await new NotFoundExceptionHandler().TryHandleAsync(
            NewContext(), new InvalidOperationException("boom"), CancellationToken.None);

        Assert.False(handled);
    }
}
