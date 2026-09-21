FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/DzhusShelter.UI/DzhusShelter.UI.csproj", "src/DzhusShelter.UI/"]
RUN dotnet restore "src/DzhusShelter.UI/DzhusShelter.UI.csproj"
COPY . .
WORKDIR "/src/src/DzhusShelter.UI"
RUN dotnet build "DzhusShelter.UI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "DzhusShelter.UI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DzhusShelter.UI.dll"]
