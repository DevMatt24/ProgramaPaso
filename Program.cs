using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ProgramaPaso;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<ProgramaPaso.Services.QuestionBank>();
builder.Services.AddScoped<ProgramaPaso.Domain.QuizEngine>();

builder.Services.AddScoped<ProgramaPaso.Services.SoundService>();
builder.Services.AddScoped<ProgramaPaso.Services.ProgressService>();


builder.Services.AddScoped<ProgramaPaso.Services.CodeRunnerService>();

await builder.Build().RunAsync();