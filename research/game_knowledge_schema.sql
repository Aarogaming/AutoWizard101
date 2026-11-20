-- Maelstrom local game knowledge schema, version 1.
-- Run on a new database. Every writer/reader must enable foreign_keys explicitly.
-- Store concise sourced facts, not mirrored pages, executable scripts, credentials or prompts.
PRAGMA foreign_keys = ON;
BEGIN IMMEDIATE;

CREATE TABLE knowledge_schema (
    version INTEGER PRIMARY KEY CHECK (version = 1),
    installed_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
);

CREATE TABLE games (
    game_id TEXT PRIMARY KEY NOT NULL CHECK (game_id LIKE 'maelstrom.game.%'),
    title TEXT NOT NULL CHECK (length(trim(title)) BETWEEN 1 AND 200),
    identity_note TEXT NOT NULL DEFAULT ''
);

CREATE TABLE domains (
    game_id TEXT NOT NULL REFERENCES games(game_id),
    domain_id TEXT NOT NULL CHECK (length(trim(domain_id)) BETWEEN 1 AND 80),
    title TEXT NOT NULL CHECK (length(trim(title)) BETWEEN 1 AND 200),
    PRIMARY KEY (game_id, domain_id)
);

CREATE TABLE entities (
    entity_id TEXT PRIMARY KEY NOT NULL CHECK (entity_id LIKE 'maelstrom.entity.%'),
    game_id TEXT NOT NULL,
    domain_id TEXT NOT NULL,
    kind TEXT NOT NULL CHECK (kind IN
        ('quest', 'area', 'route', 'spell', 'enemy', 'item', 'pet', 'minigame',
         'npc', 'mechanic', 'resource', 'other')),
    display_name TEXT NOT NULL CHECK (length(trim(display_name)) BETWEEN 1 AND 240),
    identity_note TEXT NOT NULL DEFAULT '',
    UNIQUE (entity_id, game_id),
    FOREIGN KEY (game_id, domain_id) REFERENCES domains(game_id, domain_id)
);

CREATE TABLE relationship_types (
    type_id TEXT PRIMARY KEY NOT NULL CHECK (length(trim(type_id)) BETWEEN 1 AND 80),
    meaning TEXT NOT NULL CHECK (length(trim(meaning)) BETWEEN 1 AND 1000)
);
INSERT INTO relationship_types(type_id, meaning) VALUES
    ('located-in', 'The subject has a location in the object; assertion needs evidence.'),
    ('connects-to', 'A candidate traversal connection; not permission or proof of reachability.'),
    ('requires', 'The subject depends on the object under separately recorded conditions.'),
    ('rewards', 'The subject can reward the object under separately recorded conditions.'),
    ('uses', 'The subject uses the object under separately recorded conditions.'),
    ('contains', 'The subject contains the object under separately recorded conditions.'),
    ('interacts-with', 'A typed but nonspecific interaction requiring further claims.');

CREATE TABLE relationships (
    relationship_id TEXT PRIMARY KEY NOT NULL CHECK (relationship_id LIKE 'maelstrom.relationship.%'),
    game_id TEXT NOT NULL REFERENCES games(game_id),
    subject_entity_id TEXT NOT NULL,
    type_id TEXT NOT NULL REFERENCES relationship_types(type_id),
    object_entity_id TEXT NOT NULL,
    UNIQUE (relationship_id, game_id),
    UNIQUE (game_id, subject_entity_id, type_id, object_entity_id),
    FOREIGN KEY (subject_entity_id, game_id) REFERENCES entities(entity_id, game_id),
    FOREIGN KEY (object_entity_id, game_id) REFERENCES entities(entity_id, game_id)
    -- An edge identifies a proposition. Supporting claims, not the edge alone,
    -- establish whether it is true, available, traversable or qualified.
);

CREATE TABLE sources (
    source_id TEXT PRIMARY KEY NOT NULL CHECK (source_id LIKE 'maelstrom.source.%'),
    kind TEXT NOT NULL CHECK (kind IN
        ('official-guide', 'release-notes', 'wiki', 'community', 'repository',
         'user-statement', 'local-artifact', 'other')),
    locator TEXT NOT NULL CHECK (length(trim(locator)) BETWEEN 1 AND 4096),
    identity_note TEXT NOT NULL DEFAULT ''
);

