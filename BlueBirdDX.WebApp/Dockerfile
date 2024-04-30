FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# --platform=$BUILDPLATFORM is currently required for arm builds: https://github.com/dotnet/sdk/issues/29892#issuecomment-1691521917
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG NuGet_SourceUrl
ARG NuGet_SourceUserName
ARG NuGet_SourcePassword
WORKDIR /src
COPY ["BlueBirdDX.WebApp/BlueBirdDX.WebApp.csproj", "nuget-ci.config", "BlueBirdDX.WebApp/"]
RUN dotnet restore --configfile "BlueBirdDX.WebApp/nuget-ci.config" "BlueBirdDX.WebApp/BlueBirdDX.WebApp.csproj"
COPY . .
WORKDIR "/src/BlueBirdDX.WebApp"
RUN dotnet build "BlueBirdDX.WebApp.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "BlueBirdDX.WebApp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BlueBirdDX.WebApp.dll"]
