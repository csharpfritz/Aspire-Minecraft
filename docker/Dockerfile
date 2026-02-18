FROM itzg/minecraft-server:latest

# ── Marker env var ──────────────────────────────────────────────────
# Hosting extension detects this to skip redundant setup
ENV ASPIRE_MINECRAFT_PREBAKED=true

# ── Server configuration ────────────────────────────────────────────
# Mirrors MinecraftServerBuilderExtensions.cs defaults.
# Every value can be overridden at runtime via docker run -e.
ENV EULA=TRUE \
    TYPE=PAPER \
    ONLINE_MODE=FALSE \
    MODE=creative \
    LEVEL_TYPE=flat \
    ENABLE_RCON=true \
    RCON_PORT=25575 \
    SPAWN_PROTECTION=0 \
    VIEW_DISTANCE=12 \
    SIMULATION_DISTANCE=8 \
    GENERATE_STRUCTURES=false \
    SPAWN_ANIMALS=FALSE \
    SPAWN_MONSTERS=FALSE \
    SPAWN_NPCS=FALSE \
    MAX_WORLD_SIZE=29999984

# NOTE: RCON_PASSWORD is intentionally NOT baked in — it must be
#       supplied at runtime by the hosting extension or the user.
# NOTE: SEED is intentionally NOT baked in — it is project-specific.

# ── Plugins ─────────────────────────────────────────────────────────
ENV MODRINTH_PROJECTS="bluemap"

# ── BlueMap configuration ───────────────────────────────────────────
# Accepts the Mojang resource download so BlueMap starts without
# manual intervention.
COPY bluemap/core.conf /plugins/BlueMap/core.conf

# ── Ports ───────────────────────────────────────────────────────────
EXPOSE 25565 25575 8100