CREATE TABLE source_revisions (
    source_revision_id TEXT PRIMARY KEY NOT NULL CHECK (source_revision_id LIKE 'maelstrom.source-revision.%'),
    source_id TEXT NOT NULL REFERENCES sources(source_id),
    revision_no INTEGER NOT NULL CHECK (typeof(revision_no) = 'integer' AND revision_no >= 1),
    previous_revision_id TEXT,
    title TEXT NOT NULL CHECK (length(trim(title)) BETWEEN 1 AND 500),
    publisher TEXT,
    published_on TEXT CHECK (published_on IS NULL OR
        (length(published_on) = 10 AND date(published_on, '+0 days') IS NOT NULL
         AND date(published_on, '+0 days') = published_on)),
    modified_on TEXT CHECK (modified_on IS NULL OR
        (length(modified_on) = 10 AND date(modified_on, '+0 days') IS NOT NULL
         AND date(modified_on, '+0 days') = modified_on)),
    retrieved_on TEXT NOT NULL CHECK
        (length(retrieved_on) = 10 AND date(retrieved_on, '+0 days') IS NOT NULL
         AND date(retrieved_on, '+0 days') = retrieved_on),
    retrieved_timezone TEXT NOT NULL CHECK (length(trim(retrieved_timezone)) BETWEEN 1 AND 100),
    content_sha256 TEXT CHECK (content_sha256 IS NULL OR
        (length(content_sha256) = 64 AND content_sha256 NOT GLOB '*[^0-9a-fA-F]*')),
    revision_note TEXT NOT NULL DEFAULT '',
    UNIQUE (source_revision_id, source_id),
    UNIQUE (source_id, revision_no),
    FOREIGN KEY (previous_revision_id, source_id)
        REFERENCES source_revisions(source_revision_id, source_id),
    CHECK ((revision_no = 1 AND previous_revision_id IS NULL) OR
           (revision_no > 1 AND previous_revision_id IS NOT NULL
            AND previous_revision_id <> source_revision_id))
);

CREATE TABLE claim_series (
    series_id TEXT PRIMARY KEY NOT NULL CHECK (series_id LIKE 'maelstrom.assertion.%'),
    game_id TEXT NOT NULL REFERENCES games(game_id),
    entity_id TEXT,
    relationship_id TEXT,
    predicate TEXT NOT NULL CHECK (length(trim(predicate)) BETWEEN 1 AND 160),
    UNIQUE (series_id, game_id),
    FOREIGN KEY (entity_id, game_id) REFERENCES entities(entity_id, game_id),
    FOREIGN KEY (relationship_id, game_id) REFERENCES relationships(relationship_id, game_id),
    CHECK ((entity_id IS NOT NULL) + (relationship_id IS NOT NULL) = 1)
    -- Competing series may have the same subject and predicate.
);

