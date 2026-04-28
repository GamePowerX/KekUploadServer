# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY ["./KekUploadServer/KekUploadServer.csproj", "KekUploadServer/"]
RUN case "$TARGETARCH" in amd64) dotnet_arch=x64 ;; arm64) dotnet_arch=arm64 ;; *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; esac \
    && dotnet restore "KekUploadServer/KekUploadServer.csproj" -a "$dotnet_arch"
COPY . .
WORKDIR "/src/KekUploadServer"
RUN case "$TARGETARCH" in amd64) dotnet_arch=x64 ;; arm64) dotnet_arch=arm64 ;; *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; esac \
    && dotnet publish "KekUploadServer.csproj" -c Release -a "$dotnet_arch" -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
RUN apt update
RUN apt install -y ffmpeg
RUN rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/config/
WORKDIR /app/config
ENTRYPOINT ["dotnet", "../KekUploadServer.dll"]
