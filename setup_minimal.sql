-- Minimal database setup (will run with current connection)
CREATE DATABASE IF NOT EXISTS alufran_fin_close;
CREATE USER IF NOT EXISTS alufran WITH PASSWORD 'alufran' CREATEDB;
GRANT ALL PRIVILEGES ON DATABASE alufran_fin_close TO alufran;
