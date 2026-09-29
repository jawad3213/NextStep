# syntax=docker/dockerfile:1

# Runtime libraries for HTML/PDF rendering (headless Chrome via PuppeteerSharp).
# Shared by the production and dev stages.
ARG BROWSER_DEPS="ca-certificates curl fonts-dejavu-core fonts-liberation \
    libasound2t64 libatk-bridge2.0-0 libatk1.0-0 libc6 libcairo2 libcups2t64 \
    libdbus-1-3 libexpat1 libfontconfig1 libgbm1 libgcc-s1 libglib2.0-0 \
    libgtk-3-0t64 libnspr4 libnss3 libpango-1.0-0 libpangocairo-1.0-0 \
    libstdc++6 libx11-6 libx11-xcb1 libxcb1 libxcomposite1 libxcursor1 \
    libxdamage1 libxext6 libxfixes3 libxi6 libxrandr2 libxrender1 libxss1 \
    libxtst6 xdg-utils"

# ---- Headless Chrome, downloaded once at build time (cached layer) -------
# Without it, PuppeteerSharp downloads ~150 MB at the first PDF render, inside the
# app folder (lost on every container restart): the final CV save timed out.
# Version = the build PuppeteerSharp 20.2.4 targets (update both together).
FROM alpine:3.20 AS chrome
ARG CHROME_VERSION=138.0.7204.101
ARG TARGETARCH
RUN apk add --no-cache curl unzip \
    && mkdir -p /opt/chrome \
    && if [ "$TARGETARCH" = "arm64" ]; then \
         echo "No Chrome for Testing build for linux-arm64: PDF rendering needs an amd64 image."; \
       else \
         curl -fsSL -o /tmp/chrome.zip "https://storage.googleapis.com/chrome-for-testing-public/${CHROME_VERSION}/linux64/chrome-headless-shell-linux64.zip" \
         && unzip -q /tmp/chrome.zip -d /tmp \
         && mv /tmp/chrome-headless-shell-linux64/* /opt/chrome/ \
         && rm -rf /tmp/chrome.zip /tmp/chrome-headless-shell-linux64; \
       fi

# ---- Build: restore (cached) then a single publish ----------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Publish only the native libraries for the target platform. Without a
# runtime identifier, SkiaSharp/PDFium/QuestPDF ship binaries for every OS
# (~560 MB of Windows/macOS/ARM files in the image).
ARG TARGETARCH
WORKDIR /src

# NuGet packages persist in a BuildKit cache: a csproj change only
# downloads the new packages instead of everything.
COPY NextStep.csproj ./
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    RID="linux-$( [ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x64 )" \
    && dotnet restore NextStep.csproj -r "$RID"

COPY . .
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    RID="linux-$( [ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x64 )" \
    && dotnet publish NextStep.csproj -c Release -r "$RID" --self-contained false \
       -o /app/publish --no-restore /p:UseAppHost=false

# ---- Dev: SDK + hot reload (source is bind-mounted by docker-compose.dev.yml)
# Built once; code changes are picked up by `dotnet watch`, no image rebuild.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
ARG BROWSER_DEPS
# Downloaded .deb files stay in a BuildKit cache shared by the dev and
# production stages (both Ubuntu 24.04): reinstalling never re-downloads.
RUN --mount=type=cache,id=apt-ubuntu-noble,target=/var/cache/apt,sharing=locked \
    --mount=type=cache,id=apt-lists-ubuntu-noble,target=/var/lib/apt/lists,sharing=locked \
    rm -f /etc/apt/apt.conf.d/docker-clean \
    && apt-get update && apt-get install -y --no-install-recommends $BROWSER_DEPS
COPY --from=chrome /opt/chrome /opt/chrome
WORKDIR /src
EXPOSE 5000
ENV NEXTSTEP_PUPPETEER_EXECUTABLE_PATH=/opt/chrome/chrome-headless-shell \
    ASPNETCORE_HTTP_PORTS=5000 \
    DOTNET_USE_POLLING_FILE_WATCHER=1 \
    DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1
CMD ["dotnet", "watch", "run", "--non-interactive", "--no-launch-profile", "--urls", "http://0.0.0.0:5000"]

# ---- Production runtime --------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS production
ARG BROWSER_DEPS
WORKDIR /app
RUN --mount=type=cache,id=apt-ubuntu-noble,target=/var/cache/apt,sharing=locked \
    --mount=type=cache,id=apt-lists-ubuntu-noble,target=/var/lib/apt/lists,sharing=locked \
    rm -f /etc/apt/apt.conf.d/docker-clean \
    && apt-get update && apt-get install -y --no-install-recommends $BROWSER_DEPS

COPY --from=chrome /opt/chrome /opt/chrome
COPY --from=build /app/publish .

EXPOSE 5000
ENV NEXTSTEP_PUPPETEER_EXECUTABLE_PATH=/opt/chrome/chrome-headless-shell
ENV ASPNETCORE_HTTP_PORTS=5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "NextStep.dll"]
