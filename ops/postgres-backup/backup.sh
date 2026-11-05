#!/bin/sh
# Weekly logical backup of the dzhusshelter Postgres database.
#
# Runs as the long-lived `backup` service in docker-compose.yml. Instead of a fixed cron time
# it checks once an hour whether the newest dump is older than BACKUP_INTERVAL_MINUTES: the
# home server loses power regularly, and a fixed "Sunday 03:00" slot would silently skip a
# whole week whenever the box happened to be off at that moment. This way the backup simply
# happens on the first check after the server comes back.
#
# Usage:
#   backup.sh          loop forever (the service's entrypoint)
#   backup.sh --once   take one backup right now and exit (e.g. before a risky migration)
#
# Connection comes from the standard libpq env vars (PGHOST, PGUSER, PGPASSWORD, PGDATABASE).
set -eu

BACKUP_DIR="${BACKUP_DIR:-/backups}"
BACKUP_KEEP="${BACKUP_KEEP:-3}"
BACKUP_INTERVAL_MINUTES="${BACKUP_INTERVAL_MINUTES:-10080}" # 7 days
BACKUP_CHECK_SECONDS="${BACKUP_CHECK_SECONDS:-3600}"
BACKUP_RETRY_SECONDS="${BACKUP_RETRY_SECONDS:-300}"

# Dumps hold every logged entry — keep them readable by the owner (root) only.
umask 077

log() { echo "$(date -u '+%Y-%m-%dT%H:%M:%SZ') backup: $*"; }

backup_due() {
    # Due when no finished dump is younger than the interval (including "no dumps at all").
    [ -z "$(find "$BACKUP_DIR" -maxdepth 1 -name 'dzhusshelter-*.dump' -mmin "-$BACKUP_INTERVAL_MINUTES" | head -n 1)" ]
}

rotate() {
    # ISO-8601 timestamps in the file name sort chronologically, so newest-first by name is
    # newest-first by time. Everything past the first BACKUP_KEEP entries gets deleted.
    find "$BACKUP_DIR" -maxdepth 1 -name 'dzhusshelter-*.dump' | sort -r | tail -n "+$((BACKUP_KEEP + 1))" |
        while read -r old; do
            rm -f "$old"
            log "rotated out $(basename "$old")"
        done
}

make_backup() {
    name="dzhusshelter-$(date -u '+%Y-%m-%dT%H%M%SZ').dump"
    partial="$BACKUP_DIR/.$name.partial"

    # Write to a hidden temp file and only rename on success: a dump that fails halfway
    # (Postgres still starting after a power cut, disk full, ...) must never become a
    # "newest backup" that makes rotate() delete a good one.
    if pg_dump --format=custom --file="$partial"; then
        mv "$partial" "$BACKUP_DIR/$name"
        log "created $name ($(du -h "$BACKUP_DIR/$name" | cut -f1))"
        rotate
    else
        rm -f "$partial"
        log "FAILED — existing backups left untouched"
        return 1
    fi
}

mkdir -p "$BACKUP_DIR"

if [ "${1:-}" = "--once" ]; then
    make_backup
    exit $?
fi

# Exit promptly on `docker compose stop` instead of waiting out the 10s kill timeout:
# `sleep` runs in the background so the TERM trap fires immediately.
trap 'log "stopping"; exit 0' TERM INT

log "started (every ${BACKUP_INTERVAL_MINUTES} min, keep ${BACKUP_KEEP}, dir ${BACKUP_DIR})"
while true; do
    wait_seconds="$BACKUP_CHECK_SECONDS"
    if backup_due; then
        make_backup || wait_seconds="$BACKUP_RETRY_SECONDS"
    fi
    sleep "$wait_seconds" &
    wait $!
done
