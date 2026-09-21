FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/DzhusShelter.Api/DzhusShelter.Api.csproj", "src/DzhusShelter.Api/"]
RUN dotnet restore "src/DzhusShelter.Api/DzhusShelter.Api.csproj"
COPY . .
WORKDIR "/src/src/DzhusShelter.Api"
RUN dotnet build "DzhusShelter.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "DzhusShelter.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DzhusShelter.Api.dll"]
