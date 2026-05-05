using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Hosting;
using SekaiPlatform.Shared.Web.Http;
using SekaiPlatform.SourceSync;
using SekaiPlatform.SyncWorker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSekaiPlatformDatabase(builder.Configuration);
builder.Services.AddMoeSekaiSourceSync(builder.Configuration);
builder.Services.AddSekaiPlatformInternalTokenIssuer(builder.Configuration, requirePrivateKey: true);
builder.Services.AddTransient<SekaiContextPropagationHandler>();
builder.Services.AddSekaiPlatformSearchIndexRefreshClient(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
