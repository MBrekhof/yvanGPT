using OpenAI;
using OpenAI.Chat;
using yvanGpt.Components;
using yvanGpt.Services;
using Microsoft.Extensions.AI;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Configure file upload limits (20 MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 20 * 1024 * 1024; // 20 MB
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 20 * 1024 * 1024; // 20 MB
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure SignalR for Blazor Server (file upload support)
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 20 * 1024 * 1024; // 20 MB
});

builder.Services.AddDevExpressBlazor(options =>
{
    options.SizeMode = DevExpress.Blazor.SizeMode.Medium;
});
builder.Services.AddMvc();

var openAiServiceSettings = builder.Configuration.GetSection("OpenAISettings").Get<OpenAIServiceSettings>();
if (openAiServiceSettings == null || string.IsNullOrEmpty(openAiServiceSettings.ApiKey))
    throw new InvalidOperationException("Specify the OpenAI API key in the 'appsettings.json' file.");

var openAiClient = new OpenAIClient(openAiServiceSettings.ApiKey);
var chatClient = openAiClient.GetChatClient(openAiServiceSettings.Model).AsIChatClient();

builder.Services.AddScoped<IChatClient>((provider) => chatClient);
builder.Services.AddDevExpressAI();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();

app.Run();