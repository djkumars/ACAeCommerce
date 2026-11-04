FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY services/Shared.Persistence/Shared.Persistence.csproj services/Shared.Persistence/
COPY services/Inventory/Inventory.csproj services/Inventory/
RUN dotnet restore services/Inventory/Inventory.csproj
COPY services/Shared.Persistence/ services/Shared.Persistence/
COPY services/Inventory/ services/Inventory/
RUN dotnet publish services/Inventory/Inventory.csproj -c Release -o /out --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet","Inventory.dll"]
