CREATE TABLE written_answers (
    id TEXT PRIMARY KEY,
    term_id TEXT NOT NULL REFERENCES terms(id) ON DELETE CASCADE,
    answered_at TEXT NOT NULL,
    response_text TEXT NOT NULL,
    quality INTEGER NOT NULL CHECK (quality BETWEEN 0 AND 5),
    duration_ms INTEGER NULL
);

CREATE INDEX ix_written_term ON written_answers(term_id, answered_at);
CREATE INDEX ix_written_answered ON written_answers(answered_at);
