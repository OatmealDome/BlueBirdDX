FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

# --platform=$BUILDPLATFORM is currently required for arm builds: https://github.com/dotnet/sdk/issues/29892#issuecomment-1691521917
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG NuGet_SourceUrl
ARG NuGet_SourceUserName
ARG NuGet_SourcePassword
WORKDIR /src
COPY ["BlueBirdDX/BlueBirdDX.csproj", "nuget-ci.config", "BlueBirdDX/"]
RUN dotnet restore --configfile "BlueBirdDX/nuget-ci.config" "BlueBirdDX/BlueBirdDX.csproj"
COPY . .
WORKDIR "/src/BlueBirdDX"
RUN dotnet build "BlueBirdDX.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "BlueBirdDX.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BlueBirdDX.dll"]
