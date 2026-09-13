-- Recreated from scratch by seed.py. Indexes live in indexes.sql so they can
-- be built after the bulk load, which is much faster than maintaining them
-- row by row.

DROP TABLE IF EXISTS issues;
DROP TYPE IF EXISTS issue_status;
DROP TYPE IF EXISTS issue_priority;

CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Postgres enums sort in declaration order, which matches the ordinal order
-- of the C# IssueStatus/IssuePriority enums, so sorting and < / > filters
-- behave the same as the in-memory provider.
CREATE TYPE issue_status AS ENUM ('Todo', 'InProgress', 'Done');
CREATE TYPE issue_priority AS ENUM ('Low', 'Medium', 'High', 'Urgent');

CREATE TABLE issues (
    id           integer        PRIMARY KEY,
    title        text           NOT NULL,
    status       issue_status   NOT NULL,
    priority     issue_priority NOT NULL,
    project      text           NOT NULL,
    assignee     text           NULL,
    story_points integer        NULL,
    created_at     timestamp        NOT NULL,
    due_date       date             NULL,
    estimate_hours double precision NULL,
    time_spent     interval         NULL,  -- minutes to under a day
    cycle_time     interval         NULL   -- days to months; only for Done issues
);
