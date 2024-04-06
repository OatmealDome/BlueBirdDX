FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BlueBirdDX.WebApp/BlueBirdDX.WebApp.csproj", "BlueBirdDX.WebApp/"]
RUN dotnet restore "BlueBirdDX.WebApp/BlueBirdDX.WebApp.csproj"
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
