FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BlueBirdDX/BlueBirdDX.csproj", "BlueBirdDX/"]
RUN dotnet restore "BlueBirdDX/BlueBirdDX.csproj"
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
