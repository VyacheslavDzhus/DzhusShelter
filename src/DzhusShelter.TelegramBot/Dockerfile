FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/DzhusShelter.TelegramBot/DzhusShelter.TelegramBot.csproj", "src/DzhusShelter.TelegramBot/"]
RUN dotnet restore "src/DzhusShelter.TelegramBot/DzhusShelter.TelegramBot.csproj"
COPY . .
WORKDIR "/src/src/DzhusShelter.TelegramBot"
RUN dotnet build "DzhusShelter.TelegramBot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "DzhusShelter.TelegramBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DzhusShelter.TelegramBot.dll"]
