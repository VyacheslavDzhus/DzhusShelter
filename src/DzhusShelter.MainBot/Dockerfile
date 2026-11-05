FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/DzhusShelter.MainBot/DzhusShelter.MainBot.csproj", "src/DzhusShelter.MainBot/"]
RUN dotnet restore "src/DzhusShelter.MainBot/DzhusShelter.MainBot.csproj"
COPY . .
WORKDIR "/src/src/DzhusShelter.MainBot"
RUN dotnet build "DzhusShelter.MainBot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "DzhusShelter.MainBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DzhusShelter.MainBot.dll"]