CREATE TABLE claims (
    claim_id TEXT PRIMARY KEY NOT NULL CHECK (claim_id LIKE 'maelstrom.claim.%'),
    series_id TEXT NOT NULL,
    game_id TEXT NOT NULL,
    revision_no INTEGER NOT NULL CHECK (typeof(revision_no) = 'integer' AND revision_no >= 1),
    supersedes_claim_id TEXT,
    disposition TEXT NOT NULL DEFAULT 'asserted'
        CHECK (disposition IN ('asserted', 'corrected', 'retracted')),
    statement TEXT NOT NULL CHECK (length(trim(statement)) BETWEEN 1 AND 8000),
    value_kind TEXT NOT NULL CHECK (value_kind IN ('text', 'number', 'boolean', 'entity', 'unknown')),
    value_text TEXT,
    value_number REAL,
    value_entity_id TEXT REFERENCES entities(entity_id),
    uncertainty TEXT NOT NULL CHECK (uncertainty IN
        ('unknown', 'tentative', 'documented', 'corroborated', 'disputed')),
    confidence REAL CHECK (confidence IS NULL OR
        (typeof(confidence) IN ('integer', 'real') AND confidence BETWEEN 0 AND 1)),
    game_version TEXT CHECK (game_version IS NULL OR length(trim(game_version)) BETWEEN 1 AND 120),
    region TEXT CHECK (region IS NULL OR length(trim(region)) BETWEEN 1 AND 120),
    platform TEXT CHECK (platform IS NULL OR length(trim(platform)) BETWEEN 1 AND 120),
    realm TEXT NOT NULL DEFAULT 'unknown' CHECK (realm IN ('live', 'test', 'preview', 'unknown')),
    effective_from TEXT CHECK (effective_from IS NULL OR
        (length(effective_from) = 10 AND date(effective_from, '+0 days') IS NOT NULL
         AND date(effective_from, '+0 days') = effective_from)),
    effective_until TEXT CHECK (effective_until IS NULL OR
        (length(effective_until) = 10 AND date(effective_until, '+0 days') IS NOT NULL
         AND date(effective_until, '+0 days') = effective_until)),
    primary_source_revision_id TEXT NOT NULL REFERENCES source_revisions(source_revision_id),
    source_location TEXT NOT NULL DEFAULT '',
    authored_by TEXT NOT NULL CHECK (authored_by IN ('import', 'assistant-review', 'user-review', 'tool')),
    recorded_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now')),
    UNIQUE (claim_id, game_id),
    UNIQUE (claim_id, series_id),
    UNIQUE (series_id, revision_no),
    FOREIGN KEY (series_id, game_id) REFERENCES claim_series(series_id, game_id),
    FOREIGN KEY (supersedes_claim_id, series_id) REFERENCES claims(claim_id, series_id),
    CHECK ((revision_no = 1 AND supersedes_claim_id IS NULL) OR
           (revision_no > 1 AND supersedes_claim_id IS NOT NULL
            AND supersedes_claim_id <> claim_id)),
    CHECK (disposition = 'asserted' OR supersedes_claim_id IS NOT NULL),
    CHECK (effective_from IS NULL OR effective_until IS NULL OR effective_from <= effective_until),
    CHECK (
        (value_kind = 'unknown' AND value_text IS NULL AND value_number IS NULL AND value_entity_id IS NULL)
        OR (value_kind = 'text' AND value_text IS NOT NULL AND length(value_text) <= 8000 AND value_number IS NULL AND value_entity_id IS NULL)
        OR (value_kind = 'number' AND value_text IS NULL AND typeof(value_number) IN ('integer', 'real') AND abs(value_number) <= 1.0e100 AND value_entity_id IS NULL)
        OR (value_kind = 'boolean' AND value_text IS NULL AND value_number IN (0, 1) AND value_number IS NOT NULL AND value_entity_id IS NULL)
        OR (value_kind = 'entity' AND value_text IS NULL AND value_number IS NULL AND value_entity_id IS NOT NULL)
    )
);

CREATE TABLE claim_evidence (
    evidence_id TEXT PRIMARY KEY NOT NULL CHECK (evidence_id LIKE 'maelstrom.evidence.%'),
    claim_id TEXT NOT NULL REFERENCES claims(claim_id),
    source_revision_id TEXT NOT NULL REFERENCES source_revisions(source_revision_id),
    stance TEXT NOT NULL CHECK (stance IN ('supports', 'refutes', 'context')),
    source_location TEXT NOT NULL DEFAULT '',
    note TEXT NOT NULL DEFAULT '' CHECK (length(note) <= 4000),
    UNIQUE (claim_id, source_revision_id, stance, source_location)
);

CREATE TABLE claim_links (
    link_id TEXT PRIMARY KEY NOT NULL CHECK (link_id LIKE 'maelstrom.claim-link.%'),
    left_claim_id TEXT NOT NULL REFERENCES claims(claim_id),
    right_claim_id TEXT NOT NULL REFERENCES claims(claim_id),
    relation TEXT NOT NULL CHECK (relation IN ('contradicts', 'refines', 'different-scope', 'resolves')),
    reason TEXT NOT NULL CHECK (length(trim(reason)) BETWEEN 1 AND 4000),
    evidence_source_revision_id TEXT REFERENCES source_revisions(source_revision_id),
    CHECK (left_claim_id <> right_claim_id),
    UNIQUE (left_claim_id, right_claim_id, relation)
);

