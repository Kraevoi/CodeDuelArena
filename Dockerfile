FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем csproj и восстанавливаем зависимости
COPY CodeDuelArena.csproj .
RUN dotnet restore

# Копируем все остальное и собираем
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Финальный образ
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY --from=build /app/publish .

# Render передает порт через PORT
ENV ASPNETCORE_URLS=http://+:${PORT:-80}

ENTRYPOINT ["dotnet", "CodeDuelArena.dll"]