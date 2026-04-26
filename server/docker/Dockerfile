FROM mcr.microsoft.com/dotnet/sdk:8.0

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        lsof \
        mariadb-client \
        procps \
        psmisc \
        iproute2 \
        ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /opt/clover

COPY server/app /opt/clover/server/app
COPY server/docker/entrypoint.sh /opt/clover/server/docker/entrypoint.sh

WORKDIR /opt/clover/server/app

RUN dotnet restore Melia.sln \
    && dotnet build Melia.sln -c Release --no-restore \
    && chmod +x /opt/clover/server/docker/entrypoint.sh

EXPOSE 2000 7001 7002 8080 9001 9002

ENTRYPOINT ["/opt/clover/server/docker/entrypoint.sh"]
