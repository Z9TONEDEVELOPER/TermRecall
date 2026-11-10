ALTER TABLE user_settings ADD COLUMN weekly_minutes_goal INTEGER NOT NULL DEFAULT 60;

CREATE TABLE achievements (
    code TEXT PRIMARY KEY,
    awarded_at TEXT NOT NULL
);
