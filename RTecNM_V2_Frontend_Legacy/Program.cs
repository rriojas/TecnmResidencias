var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpClient("BackendApi");

var app = builder.Build();

// Forwarding Proxy: Redirige llamadas /api/* del Frontend hacia el Backend API (RTecNM_V2_Backend)
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();

        var backendUrl = config["BackendUrl"] ?? "http://localhost:5144";
        var client = httpClientFactory.CreateClient("BackendApi");
        
        var targetUri = new Uri($"{backendUrl.TrimEnd('/')}{context.Request.Path}{context.Request.QueryString}");
        Console.WriteLine($"[PROXY DEBUG] Forwarding {context.Request.Method} {context.Request.Path} -> {targetUri}");
        
        using var requestMessage = new HttpRequestMessage();
        requestMessage.Method = new HttpMethod(context.Request.Method);
        requestMessage.RequestUri = targetUri;
        
        if (HttpMethods.IsPost(context.Request.Method) || 
            HttpMethods.IsPut(context.Request.Method) || 
            HttpMethods.IsPatch(context.Request.Method))
        {
            requestMessage.Content = new StreamContent(context.Request.Body);
            if (context.Request.ContentType != null)
            {
                requestMessage.Content.Headers.ContentType = 
                    System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }
        }
        
        foreach (var header in context.Request.Headers)
        {
            if (!header.Key.StartsWith("Host", StringComparison.OrdinalIgnoreCase) && 
                !header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }
        
        try
        {
            using var responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            Console.WriteLine($"[PROXY DEBUG] Backend response status: {(int)responseMessage.StatusCode}");
            context.Response.StatusCode = (int)responseMessage.StatusCode;
            
            foreach (var header in responseMessage.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
            foreach (var header in responseMessage.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
            
            context.Response.Headers.Remove("transfer-encoding");
            await responseMessage.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync($"Error al comunicar con RTecNM_V2_Backend ({backendUrl}): {ex.Message}");
        }
        return;
    }

    await next();
});

app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = app.Services.GetService<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()?.Addresses;
    var primaryUrl = addresses?.FirstOrDefault() ?? "http://localhost:5000";
    var backendUrl = app.Configuration["BackendUrl"] ?? "http://localhost:5144";

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n==================================================");
    Console.WriteLine(" 🚀 RTecNM V2 FRONTEND EN EJECUCIÓN");
    Console.WriteLine($" 📌 Servidor UI iniciado en:   {primaryUrl}");
    Console.WriteLine($" 🔗 Conectado a Backend API:   {backendUrl}");
    Console.WriteLine($" 🔗 Pantalla de Login:         {primaryUrl}/auth/login");
    Console.WriteLine("==================================================\n");
    Console.ResetColor();
});

app.Run();
