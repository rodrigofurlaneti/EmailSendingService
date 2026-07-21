# ===== Estágio 1: build =====
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia arquivos de solução/props primeiro para aproveitar cache de restore
COPY global.json Directory.Build.props EmailSendingService.sln ./
COPY src/EmailSendingService.Domain/EmailSendingService.Domain.csproj          src/EmailSendingService.Domain/
COPY src/EmailSendingService.Application/EmailSendingService.Application.csproj  src/EmailSendingService.Application/
COPY src/EmailSendingService.Infrastructure/EmailSendingService.Infrastructure.csproj src/EmailSendingService.Infrastructure/
COPY src/EmailSendingService.Api/EmailSendingService.Api.csproj                 src/EmailSendingService.Api/

# Restaura só o projeto da API (traz as dependências transitivas)
RUN dotnet restore src/EmailSendingService.Api/EmailSendingService.Api.csproj

# Copia o restante do código-fonte e publica
COPY src/ src/
RUN dotnet publish src/EmailSendingService.Api/EmailSendingService.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ===== Estágio 2: runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# A API escuta na 8080 dentro do container. Roda em modo Production.
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Healthcheck usando o endpoint /health exposto pela API
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "EmailSendingService.Api.dll"]
