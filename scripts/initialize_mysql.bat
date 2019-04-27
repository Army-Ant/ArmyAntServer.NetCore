cd ..\external\mysql\mysql-8.0.11-winx64\bin

if not exist data (mkdir data)
mysqld --initialize-insecure

cd ..\..\..\..\scripts