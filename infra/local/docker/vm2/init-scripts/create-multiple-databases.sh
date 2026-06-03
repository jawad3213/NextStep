#!/bin/bash

set -e
set -u

function create_user_and_database() {
	local database=$1
	local db_user=$2
	local db_pass=$3
	
	echo "  Creating user '$db_user' and database '$database'"
	psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
	    CREATE USER $db_user WITH PASSWORD '$db_pass';
	    CREATE DATABASE $database;
	    GRANT ALL PRIVILEGES ON DATABASE $database TO $db_user;
	    ALTER DATABASE $database OWNER TO $db_user;
EOSQL

    # Special case for keycloak: create the schema
    if [ "$database" == "keycloak_db" ]; then
        echo "  Creating schema 'keycloak_schema' in '$database'"
        psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$database" <<-EOSQL
            CREATE SCHEMA IF NOT EXISTS keycloak_schema AUTHORIZATION $db_user;
EOSQL
    fi
}

if [ -n "$POSTGRES_MULTIPLE_DATABASES" ]; then
	echo "Multiple database creation requested: $POSTGRES_MULTIPLE_DATABASES"
	for db_info in $(echo $POSTGRES_MULTIPLE_DATABASES | tr ',' ' '); do
		db=$(echo $db_info | cut -d: -f1)
		usr=$(echo $db_info | cut -d: -f2)
		pwd=$(echo $db_info | cut -d: -f3)
		create_user_and_database $db $usr $pwd
	done
	echo "Multiple databases created"
	
fi
