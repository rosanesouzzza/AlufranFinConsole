-- ============================================
-- SETUP POSTGRESQL PARA ALUFRAN FIN CLOSE
-- ============================================

-- Criar database
CREATE DATABASE alufran_fin_close
    WITH
    ENCODING = 'UTF8'
    LC_COLLATE = 'C'
    LC_CTYPE = 'C'
    TEMPLATE = template0;

-- Criar user
CREATE USER alufran WITH PASSWORD 'alufran' CREATEDB;

-- Dar permissões
GRANT ALL PRIVILEGES ON DATABASE alufran_fin_close TO alufran;

-- Conectar ao banco novo e configurar
\c alufran_fin_close

-- Criar extensões
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "hstore";

-- Dar default schema
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO alufran;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO alufran;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON FUNCTIONS TO alufran;

COMMIT;

-- Confirmação
SELECT version();
SELECT datname FROM pg_database WHERE datname = 'alufran_fin_close';
SELECT usename FROM pg_user WHERE usename = 'alufran';
