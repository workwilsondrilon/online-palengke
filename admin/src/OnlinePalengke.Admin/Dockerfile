# syntax=docker/dockerfile:1.7
#
# Build context is the REPOSITORY ROOT, not this folder - see the `build:`
# block for the `admin-web` service in docker-compose.yml. This project reads
# Directory.Build.props from the repo root.
#
#   docker build -f admin/src/OnlinePalengke.Admin/Dockerfile -t palengke-admin .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY admin/src/OnlinePalengke.Admin/OnlinePalengke.Admin.csproj admin/src/OnlinePalengke.Admin/

RUN dotnet restore admin/src/OnlinePalengke.Admin/OnlinePalengke.Admin.csproj

COPY admin/src/OnlinePalengke.Admin/ admin/src/OnlinePalengke.Admin/

RUN dotnet publish admin/src/OnlinePalengke.Admin/OnlinePalengke.Admin.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OnlinePalengke.Admin.dll"]