CREATE TABLE observations (
    observation_id TEXT PRIMARY KEY NOT NULL CHECK (observation_id LIKE 'maelstrom.observation.%'),
    game_id TEXT NOT NULL REFERENCES games(game_id),
    entity_id TEXT,
    environment_kind TEXT NOT NULL CHECK (environment_kind IN
        ('live-client', 'recorded-client', 'synthetic', 'manual-report')),
    inputs_by TEXT NOT NULL CHECK (inputs_by IN ('none', 'human', 'maelstrom', 'other', 'unknown')),
    observed_on TEXT NOT NULL CHECK
        (length(observed_on) = 10 AND date(observed_on, '+0 days') IS NOT NULL
         AND date(observed_on, '+0 days') = observed_on),
    observed_timezone TEXT NOT NULL CHECK (length(trim(observed_timezone)) BETWEEN 1 AND 100),
    game_version TEXT CHECK (game_version IS NULL OR length(trim(game_version)) BETWEEN 1 AND 120),
    region TEXT CHECK (region IS NULL OR length(trim(region)) BETWEEN 1 AND 120),
    platform TEXT CHECK (platform IS NULL OR length(trim(platform)) BETWEEN 1 AND 120),
    realm TEXT NOT NULL DEFAULT 'unknown' CHECK (realm IN ('live', 'test', 'preview', 'unknown')),
    artifact_locator TEXT,
    artifact_sha256 TEXT,
    fact TEXT NOT NULL CHECK (length(trim(fact)) BETWEEN 1 AND 8000),
    recorded_by TEXT NOT NULL CHECK (recorded_by IN ('assistant-review', 'user-review', 'tool')),
    UNIQUE (observation_id, game_id),
    FOREIGN KEY (entity_id, game_id) REFERENCES entities(entity_id, game_id),
    CHECK ((artifact_locator IS NULL AND artifact_sha256 IS NULL) OR
        (artifact_locator IS NOT NULL AND length(trim(artifact_locator)) BETWEEN 1 AND 4096
         AND artifact_sha256 IS NOT NULL AND length(artifact_sha256) = 64
         AND artifact_sha256 NOT GLOB '*[^0-9a-fA-F]*'))
);

CREATE TABLE qualifications (
    qualification_id TEXT PRIMARY KEY NOT NULL CHECK (qualification_id LIKE 'maelstrom.qualification.%'),
    game_id TEXT NOT NULL REFERENCES games(game_id),
    claim_id TEXT NOT NULL,
    observation_id TEXT NOT NULL,
    level TEXT NOT NULL CHECK (level IN ('source-context', 'recorded-client', 'current-client', 'synthetic')),
    result TEXT NOT NULL CHECK (result IN ('supports', 'contradicts', 'inconclusive')),
    note TEXT NOT NULL CHECK (length(trim(note)) BETWEEN 1 AND 4000),
    recorded_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now')),
    FOREIGN KEY (claim_id, game_id) REFERENCES claims(claim_id, game_id),
    FOREIGN KEY (observation_id, game_id) REFERENCES observations(observation_id, game_id)
    -- This is scoped evidence about a claim, never an execution permission.
);

