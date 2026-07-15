# Thin image over the official mongo:7 that bakes the least-privilege user bootstrap
# (CFG-9.5) into the entrypoint. Baking the script (rather than bind-mounting it) keeps
# the stack portable — it needs no Docker host file-sharing configuration to start.
FROM mongo:7

COPY init-users.sh /docker-entrypoint-initdb.d/init-users.sh
RUN chmod +x /docker-entrypoint-initdb.d/init-users.sh
