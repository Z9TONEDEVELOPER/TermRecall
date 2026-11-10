ALTER TABLE terms ADD COLUMN code TEXT NOT NULL DEFAULT '';
ALTER TABLE terms ADD COLUMN reference_url TEXT NOT NULL DEFAULT '';
ALTER TABLE terms ADD COLUMN archived INTEGER NOT NULL DEFAULT 0 CHECK (archived IN (0, 1));

CREATE TABLE term_tags (
    term_id TEXT NOT NULL REFERENCES terms(id) ON DELETE CASCADE,
    tag TEXT NOT NULL COLLATE NOCASE,
    PRIMARY KEY (term_id, tag)
);

CREATE INDEX ix_term_tags_tag ON term_tags(tag);
