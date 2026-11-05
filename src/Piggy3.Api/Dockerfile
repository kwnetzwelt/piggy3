FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Piggy3.Shared/Piggy3.Shared.csproj src/Piggy3.Shared/
COPY src/Piggy3.Api/Piggy3.Api.csproj src/Piggy3.Api/
RUN dotnet restore src/Piggy3.Api/Piggy3.Api.csproj

COPY src/Piggy3.Shared/ src/Piggy3.Shared/
COPY src/Piggy3.Api/ src/Piggy3.Api/
RUN dotnet publish src/Piggy3.Api/Piggy3.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Piggy3.Api.dll"]
