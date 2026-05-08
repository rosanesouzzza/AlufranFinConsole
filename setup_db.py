#!/usr/bin/env python3
"""
Setup PostgreSQL database for Alufran Financial Console
"""
import psycopg2
from psycopg2 import sql
import sys
import os

def setup_database():
    # Connect to default postgres database
    try:
        conn = psycopg2.connect(
            host="localhost",
            port=5432,
            database="postgres",
            user="postgres",
            password=""  # Try empty password first
        )
    except psycopg2.OperationalError:
        # Try with default password
        try:
            conn = psycopg2.connect(
                host="localhost",
                port=5432,
                database="postgres",
                user="postgres",
                password="postgres"
            )
        except psycopg2.OperationalError as e:
            print(f"Cannot connect to PostgreSQL: {e}")
            sys.exit(1)

    conn.autocommit = True
    cur = conn.cursor()

    try:
        # Create database
        print("[1/3] Creating database 'alufran_fin_close'...")
        cur.execute(sql.SQL("DROP DATABASE IF EXISTS {} CASCADE").format(
            sql.Identifier("alufran_fin_close")
        ))
        cur.execute(sql.SQL("CREATE DATABASE {} WITH ENCODING = %s").format(
            sql.Identifier("alufran_fin_close")
        ), ("UTF8",))
        print("✓ Database created")

        # Create user
        print("[2/3] Creating user 'alufran'...")
        cur.execute(sql.SQL("DROP USER IF EXISTS {}").format(
            sql.Identifier("alufran")
        ))
        cur.execute(sql.SQL("CREATE USER {} WITH PASSWORD %s CREATEDB").format(
            sql.Identifier("alufran")
        ), ("alufran",))
        print("✓ User created")

        # Grant privileges
        print("[3/3] Granting privileges...")
        cur.execute(sql.SQL("GRANT ALL PRIVILEGES ON DATABASE {} TO {}").format(
            sql.Identifier("alufran_fin_close"),
            sql.Identifier("alufran")
        ))
        print("✓ Privileges granted")

        print("\n✅ Database setup complete!")
        print("You can now run: dotnet ef database update")

    except psycopg2.Error as e:
        print(f"Error: {e}")
        sys.exit(1)
    finally:
        cur.close()
        conn.close()

if __name__ == "__main__":
    setup_database()
