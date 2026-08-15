# syntax=docker/dockerfile:1.7
#
# Build context is the REPOSITORY ROOT, not this folder - see the `build:`
# block for the `api` service in docker-compose.yml. This project references
# three sibling projects under admin/src/ and reads Directory.Build.props
# from the repo root, so the Docker build needs to see all of that.
#
#   docker build -f admin/src/OnlinePalengke.Api/Dockerfile -t palengke-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files only, first - this layer is cached across builds that change
# application code but not dependencies, which is the common case.
COPY Directory.Build.props ./
COPY admin/src/OnlinePalengke.Domain/OnlinePalengke.Domain.csproj admin/src/OnlinePalengke.Domain/
COPY admin/src/OnlinePalengke.Application/OnlinePalengke.Application.csproj admin/src/OnlinePalengke.Application/
COPY admin/src/OnlinePalengke.Infrastructure/OnlinePalengke.Infrastructure.csproj admin/src/OnlinePalengke.Infrastructure/
COPY admin/src/OnlinePalengke.Api/OnlinePalengke.Api.csproj admin/src/OnlinePalengke.Api/

RUN dotnet restore admin/src/OnlinePalengke.Api/OnlinePalengke.Api.csproj

COPY admin/src/OnlinePalengke.Domain/ admin/src/OnlinePalengke.Domain/
COPY admin/src/OnlinePalengke.Application/ admin/src/OnlinePalengke.Application/
COPY admin/src/OnlinePalengke.Infrastructure/ admin/src/OnlinePalengke.Infrastructure/
COPY admin/src/OnlinePalengke.Api/ admin/src/OnlinePalengke.Api/

RUN dotnet publish admin/src/OnlinePalengke.Api/OnlinePalengke.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# $APP_UID is the non-root "app" user baked into the official runtime image.
# There is no reason for this process to run as root.
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OnlinePalengke.Api.dll"]