CREATE TABLE runtime_candidates (
    candidate_id TEXT PRIMARY KEY NOT NULL CHECK (candidate_id LIKE 'maelstrom.candidate.%'),
    game_id TEXT NOT NULL REFERENCES games(game_id),
    entity_id TEXT,
    kind TEXT NOT NULL CHECK (kind IN ('action', 'routine', 'recipe', 'recognizer')),
    registry_ref TEXT NOT NULL CHECK (length(trim(registry_ref)) BETWEEN 1 AND 240),
    pack_id TEXT NOT NULL CHECK (length(trim(pack_id)) BETWEEN 1 AND 160),
    pack_version TEXT NOT NULL CHECK (length(trim(pack_version)) BETWEEN 1 AND 120),
    artifact_sha256 TEXT CHECK (artifact_sha256 IS NULL OR
        (length(artifact_sha256) = 64 AND artifact_sha256 NOT GLOB '*[^0-9a-fA-F]*')),
    enabled INTEGER NOT NULL DEFAULT 0 CHECK (typeof(enabled) = 'integer' AND enabled = 0),
    status TEXT NOT NULL DEFAULT 'disabled-candidate' CHECK (status = 'disabled-candidate'),
    note TEXT NOT NULL DEFAULT '' CHECK (length(note) <= 4000),
    UNIQUE (candidate_id, game_id),
    FOREIGN KEY (entity_id, game_id) REFERENCES entities(entity_id, game_id)
);

CREATE TABLE candidate_claims (
    candidate_id TEXT NOT NULL,
    claim_id TEXT NOT NULL,
    game_id TEXT NOT NULL,
    PRIMARY KEY (candidate_id, claim_id),
    FOREIGN KEY (candidate_id, game_id) REFERENCES runtime_candidates(candidate_id, game_id),
    FOREIGN KEY (claim_id, game_id) REFERENCES claims(claim_id, game_id)
);

CREATE INDEX entities_by_domain ON entities(game_id, domain_id, kind, display_name);
CREATE INDEX relationships_from ON relationships(game_id, subject_entity_id, type_id);
CREATE INDEX relationships_to ON relationships(game_id, object_entity_id, type_id);
CREATE INDEX revisions_by_source ON source_revisions(source_id, retrieved_on);
CREATE INDEX series_by_entity ON claim_series(game_id, entity_id, predicate);
CREATE INDEX series_by_relationship ON claim_series(game_id, relationship_id, predicate);
CREATE INDEX claims_by_scope ON claims(game_id, realm, region, platform, game_version);
CREATE INDEX claims_by_source ON claims(primary_source_revision_id);
CREATE INDEX claims_by_predecessor ON claims(supersedes_claim_id);
CREATE INDEX evidence_by_source ON claim_evidence(source_revision_id);
CREATE INDEX links_by_right_claim ON claim_links(right_claim_id);
CREATE INDEX observations_by_scope ON observations(game_id, environment_kind, realm, game_version, observed_on);
CREATE INDEX qualifications_by_claim ON qualifications(claim_id, level, result);
CREATE INDEX qualifications_by_observation ON qualifications(observation_id);
CREATE INDEX candidates_by_game ON runtime_candidates(game_id, kind, registry_ref);
CREATE INDEX candidate_claims_by_claim ON candidate_claims(claim_id);

CREATE TRIGGER source_revision_sequence BEFORE INSERT ON source_revisions
WHEN NEW.previous_revision_id IS NOT NULL AND NEW.revision_no <>
    (SELECT revision_no + 1 FROM source_revisions WHERE source_revision_id = NEW.previous_revision_id)
BEGIN SELECT RAISE(ABORT, 'Source revisions must follow their predecessor'); END;

CREATE TRIGGER claim_revision_sequence BEFORE INSERT ON claims
WHEN NEW.supersedes_claim_id IS NOT NULL AND NEW.revision_no <>
    (SELECT revision_no + 1 FROM claims WHERE claim_id = NEW.supersedes_claim_id)
BEGIN SELECT RAISE(ABORT, 'Claim revisions must follow their predecessor'); END;

CREATE TRIGGER qualification_scope BEFORE INSERT ON qualifications
WHEN NOT EXISTS (
    SELECT 1 FROM observations o WHERE o.observation_id = NEW.observation_id
    AND (
        NEW.level = 'source-context'
        OR (NEW.level = 'synthetic' AND o.environment_kind = 'synthetic')
        OR (NEW.level = 'recorded-client' AND o.environment_kind = 'recorded-client')
        OR (NEW.level = 'current-client' AND o.environment_kind = 'live-client'
            AND o.game_version IS NOT NULL AND o.region IS NOT NULL AND o.platform IS NOT NULL
            AND o.realm IN ('live', 'test') AND o.artifact_sha256 IS NOT NULL)
    )
)
BEGIN SELECT RAISE(ABORT, 'Qualification needs matching, explicit observation scope'); END;

