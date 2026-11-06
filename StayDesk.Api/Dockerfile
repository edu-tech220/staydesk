FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY StayDesk.Api/StayDesk.Api.csproj StayDesk.Api/
RUN dotnet restore StayDesk.Api/StayDesk.Api.csproj

COPY StayDesk.Api/ StayDesk.Api/
WORKDIR /src/StayDesk.Api
RUN dotnet publish StayDesk.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "StayDesk.Api.dll"]
