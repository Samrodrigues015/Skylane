# Fase 1: compilar e publicar
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar só o .csproj primeiro aproveita a cache do Docker:
# o restore só volta a correr quando as dependências mudam.
COPY src/Skylane.Api/Skylane.Api.csproj src/Skylane.Api/
RUN dotnet restore src/Skylane.Api/Skylane.Api.csproj

COPY src/ src/
RUN dotnet publish src/Skylane.Api/Skylane.Api.csproj -c Release -o /app --no-restore

# Fase 2: imagem final, só com o runtime (bem mais pequena que a do SDK)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app

ENTRYPOINT ["dotnet", "Skylane.Api.dll"]
