-- Btree indexes back ORDER BY (with id as the paging tiebreaker) and the
-- = / < / > filters; the trigram index backs ILIKE '%...%' on title.

CREATE INDEX issues_status_idx       ON issues (status, id);
CREATE INDEX issues_priority_idx     ON issues (priority, id);
CREATE INDEX issues_project_idx      ON issues (project, id);
CREATE INDEX issues_assignee_idx     ON issues (assignee, id);
CREATE INDEX issues_story_points_idx ON issues (story_points, id);
CREATE INDEX issues_created_at_idx   ON issues (created_at, id);
CREATE INDEX issues_due_date_idx     ON issues (due_date, id);
CREATE INDEX issues_estimate_idx     ON issues (estimate_hours, id);
CREATE INDEX issues_time_spent_idx   ON issues (time_spent, id);
CREATE INDEX issues_cycle_time_idx   ON issues (cycle_time, id);
CREATE INDEX issues_title_trgm_idx   ON issues USING gin (title gin_trgm_ops);

ANALYZE issues;
