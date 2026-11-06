FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY StayDesk.Web/StayDesk.Web.csproj StayDesk.Web/
RUN dotnet restore StayDesk.Web/StayDesk.Web.csproj

COPY StayDesk.Web/ StayDesk.Web/
WORKDIR /src/StayDesk.Web
RUN dotnet publish StayDesk.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "StayDesk.Web.dll"]