-- Leaf means latest recorded revision in its own series; it is not resolved truth.
CREATE VIEW claim_leaves AS
SELECT c.* FROM claims c
WHERE NOT EXISTS (SELECT 1 FROM claims newer WHERE newer.supersedes_claim_id = c.claim_id);

-- History is append-only. Corrections create revisions/links, not UPDATE/REPLACE.
-- These integrity triggers are not a security boundary against the database owner.
CREATE TRIGGER knowledge_schema_no_update BEFORE UPDATE ON knowledge_schema
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER knowledge_schema_no_delete BEFORE DELETE ON knowledge_schema
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER games_no_update BEFORE UPDATE ON games
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER games_no_delete BEFORE DELETE ON games
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER domains_no_update BEFORE UPDATE ON domains
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER domains_no_delete BEFORE DELETE ON domains
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER entities_no_update BEFORE UPDATE ON entities
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER entities_no_delete BEFORE DELETE ON entities
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER relationship_types_no_update BEFORE UPDATE ON relationship_types
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER relationship_types_no_delete BEFORE DELETE ON relationship_types
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER relationships_no_update BEFORE UPDATE ON relationships
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER relationships_no_delete BEFORE DELETE ON relationships
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER sources_no_update BEFORE UPDATE ON sources
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER sources_no_delete BEFORE DELETE ON sources
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER source_revisions_no_update BEFORE UPDATE ON source_revisions
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER source_revisions_no_delete BEFORE DELETE ON source_revisions
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER claim_series_no_update BEFORE UPDATE ON claim_series
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER claim_series_no_delete BEFORE DELETE ON claim_series
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER claims_no_update BEFORE UPDATE ON claims
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER claims_no_delete BEFORE DELETE ON claims
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER claim_evidence_no_update BEFORE UPDATE ON claim_evidence
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER claim_evidence_no_delete BEFORE DELETE ON claim_evidence
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER claim_links_no_update BEFORE UPDATE ON claim_links
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER claim_links_no_delete BEFORE DELETE ON claim_links
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER observations_no_update BEFORE UPDATE ON observations
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER observations_no_delete BEFORE DELETE ON observations
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER qualifications_no_update BEFORE UPDATE ON qualifications
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER qualifications_no_delete BEFORE DELETE ON qualifications
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER runtime_candidates_no_update BEFORE UPDATE ON runtime_candidates
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER runtime_candidates_no_delete BEFORE DELETE ON runtime_candidates
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER candidate_claims_no_update BEFORE UPDATE ON candidate_claims
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;
CREATE TRIGGER candidate_claims_no_delete BEFORE DELETE ON candidate_claims
BEGIN SELECT RAISE(ABORT, 'Knowledge history is append-only'); END;

-- Evidence from a new version/realm must not refute or validate a differently
-- scoped historical claim merely because the behavior changed over time.
CREATE TRIGGER qualification_claim_scope BEFORE INSERT ON qualifications
WHEN NEW.level IN ('current-client', 'recorded-client') AND NEW.result <> 'inconclusive'
AND NOT EXISTS (
    SELECT 1 FROM claims c JOIN observations o
        ON o.observation_id = NEW.observation_id
    WHERE c.claim_id = NEW.claim_id
      AND (c.game_version IS NULL OR c.game_version = o.game_version)
      AND (c.region IS NULL OR c.region = o.region)
      AND (c.platform IS NULL OR c.platform = o.platform)
      AND (c.realm = 'unknown' OR c.realm = o.realm)
      AND (c.effective_from IS NULL OR o.observed_on >= c.effective_from)
      AND (c.effective_until IS NULL OR o.observed_on <= c.effective_until)
)
BEGIN SELECT RAISE(ABORT, 'Observation and claim scopes differ; record an inconclusive or different-scope link'); END;

