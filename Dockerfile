# syntax=docker/dockerfile:1
# One Dockerfile for every .NET service:
#   docker build --build-arg PROJECT=src/Services/Catalog/OmniOrder.Catalog.Api --build-arg ASSEMBLY=OmniOrder.Catalog.Api .
ARG PROJECT
ARG ASSEMBLY

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG PROJECT
ARG ASSEMBLY
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet restore ${PROJECT}/${ASSEMBLY}.csproj
RUN dotnet publish ${PROJECT}/${ASSEMBLY}.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
ARG ASSEMBLY
ENV ASSEMBLY=${ASSEMBLY} \
    ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
USER app
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=6 \
  CMD wget -qO- http://localhost:8080/health/live || exit 1
ENTRYPOINT ["sh", "-c", "exec dotnet ${ASSEMBLY}.dll"]
