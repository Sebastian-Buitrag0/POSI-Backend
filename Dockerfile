# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY POSI.sln .
COPY Src/POSI.Domain/POSI.Domain.csproj Src/POSI.Domain/
COPY Src/POSI.Data/POSI.Data.csproj Src/POSI.Data/
COPY Src/POSI.Services/POSI.Services.csproj Src/POSI.Services/
COPY Src/POSI.Api/POSI.Api.csproj Src/POSI.Api/

RUN dotnet restore POSI.sln

# Copy source and publish
COPY . .
RUN dotnet publish Src/POSI.Api/POSI.Api.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "POSI.Api.dll"]
