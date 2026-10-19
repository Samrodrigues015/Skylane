# Fase 1: compilar o site React (fica em src/Skylane.Api/wwwroot, ver vite.config.ts)
FROM node:24-alpine AS web
WORKDIR /src/src/Skylane.Web

COPY src/Skylane.Web/package.json src/Skylane.Web/package-lock.json ./
RUN npm ci

COPY src/Skylane.Web/ ./
RUN npm run build

# Fase 2: compilar e publicar a API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar só o .csproj primeiro aproveita a cache do Docker:
# o restore só volta a correr quando as dependências mudam.
COPY src/Skylane.Api/Skylane.Api.csproj src/Skylane.Api/
RUN dotnet restore src/Skylane.Api/Skylane.Api.csproj

COPY src/Skylane.Api/ src/Skylane.Api/
COPY --from=web /src/src/Skylane.Api/wwwroot src/Skylane.Api/wwwroot
RUN dotnet publish src/Skylane.Api/Skylane.Api.csproj -c Release -o /app --no-restore

# Fase 3: imagem final, só com o runtime (bem mais pequena que a do SDK)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app

ENTRYPOINT ["dotnet", "Skylane.Api.dll"]
