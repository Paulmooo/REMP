var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Recam.API.Middlewares.Exceptions.ExceptionHandlingService>();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionHandlingService = context.RequestServices.GetRequiredService<Recam.API.Middlewares.Exceptions.ExceptionHandlingService>();
        await exceptionHandlingService.HandleExceptionAsync(context);
    });
});

app.Run();
