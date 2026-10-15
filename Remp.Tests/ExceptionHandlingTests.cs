using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Remp.API.Middlewares.Exceptions;
using Remp.Common.Exceptions;

namespace Remp.Tests;

public class ExceptionHandlingTests
{
    public static TheoryData<Exception, int> Cases => new()
    {
        { new ValidationException("Invalid profile"), 400 },
        { new ArgumentException("Invalid input"), 400 },
        { new ForbiddenException("Other company"), 403 },
        { new ConflictException("Already linked"), 409 },
        { new KeyNotFoundException(), 404 },
        { new UnauthorizedAccessException(), 401 },
        { new InvalidOperationException("internal diagnostic"), 500 },
        { new Exception("internal diagnostic"), 500 }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ExpectedErrors_HaveStableHttpStatusAndEnvelope(Exception error, int status)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/User/agent";
        context.Response.Body = new MemoryStream();
        context.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature { Error = error });
        await new ExceptionHandlingService().HandleExceptionAsync(context);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(status, body.RootElement.GetProperty("StatusCode").GetInt32());
        Assert.Equal(error.GetType().Name, body.RootElement.GetProperty("ErrorType").GetString());
        Assert.Equal("/api/User/agent", body.RootElement.GetProperty("Detail").GetString());
        if (status == 500) Assert.DoesNotContain("internal diagnostic", body.RootElement.GetProperty("Message").GetString());
    }
}
