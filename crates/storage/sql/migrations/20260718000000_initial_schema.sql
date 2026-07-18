CREATE TABLE IF NOT EXISTS admin_configs (
    id BIGINT PRIMARY KEY,
    source_bans BOOLEAN NOT NULL,
    is_blocked BOOLEAN NOT NULL
);

CREATE TABLE IF NOT EXISTS aliases (
    guild_id BIGINT NOT NULL,
    name VARCHAR(2000) NOT NULL,
    content VARCHAR(2000),
    PRIMARY KEY (guild_id, name)
);

CREATE UNLOGGED TABLE IF NOT EXISTS bans (
    guild_id BIGINT NOT NULL,
    user_id BIGINT NOT NULL,
    reason TEXT,
    avatar TEXT,
    PRIMARY KEY (guild_id, user_id)
);
CREATE INDEX IF NOT EXISTS bans_guild_id_idx ON bans (guild_id);
CREATE INDEX IF NOT EXISTS bans_user_id_idx ON bans (user_id);

CREATE TABLE IF NOT EXISTS escalation_histories (
    id SERIAL PRIMARY KEY,
    guild_id BIGINT NOT NULL,
    subject_id BIGINT NOT NULL,
    authorizer_id BIGINT NOT NULL,
    authorizer_name VARCHAR(255) NOT NULL,
    display_name VARCHAR(2000) NOT NULL,
    "timestamp" TIMESTAMPTZ DEFAULT now() NOT NULL,
    action BYTEA NOT NULL,
    level_delta INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS feeds (
    id SERIAL PRIMARY KEY,
    type VARCHAR(255) NOT NULL,
    source VARCHAR(8192) NOT NULL,
    last_updated TIMESTAMPTZ NOT NULL,
    CONSTRAINT feeds_type_source_key UNIQUE (type, source)
);

CREATE TABLE IF NOT EXISTS feed_channels (
    feed_id INTEGER REFERENCES feeds(id),
    channel_id BIGINT
);

CREATE TABLE IF NOT EXISTS members (
    guild_id BIGINT NOT NULL,
    user_id BIGINT NOT NULL,
    role_ids BIGINT[] NOT NULL,
    nickname VARCHAR(32),
    present BOOLEAN DEFAULT false NOT NULL,
    last_seen TIMESTAMPTZ DEFAULT now() NOT NULL,
    bot BOOLEAN DEFAULT false NOT NULL,
    premium_since TIMESTAMPTZ,
    avatar TEXT,
    PRIMARY KEY (guild_id, user_id)
);

CREATE TABLE IF NOT EXISTS oauth (
    refresh_token TEXT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    access_token TEXT NOT NULL,
    expiration TIMESTAMP WITHOUT TIME ZONE NOT NULL
);

CREATE TABLE IF NOT EXISTS pending_actions (
    id SERIAL PRIMARY KEY,
    "timestamp" TIMESTAMPTZ NOT NULL,
    data BYTEA NOT NULL
);

CREATE TABLE IF NOT EXISTS pending_deescalations (
    user_id BIGINT NOT NULL,
    guild_id BIGINT NOT NULL,
    expiration TIMESTAMPTZ NOT NULL,
    amount BIGINT NOT NULL,
    entry_id INTEGER NOT NULL REFERENCES escalation_histories(id),
    PRIMARY KEY (user_id, guild_id)
);

CREATE TABLE IF NOT EXISTS tags (
    guild_id BIGINT NOT NULL,
    tag VARCHAR(2000) NOT NULL,
    response VARCHAR(2000) NOT NULL,
    PRIMARY KEY (guild_id, tag)
);

CREATE TABLE IF NOT EXISTS usernames (
    user_id BIGINT NOT NULL,
    "timestamp" TIMESTAMPTZ DEFAULT now() NOT NULL,
    name VARCHAR(32) NOT NULL,
    discriminator INTEGER,
    PRIMARY KEY (user_id, "timestamp"),
    CONSTRAINT idx_unique_username UNIQUE (user_id, name, discriminator)
);
CREATE INDEX IF NOT EXISTS idx_username_user_id ON usernames (user_id);

DO $$
BEGIN
    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'grafana') THEN
        GRANT SELECT ON ALL TABLES IN SCHEMA public TO grafana;
    END IF;
END $$;
