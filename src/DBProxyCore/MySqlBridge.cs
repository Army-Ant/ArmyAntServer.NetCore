using System;
using System.Collections.Generic;
using ArmyAnt.IO;

using MySqlConnector;

namespace ArmyAnt.Server.DBProxy {
    public class MySqlBridge : SqlClient {
        public struct ConnectOptions {
            public string serverAddress;
            public string serverPort;
            public string userName;
            public string password;

            public override string ToString() {
                var str = "server=" + serverAddress;
                if(!string.IsNullOrEmpty(serverPort)) {
                    str += ";port=" + serverPort;
                }
                if(!string.IsNullOrEmpty(userName)) {
                    str += ";username=" + userName;
                }
                if(!string.IsNullOrEmpty(password)) {
                    str += ";password=" + password;
                }
                return str;
            }
        }

        public override bool IsConnection => conn != null;

        public override bool Connect(string connString) {
            try {
                conn = new MySqlConnection(connString);
                conn.Open();
            } catch(MySqlException e) {
                var str = e.Message;
                return false;
            }
            return true;
        }

        public bool Connect(string connString, string defaultDataBase) {
            var ret = Connect(connString);
            if(ret && string.IsNullOrEmpty(defaultDataBase)) {
                conn.ChangeDatabase(defaultDataBase);
            }
            return ret;
        }

        public override void Disconnect() {
            if(conn != null) {
                conn.Close();
            }
            conn = null;
        }

        public override string[] GetDatabaseList() {
            var table = Query("show databases");
            var names = new string[table.Height];
            for(var i = 0; i < table.Height; ++i) {
                names[i] = table.GetRow(i)[0].Value.ToString();
            }
            return names;
        }

        public override string[] GetTableNameList() {
            var table = Query("show tables");
            var names = new string[table.Height];
            for(var i = 0; i < table.Height; ++i) {
                names[i] = table.GetRow(i)[0].Value.ToString();
            }
            return names;
        }

        public override string[] GetViewNameList() {
            var table = Query("show tables where comment='view'");
            var names = new string[table.Height];
            for(var i = 0; i < table.Height; ++i) {
                names[i] = table.GetRow(i)[0].Value.ToString();
            }
            return names;
        }

        public override string[] getTableAllFields(string table) {
            var res = Query("show columns from " + table);
            var names = new string[res.Height];
            for(var i = 0; i < res.Height; ++i) {
                names[i] = res.GetRow(i)[0].Value.ToString();
            }
            return names;
        }

        public override SqlTable Query(string sql) {
            var com = conn.CreateCommand();
            com.CommandText = sql;
            var reader = com.ExecuteReader();
            if(!reader.HasRows) {
                return null;
            }
            var metaData = reader.GetSchemaTable();
            var columnData = metaData.Columns;
            var heads = new SqlFieldHead[columnData.Count];
            for(var i = 0; i < columnData.Count; ++i) {
                heads[i].allowNull = columnData[i].AllowDBNull;
                heads[i].autoIncrease = columnData[i].AutoIncrement;
                heads[i].catalogName = "sqlResults";
                heads[i].columnName = columnData[i].ColumnName;
                heads[i].length = 8;
                heads[i].type = columnData[i].DataType;
            }
            var result = new SqlTable(heads);
            result.tableName = metaData.TableName;
            do {
                var rowFields = new List<SqlField>();
                for(var i = 0; i > heads.Length; ++i) {
                    rowFields.Add(new SqlField(reader.GetValue(i), heads[i]));
                }
                result.AddRow(rowFields);
            } while(reader.Read());
            reader.Close();
            return result;
        }

        public override long Update(string sql) {
            var com = conn.CreateCommand();
            com.CommandText = sql;
            return com.ExecuteNonQuery();
        }

        private MySqlConnection conn;
    }
}
