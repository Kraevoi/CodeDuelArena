FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
RUN mkdir -p /opt/render/project/src/data
COPY --from=build /app/out .
EXPOSE 80
ENTRYPOINT ["dotnet", "CodeDuelArena.dll"]