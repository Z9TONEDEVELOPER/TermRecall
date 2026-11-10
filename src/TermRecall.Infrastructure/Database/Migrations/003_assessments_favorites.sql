CREATE TABLE assessment_answers (
    id TEXT PRIMARY KEY,
    mode TEXT NOT NULL CHECK (mode IN ('test', 'exam')),
    term_id TEXT NOT NULL REFERENCES terms(id) ON DELETE CASCADE,
    answered_at TEXT NOT NULL,
    correct INTEGER NOT NULL CHECK (correct IN (0, 1)),
    selected_text TEXT NOT NULL,
    duration_ms INTEGER NULL
);

CREATE INDEX ix_assessment_term ON assessment_answers(term_id, answered_at);
CREATE INDEX ix_assessment_answered ON assessment_answers(answered_at);

CREATE TABLE term_favorites (
    term_id TEXT PRIMARY KEY REFERENCES terms(id) ON DELETE CASCADE,
    created_at TEXT NOT NULL
);
