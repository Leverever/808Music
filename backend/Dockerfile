FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["808Music.Domain/808Music.Domain.csproj", "808Music.Domain/"]
COPY ["808Music.Application/808Music.Application.csproj", "808Music.Application/"]
COPY ["808Music.Infrastructure/808Music.Infrastructure.csproj", "808Music.Infrastructure/"]
COPY ["RS1_2024_25.API/_808Music.Api.csproj", "RS1_2024_25.API/"]
RUN dotnet restore "RS1_2024_25.API/_808Music.Api.csproj"

COPY . .
RUN dotnet publish "RS1_2024_25.API/_808Music.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    && mkdir -p /app/publish/wwwroot /app/publish/TrackFiles \
    && if [ -d /src/RS1_2024_25.API/wwwroot ]; then \
         cp -a /src/RS1_2024_25.API/wwwroot/. /app/publish/wwwroot/; \
       fi \
    && if [ -d /src/RS1_2024_25.API/TrackFiles ]; then \
         cp -a /src/RS1_2024_25.API/TrackFiles/. /app/publish/TrackFiles/; \
       fi

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .

# The named volumes are populated from these image directories the first time
# they are mounted, preserving the local wwwroot and legacy track files that
# were present in the Docker build context.
RUN mkdir -p /app/wwwroot /app/TrackFiles \
    && chown -R "$APP_UID:$APP_UID" /app/wwwroot /app/TrackFiles

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "_808Music.Api.dll"]
