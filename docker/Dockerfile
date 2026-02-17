FROM itzg/minecraft-server:latest

# Marker env var — hosting extension detects this to skip redundant setup
ENV ASPIRE_MINECRAFT_PREBAKED=true

# Pre-install plugins for faster, deterministic startup
ENV MODRINTH_PROJECTS="bluemap"

# Accept BlueMap download EULA
# Note: DecentHolograms and OTEL agent can be added in future iterations
