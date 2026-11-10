CREATE TABLE term_media (
    id TEXT PRIMARY KEY,
    term_id TEXT NOT NULL REFERENCES terms(id) ON DELETE CASCADE,
    kind TEXT NOT NULL CHECK (kind IN ('image', 'audio')),
    file_name TEXT NOT NULL,
    media_type TEXT NOT NULL,
    created_at TEXT NOT NULL
);

CREATE INDEX ix_term_media_term ON term_media(term_id);
