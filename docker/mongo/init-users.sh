#!/bin/bash
# Runs once on first MongoDB init (empty data dir), authenticated as the root user
# created via MONGO_INITDB_ROOT_USERNAME/PASSWORD. Creates two least-privilege users on
# the application database (CFG-9.5):
#   - a read-only user  -> used by the library (service-a / service-b)
#   - a read-write user -> used by the Admin API (config-api)
# Credentials come from environment variables (injected from .env), never hardcoded.
set -euo pipefail

mongosh --quiet \
  --host localhost \
  --username "$MONGO_INITDB_ROOT_USERNAME" \
  --password "$MONGO_INITDB_ROOT_PASSWORD" \
  --authenticationDatabase admin <<EOF
const appDb = db.getSiblingDB('configdb');

appDb.createUser({
  user: '$MONGO_APP_RW_USERNAME',
  pwd: '$MONGO_APP_RW_PASSWORD',
  roles: [{ role: 'readWrite', db: 'configdb' }]
});

appDb.createUser({
  user: '$MONGO_APP_RO_USERNAME',
  pwd: '$MONGO_APP_RO_PASSWORD',
  roles: [{ role: 'read', db: 'configdb' }]
});
EOF
