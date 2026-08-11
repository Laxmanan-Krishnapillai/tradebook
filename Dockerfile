FROM node:22-bookworm-slim AS frontend
WORKDIR /src/Frontend
ARG VITE_ENTRA_TENANT_ID
ARG VITE_ENTRA_SPA_CLIENT_ID
ARG VITE_ENTRA_API_CLIENT_ID
ARG VITE_ENTRA_REDIRECT_ORIGIN
ENV VITE_ENTRA_TENANT_ID=$VITE_ENTRA_TENANT_ID \
    VITE_ENTRA_SPA_CLIENT_ID=$VITE_ENTRA_SPA_CLIENT_ID \
    VITE_ENTRA_API_CLIENT_ID=$VITE_ENTRA_API_CLIENT_ID \
    VITE_ENTRA_REDIRECT_ORIGIN=$VITE_ENTRA_REDIRECT_ORIGIN
COPY src/Frontend/package.json src/Frontend/package-lock.json ./
RUN npm ci --legacy-peer-deps
COPY src/Frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY global.json Directory.Build.props Directory.Build.targets Directory.Packages.props BannedSymbols.txt .editorconfig .csharpierignore ./
COPY src/Backend/ ./src/Backend/
COPY src/Aspire/ ./src/Aspire/
COPY src/Database/ ./src/Database/
COPY tests/ ./tests/
RUN dotnet restore src/Backend/Tradebook.sln
COPY --from=frontend /src/Frontend/dist ./src/Backend/src/Tradebook.Api/wwwroot
RUN dotnet publish src/Backend/src/Tradebook.Api/Tradebook.Api.csproj -c Release -o /app/publish --no-restore
RUN dotnet publish src/Backend/src/Tradebook.Migrations/Tradebook.Migrations.csproj -c Release -o /app/migrator --no-restore

FROM postgres:17-bookworm AS database-ops
ARG AZCOPY_VERSION=10.32.4
ARG AZCOPY_SHA256=8f859a0dbbc117660c249fb3569694fc8a0f33b68701f5b2b92ccc001ee50784
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl \
    && curl --fail --location --show-error \
       "https://github.com/Azure/azure-storage-azcopy/releases/download/v${AZCOPY_VERSION}/azcopy_linux_amd64_${AZCOPY_VERSION}.tar.gz" \
       --output /tmp/azcopy.tar.gz \
    && echo "${AZCOPY_SHA256}  /tmp/azcopy.tar.gz" | sha256sum --check --strict \
    && mkdir /tmp/azcopy \
    && tar --extract --gzip --file /tmp/azcopy.tar.gz --directory /tmp/azcopy --strip-components=1 \
    && install --mode 0555 /tmp/azcopy/azcopy /usr/local/bin/azcopy \
    && rm -rf /tmp/azcopy /tmp/azcopy.tar.gz /var/lib/apt/lists/*
COPY --from=backend /app/migrator/ /opt/tradebook/migrator/
COPY --from=mcr.microsoft.com/dotnet/aspnet:10.0 /usr/share/dotnet/ /usr/share/dotnet/
COPY infra/database-ops/ /opt/tradebook/database-ops/
RUN ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet \
    && chmod 0555 /opt/tradebook/database-ops/*.sh \
    # Pre-create the backup mount point owned by the runtime user: a named volume
    # initialized from this directory inherits the ownership, so backup.sh can
    # mkdir under it while running as postgres.
    && mkdir -p /tmp/tradebook-backup \
    && chown postgres:postgres /tmp/tradebook-backup
USER postgres
ENTRYPOINT ["/bin/bash"]
CMD ["/opt/tradebook/database-ops/run-migrations.sh"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --system tradebook \
    && useradd --system --gid tradebook --create-home tradebook
COPY --from=backend /app/publish ./
RUN chown -R tradebook:tradebook /app
USER tradebook
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=5 \
    CMD curl --fail --silent --show-error http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "Tradebook.Api.dll"]
