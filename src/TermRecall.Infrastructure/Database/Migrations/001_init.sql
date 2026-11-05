CREATE TABLE categories (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL COLLATE NOCASE UNIQUE,
    color TEXT NOT NULL
);

CREATE TABLE decks (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE terms (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    definition TEXT NOT NULL,
    example TEXT NOT NULL DEFAULT '',
    explanation TEXT NOT NULL DEFAULT '',
    category_id TEXT NULL REFERENCES categories(id) ON DELETE SET NULL,
    deck_id TEXT NOT NULL REFERENCES decks(id) ON DELETE CASCADE,
    created_at TEXT NOT NULL,
    last_reviewed_at TEXT NULL,
    next_review_at TEXT NULL,
    interval_days INTEGER NOT NULL DEFAULT 0 CHECK (interval_days >= 0),
    ease_factor REAL NOT NULL DEFAULT 2.5 CHECK (ease_factor >= 1.3),
    repetition_count INTEGER NOT NULL DEFAULT 0 CHECK (repetition_count >= 0),
    status INTEGER NOT NULL DEFAULT 0,
    lapse_count INTEGER NOT NULL DEFAULT 0 CHECK (lapse_count >= 0)
);

CREATE INDEX ix_terms_deck ON terms(deck_id);
CREATE INDEX ix_terms_next_review ON terms(next_review_at);
CREATE INDEX ix_terms_status ON terms(status);
CREATE INDEX ix_terms_title ON terms(title);

CREATE TABLE review_history (
    id TEXT PRIMARY KEY,
    term_id TEXT NOT NULL REFERENCES terms(id) ON DELETE CASCADE,
    reviewed_at TEXT NOT NULL,
    quality INTEGER NOT NULL CHECK (quality BETWEEN 0 AND 5),
    previous_interval INTEGER NOT NULL,
    new_interval INTEGER NOT NULL,
    previous_ease_factor REAL NOT NULL,
    new_ease_factor REAL NOT NULL,
    response_duration_ms INTEGER NULL,
    repetition_count_before INTEGER NOT NULL,
    repetition_count_after INTEGER NOT NULL,
    calculation_note TEXT NOT NULL DEFAULT ''
);

CREATE INDEX ix_history_term ON review_history(term_id, reviewed_at);
CREATE INDEX ix_history_reviewed ON review_history(reviewed_at);

CREATE TABLE user_settings (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    daily_new_terms_limit INTEGER NOT NULL,
    daily_review_limit INTEGER NOT NULL,
    reminder_enabled INTEGER NOT NULL,
    reminder_time TEXT NOT NULL,
    theme INTEGER NOT NULL,
    include_new_terms INTEGER NOT NULL,
    mastery_target_days INTEGER NOT NULL,
    weight_success REAL NOT NULL,
    weight_regularity REAL NOT NULL,
    weight_mastery REAL NOT NULL,
    display_name TEXT NOT NULL,
    difficult_ease_threshold REAL NOT NULL
);
