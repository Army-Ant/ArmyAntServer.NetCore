#!/bin/bash

INIT_SQL_PATH=../res/sqlScripts/initializeNewEmptyDB_MariaDB.sql
MYSQL_DATA_PATH=/data/mysql/data

mysqld --initialize-insecure

echo run initialization sql script
mysql -h localhost -u root < $INIT_SQL_PATH
echo successful !

