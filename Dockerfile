FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EventBoard.Api.csproj .
RUN dotnet restore EventBoard.Api.csproj

COPY . .
RUN dotnet publish EventBoard.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
VOLUME ["/app/uploads"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "EventBoard.Api.dll"]
