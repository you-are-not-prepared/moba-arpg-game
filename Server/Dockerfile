# The game server: the headless Linux build made by MOBA > Build Linux Server (in Builds/LinuxServer).
FROM ubuntu:24.04
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /server
COPY Builds/LinuxServer/ ./
RUN chmod +x MobaServer.x86_64 && chown -R ubuntu:ubuntu /server
USER ubuntu
EXPOSE 7777/udp
# -logFile - : Unity's log goes to stdout, so `docker compose logs` shows it.
ENTRYPOINT ["./MobaServer.x86_64", "-batchmode", "-nographics", "-logFile", "-"]
