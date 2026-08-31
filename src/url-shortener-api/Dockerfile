FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY Directory.Build.props UrlShortener.sln ./
COPY Api/Api.csproj Api/
COPY Application/Application.csproj Application/
COPY Domain/Domain.csproj Domain/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
RUN dotnet restore Api/Api.csproj

COPY . .
RUN dotnet publish Api/Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

RUN case "${TARGETARCH}" in \
        amd64) migration_rid=linux-x64 ;; \
        arm64) migration_rid=linux-arm64 ;; \
        *) echo "Unsupported target architecture: ${TARGETARCH}" >&2; exit 1 ;; \
    esac \
    && dotnet tool install --tool-path /tools dotnet-ef --version 10.0.0 \
    && /tools/dotnet-ef migrations bundle \
        --project Infrastructure/Infrastructure.csproj \
        --startup-project Api/Api.csproj \
        --configuration Release \
        --no-build \
        --target-runtime "${migration_rid}" \
        --output /app/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/efbundle ./efbundle

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
