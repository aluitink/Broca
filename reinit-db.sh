#!/usr/bin/env bash
set -euo pipefail

CONTAINER="${CONTAINER:-broca-mysql}"
DB="${DB:-broca}"
USER="${USER:-broca}"
PASS="${PASS:-broca}"

echo "Dropping all tables in '$DB' on container '$CONTAINER'..."

docker exec "$CONTAINER" mysql -u"$USER" -p"$PASS" "$DB" -e "
  SET FOREIGN_KEY_CHECKS=0;
  DROP TABLE IF EXISTS actor_relationships;
  DROP TABLE IF EXISTS collection_members;
  DROP TABLE IF EXISTS delivery_queue;
  DROP TABLE IF EXISTS activities;
  DROP TABLE IF EXISTS collections;
  DROP TABLE IF EXISTS blobs;
  DROP TABLE IF EXISTS actors;
  SET FOREIGN_KEY_CHECKS=1;
"

echo "Done. Restarting broca-api to recreate schema..."
docker restart broca-api

echo "DB reinitialized."
