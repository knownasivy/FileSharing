FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
ARG APP_UID=1000
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["FileSharing.Api/FileSharing.Api.csproj", "FileSharing.Api/"]
RUN dotnet restore "FileSharing.Api/FileSharing.Api.csproj"

COPY . .
WORKDIR "/src/FileSharing.Api"
RUN dotnet build "FileSharing.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "FileSharing.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Option to download?

COPY --from=my-ffmpeg-static:latest /ffmpeg /usr/local/bin/
COPY --from=my-ffmpeg-static:latest /ffprobe /usr/local/bin/

ENTRYPOINT ["dotnet", "FileSharing.Api.dll"]