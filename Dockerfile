FROM node:22-bookworm-slim AS frontend
WORKDIR /src/Frontend
COPY src/Frontend/package.json src/Frontend/package-lock.json ./
RUN npm ci --legacy-peer-deps
COPY src/Frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend
WORKDIR /src
COPY Directory.Build.props Directory.Build.targets ./
COPY src/Backend/ ./src/Backend/
COPY tests/ ./tests/
RUN dotnet restore src/Backend/Tradebook.sln
RUN dotnet publish src/Backend/src/Tradebook.Api/Tradebook.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS runtime
WORKDIR /app
RUN groupadd --system tradebook && useradd --system --gid tradebook --create-home tradebook
COPY --from=backend /app/publish ./
COPY --from=frontend /src/Frontend/dist ./wwwroot
RUN chown -R tradebook:tradebook /app
USER tradebook
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Tradebook.Api.dll"]
