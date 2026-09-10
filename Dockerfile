# ===== ETAPA 1: COMPILACION =====
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copiar solo el .csproj primero para aprovechar el cache de Docker
COPY *.csproj ./
RUN dotnet restore

# Copiar el resto del codigo y compilar
COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# ===== ETAPA 2: IMAGEN FINAL (solo runtime, mas pequena) =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copiar los archivos compilados
COPY --from=build /app/publish .

# Railway inyecta la variable PORT automaticamente
# ASPNETCORE_HTTP_PORTS se lee en Program.cs via variable de entorno PORT
EXPOSE 8080

# Iniciar la aplicacion
ENTRYPOINT ["dotnet", "SistemaChotaExpress.dll"]