-- Reject duplicate keys before SQLite can implement INSERT OR REPLACE as a
-- delete/insert. This preserves history even when recursive_triggers is off.
CREATE TRIGGER knowledge_schema_no_replace BEFORE INSERT ON knowledge_schema
WHEN EXISTS (SELECT 1 FROM knowledge_schema WHERE (version = NEW.version))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER games_no_replace BEFORE INSERT ON games
WHEN EXISTS (SELECT 1 FROM games WHERE (game_id = NEW.game_id))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER domains_no_replace BEFORE INSERT ON domains
WHEN EXISTS (SELECT 1 FROM domains WHERE (game_id = NEW.game_id AND domain_id = NEW.domain_id))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER entities_no_replace BEFORE INSERT ON entities
WHEN EXISTS (SELECT 1 FROM entities WHERE (entity_id = NEW.entity_id))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER relationship_types_no_replace BEFORE INSERT ON relationship_types
WHEN EXISTS (SELECT 1 FROM relationship_types WHERE (type_id = NEW.type_id))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER relationships_no_replace BEFORE INSERT ON relationships
WHEN EXISTS (SELECT 1 FROM relationships WHERE (relationship_id = NEW.relationship_id) OR (game_id = NEW.game_id AND subject_entity_id = NEW.subject_entity_id AND type_id = NEW.type_id AND object_entity_id = NEW.object_entity_id))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER sources_no_replace BEFORE INSERT ON sources
WHEN EXISTS (SELECT 1 FROM sources WHERE (source_id = NEW.source_id))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER source_revisions_no_replace BEFORE INSERT ON source_revisions
WHEN EXISTS (SELECT 1 FROM source_revisions WHERE (source_revision_id = NEW.source_revision_id) OR (source_id = NEW.source_id AND revision_no = NEW.revision_no))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER claim_series_no_replace BEFORE INSERT ON claim_series
WHEN EXISTS (SELECT 1 FROM claim_series WHERE (series_id = NEW.series_id))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER claims_no_replace BEFORE INSERT ON claims
WHEN EXISTS (SELECT 1 FROM claims WHERE (claim_id = NEW.claim_id) OR (series_id = NEW.series_id AND revision_no = NEW.revision_no))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER claim_evidence_no_replace BEFORE INSERT ON claim_evidence
WHEN EXISTS (SELECT 1 FROM claim_evidence WHERE (evidence_id = NEW.evidence_id) OR (claim_id = NEW.claim_id AND source_revision_id = NEW.source_revision_id AND stance = NEW.stance AND source_location = NEW.source_location))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER claim_links_no_replace BEFORE INSERT ON claim_links
WHEN EXISTS (SELECT 1 FROM claim_links WHERE (link_id = NEW.link_id) OR (left_claim_id = NEW.left_claim_id AND right_claim_id = NEW.right_claim_id AND relation = NEW.relation))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER observations_no_replace BEFORE INSERT ON observations
WHEN EXISTS (SELECT 1 FROM observations WHERE (observation_id = NEW.observation_id))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER qualifications_no_replace BEFORE INSERT ON qualifications
WHEN EXISTS (SELECT 1 FROM qualifications WHERE (qualification_id = NEW.qualification_id))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER runtime_candidates_no_replace BEFORE INSERT ON runtime_candidates
WHEN EXISTS (SELECT 1 FROM runtime_candidates WHERE (candidate_id = NEW.candidate_id))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;
CREATE TRIGGER candidate_claims_no_replace BEFORE INSERT ON candidate_claims
WHEN EXISTS (SELECT 1 FROM candidate_claims WHERE (candidate_id = NEW.candidate_id AND claim_id = NEW.claim_id))
BEGIN SELECT RAISE(ABORT, 'Existing knowledge records cannot be replaced'); END;

-- Mark initialization only after all tables, indexes and guards were created.
INSERT INTO knowledge_schema(version) VALUES (1);
COMMIT;


