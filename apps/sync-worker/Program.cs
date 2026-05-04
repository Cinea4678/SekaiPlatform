using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web;
using SekaiPlatform.SourceSync;
using SekaiPlatform.SyncWorker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSekaiPlatformDatabase(builder.Configuration);
builder.Services.AddMoeSekaiSourceSync(builder.Configuration);
builder.Services.AddSekaiPlatformSearchIndexRefreshClient(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
