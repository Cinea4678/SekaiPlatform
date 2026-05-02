FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore apps/sync-worker/SekaiPlatform.SyncWorker.csproj
RUN dotnet publish apps/sync-worker/SekaiPlatform.SyncWorker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SekaiPlatform.SyncWorker.dll"]
