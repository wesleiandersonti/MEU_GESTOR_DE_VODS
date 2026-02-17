CREATE DATABASE IF NOT EXISTS {{DB_NAME}} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE {{DB_NAME}};

CREATE TABLE IF NOT EXISTS yt_channels (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  name VARCHAR(255) NOT NULL,
  category VARCHAR(120) NOT NULL,
  channel_url VARCHAR(500) NOT NULL,
  live_url VARCHAR(500) NOT NULL,
  enabled TINYINT(1) NOT NULL DEFAULT 1,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY uq_yt_channels_channel_url (channel_url),
  KEY idx_yt_channels_enabled_category (enabled, category),
  KEY idx_yt_channels_updated_at (updated_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS yt_channel_status (
  channel_id BIGINT UNSIGNED NOT NULL,
  is_live TINYINT(1) NOT NULL DEFAULT 0,
  is_online TINYINT(1) NOT NULL DEFAULT 0,
  live_video_id VARCHAR(64) NULL,
  stream_url TEXT NULL,
  format ENUM('HLS', 'DASH') NULL,
  last_http_code SMALLINT NULL,
  last_checked_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  error_code ENUM('NO_LIVE', 'SCHEDULED', 'BLOCKED', 'TIMEOUT', 'YTDLP_FAIL', 'HTTP_FAIL') NULL,
  error_message VARCHAR(1000) NULL,
  PRIMARY KEY (channel_id),
  KEY idx_yt_status_online_checked (is_online, last_checked_at),
  KEY idx_yt_status_live_checked (is_live, last_checked_at),
  KEY idx_yt_status_error_checked (error_code, last_checked_at),
  CONSTRAINT fk_yt_status_channel FOREIGN KEY (channel_id)
    REFERENCES yt_channels (id)
    ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS yt_checks_history (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  channel_id BIGINT UNSIGNED NOT NULL,
  checked_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  result ENUM('ONLINE', 'NO_LIVE', 'SCHEDULED', 'BLOCKED', 'TIMEOUT', 'YTDLP_FAIL', 'HTTP_FAIL') NOT NULL,
  details JSON NULL,
  duration_ms INT UNSIGNED NOT NULL,
  PRIMARY KEY (id),
  KEY idx_yt_history_channel_checked (channel_id, checked_at),
  KEY idx_yt_history_result_checked (result, checked_at),
  CONSTRAINT fk_yt_history_channel FOREIGN KEY (channel_id)
    REFERENCES yt_channels (id)
    ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS yt_exports (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  type ENUM('M3U') NOT NULL,
  file_path VARCHAR(800) NOT NULL,
  channels_count INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  KEY idx_yt_exports_created_at (created_at),
  KEY idx_yt_exports_type_created (type, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
