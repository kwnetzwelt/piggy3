FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Piggy3.Shared/Piggy3.Shared.csproj src/Piggy3.Shared/
COPY src/Piggy3.Frontend/Piggy3.Frontend.csproj src/Piggy3.Frontend/
RUN dotnet restore src/Piggy3.Frontend/Piggy3.Frontend.csproj

COPY src/Piggy3.Shared/ src/Piggy3.Shared/
COPY src/Piggy3.Frontend/ src/Piggy3.Frontend/
RUN dotnet publish src/Piggy3.Frontend/Piggy3.Frontend.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Piggy3.Frontend.dll"]
