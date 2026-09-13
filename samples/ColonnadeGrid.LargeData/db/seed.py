#!/usr/bin/env python3
"""Populate the large-data sample's Postgres database with synthetic issues.

Connection settings come from the standard libpq environment variables
(PGHOST, PGPORT, PGDATABASE, PGUSER, PGPASSWORD), defaulting to the values in
docker-compose.yml so the script also works when run from the host:

    pip install -r requirements.txt
    python seed.py                      # 100,000 rows, skipped if already seeded
    python seed.py --rows 500000 --force

Data is generated from a fixed random seed, so the same --rows/--seed always
produces the same table.
"""

from __future__ import annotations

import argparse
import os
import random
import sys
import time
from datetime import datetime, timedelta
from pathlib import Path

import psycopg

HERE = Path(__file__).resolve().parent

VERBS = [
    "Fix", "Add", "Refactor", "Investigate", "Remove", "Document", "Optimize",
    "Migrate", "Test", "Redesign", "Support", "Deprecate", "Cache", "Validate",
    "Localize", "Monitor",
]
SUBJECTS = [
    "login redirect", "dark mode", "API pagination", "authentication middleware",
    "checkout flow", "search results", "signup errors", "CI caching",
    "empty states", "keyboard shortcuts", "background worker", "CSV export",
    "date formatting", "rate limiting", "billing webhooks", "modal dialogs",
    "dashboard queries", "file upload", "scheduler", "audit log",
    "bundle size", "health checks", "bulk selection", "column resize",
    "dependency graph", "onboarding checklist", "retry backoff", "logging",
    "password reset", "notification emails", "feature flags", "image thumbnails",
    "session timeout", "report builder", "permissions model", "mobile layout",
]
QUALIFIERS = [
    "", "", "", "on Safari", "for admins", "in the EU region", "under load",
    "after upgrade", "for large accounts", "in dark mode", "on mobile",
    "behind feature flag", "for SSO users", "in staging",
]
PROJECTS = [
    "Atlas", "Beacon", "Cobalt", "Delta", "Ember", "Falcon", "Granite", "Harbor",
    "Iris", "Juniper", "Keystone", "Lumen", "Meridian", "Nimbus", "Onyx",
    "Pioneer", "Quartz", "Redwood", "Summit", "Tundra", "Umbra", "Vertex",
    "Willow", "Zephyr",
]
FIRST_NAMES = [
    "alice", "bob", "carol", "dave", "erin", "frank", "grace", "heidi", "ivan",
    "judy", "mallory", "niaj", "olivia", "peggy", "rupert", "sybil", "trent",
    "uma", "victor", "wendy",
]
# ~200 distinct assignees, so grouping by assignee produces many groups.
ASSIGNEES = [f"{name}{n}" for name in FIRST_NAMES for n in range(1, 11)]

STATUSES = (["Todo", "InProgress", "Done"], [35, 20, 45])
PRIORITIES = (["Low", "Medium", "High", "Urgent"], [30, 40, 22, 8])
STORY_POINTS = [1, 2, 3, 5, 8, 13]

COLUMNS = (
    "id", "title", "status", "priority", "project", "assignee",
    "story_points", "created_at", "due_date",
    "estimate_hours", "time_spent", "cycle_time",
)


def generate_rows(count: int, seed: int):
    rng = random.Random(seed)
    # A separate generator for the columns added later, so the original
    # columns' values don't change with them.
    extra_rng = random.Random(seed + 1)
    now = datetime(2026, 9, 1)
    three_years_seconds = 3 * 365 * 24 * 3600

    for issue_id in range(1, count + 1):
        qualifier = rng.choice(QUALIFIERS)
        title = f"{rng.choice(VERBS)} {rng.choice(SUBJECTS)}"
        if qualifier:
            title = f"{title} {qualifier}"

        created_at = now - timedelta(seconds=rng.randrange(three_years_seconds))
        due_date = (
            (created_at + timedelta(days=rng.randint(7, 120))).date()
            if rng.random() < 0.6
            else None
        )

        status = rng.choices(*STATUSES)[0]
        row = (
            issue_id,
            title,
            status,
            rng.choices(*PRIORITIES)[0],
            rng.choice(PROJECTS),
            rng.choice(ASSIGNEES) if rng.random() < 0.9 else None,
            rng.choice(STORY_POINTS) if rng.random() < 0.8 else None,
            created_at.replace(microsecond=0),
            due_date,
        )

        # Skewed toward small estimates, like real ones: mostly under a day's work.
        estimate_hours = (
            round(min(400.0, max(0.25, extra_rng.lognormvariate(1.5, 1.0))), 2)
            if extra_rng.random() < 0.85
            else None
        )
        time_spent = (
            timedelta(minutes=extra_rng.randrange(15, 24 * 60, 15))
            if extra_rng.random() < 0.7
            else None
        )
        cycle_time = (
            timedelta(days=extra_rng.randint(0, 270), hours=extra_rng.randint(1, 23))
            if status == "Done"
            else None
        )

        yield row + (estimate_hours, time_spent, cycle_time)


def already_seeded(conn: psycopg.Connection) -> int | None:
    """Returns the row count if the issues table exists, else None."""
    exists = conn.execute("SELECT to_regclass('public.issues') IS NOT NULL").fetchone()[0]
    if not exists:
        return None
    return conn.execute("SELECT count(*) FROM issues").fetchone()[0]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--rows", type=int, default=int(os.environ.get("SEED_ROWS", "100000")),
                        help="number of issues to generate (default: $SEED_ROWS or 100000)")
    parser.add_argument("--seed", type=int, default=42, help="random seed (default: 42)")
    parser.add_argument("--force", action="store_true", default=os.environ.get("SEED_FORCE", "0") not in ("", "0", "false"),
                        help="drop and re-seed even if the table already has rows (default: $SEED_FORCE)")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.rows < 1:
        print("--rows must be at least 1", file=sys.stderr)
        return 2

    conninfo = {
        "host": os.environ.get("PGHOST", "localhost"),
        "port": os.environ.get("PGPORT", "5432"),
        "dbname": os.environ.get("PGDATABASE", "colonnade"),
        "user": os.environ.get("PGUSER", "colonnade"),
        "password": os.environ.get("PGPASSWORD", "colonnade"),
    }

    with psycopg.connect(**conninfo) as conn:
        existing = already_seeded(conn)
        if existing and not args.force:
            print(f"issues table already has {existing:,} rows; skipping (use --force or SEED_FORCE=1 to re-seed)")
            return 0

        started = time.perf_counter()
        print(f"Creating schema on {conninfo['host']}:{conninfo['port']}/{conninfo['dbname']} ...")
        conn.execute((HERE / "schema.sql").read_text())

        print(f"Loading {args.rows:,} issues ...")
        with conn.cursor() as cur:
            with cur.copy(f"COPY issues ({', '.join(COLUMNS)}) FROM STDIN") as copy:
                for row in generate_rows(args.rows, args.seed):
                    copy.write_row(row)

        print("Building indexes ...")
        conn.execute((HERE / "indexes.sql").read_text())
        conn.commit()

        print(f"Done: {args.rows:,} issues in {time.perf_counter() - started:.1f}s")
    return 0


if __name__ == "__main__":
    sys.exit(main())
