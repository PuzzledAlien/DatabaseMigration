﻿using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseMigration.Core
{
    public class MySqlInterpreter : DbInterpreter
    {
        #region Property
        public override string CommandParameterChar { get { return "@"; } }
        public override char QuotationLeftChar { get { return '`'; } }
        public override char QuotationRightChar { get { return '`'; } }
        public override DatabaseType DatabaseType { get { return DatabaseType.MySql; } }

        public readonly string DbCharset = "utf8";
        public readonly string DbCharsetCollation = "utf8_bin";
        #endregion

        #region Constructor
        public MySqlInterpreter(ConnectionInfo connectionInfo, GenerateScriptOption options) : base(connectionInfo, options) { }
        #endregion

        #region Common Method
        public override DbConnector GetDbConnector()
        {
            return new DbConnector(new MySqlProvider(), new MySqlConnectionBuilder(), this.ConnectionInfo);
        }

        protected override IEnumerable<DbParameter> BuildCommandParameters(Dictionary<string, object> paramaters)
        {
            foreach (KeyValuePair<string, object> kp in paramaters)
            {
                yield return new MySqlParameter(kp.Key, kp.Value);
            }
        }
        #endregion

        #region Database
        public override List<Database> GetDatabases()
        {
            DbConnector dbConnector = this.GetDbConnector();

            string sql = $@"SELECT SCHEMA_NAME AS `Name` FROM INFORMATION_SCHEMA.`SCHEMATA`";

            return base.GetDatabases(dbConnector, sql);
        }
        #endregion

        #region Table
        public override List<Table> GetTables(params string[] tableNames)
        {
            DbConnector dbConnector = this.GetDbConnector();

            string sql = $@"SELECT TABLE_SCHEMA AS `Owner`, TABLE_NAME AS `Name`, TABLE_COMMENT AS `Comment`,
                        1 AS `IdentitySeed`, 1 AS `IdentityIncrement`
                        FROM INFORMATION_SCHEMA.`TABLES`
                        WHERE TABLE_SCHEMA ='{ConnectionInfo.Database}' 
                        ";

            if (tableNames != null && tableNames.Count() > 0)
            {
                string strTableNames = StringHelper.GetSingleQuotedString(tableNames);
                sql += $" AND TABLE_NAME IN ({ strTableNames })";
            }

            sql += " ORDER BY TABLE_NAME";

            return base.GetTables(dbConnector, sql);
        }
        #endregion

        #region Table Column
        public override List<TableColumn> GetTableColumns(params string[] tableNames)
        {
            DbConnector dbConnector = this.GetDbConnector();

            string sql = $@"SELECT TABLE_SCHEMA AS `Owner`, TABLE_NAME AS TableName, COLUMN_NAME AS ColumnName, COLUMN_TYPE AS DataType, 
                        CHARACTER_MAXIMUM_LENGTH AS MaxLength, IS_NULLABLE AS IsNullable,ORDINAL_POSITION AS `Order`,
                        NUMERIC_PRECISION AS `Precision`,NUMERIC_SCALE AS `Scale`, COLUMN_DEFAULT AS `DefaultValue`,COLUMN_COMMENT AS `Comment`,
                        CASE EXTRA WHEN 'auto_increment' THEN 1 ELSE 0 END AS `IsIdentity`
                        FROM INFORMATION_SCHEMA.`COLUMNS`
                        WHERE TABLE_SCHEMA ='{ConnectionInfo.Database}'";

            if (tableNames != null && tableNames.Count() > 0)
            {
                string strTableNames = StringHelper.GetSingleQuotedString(tableNames);
                sql += $" AND TABLE_NAME IN ({ strTableNames })";
            }

            return base.GetTableColumns(dbConnector, sql);
        }
        #endregion

        #region Table Primary Key
        public override List<TablePrimaryKey> GetTablePrimaryKeys(params string[] tableNames)
        {
            DbConnector dbConnector = this.GetDbConnector();

            string sql = $@"SELECT C.`CONSTRAINT_SCHEMA` AS `Owner`, K.TABLE_NAME AS TableName, K.CONSTRAINT_NAME AS KeyName, K.COLUMN_NAME AS ColumnName, K.`ORDINAL_POSITION` AS `Order`, 0 AS IsDesc
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS C
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS K ON C.TABLE_NAME = K.TABLE_NAME AND C.CONSTRAINT_CATALOG = K.CONSTRAINT_CATALOG AND C.CONSTRAINT_SCHEMA = K.CONSTRAINT_SCHEMA AND C.CONSTRAINT_NAME = K.CONSTRAINT_NAME
                        WHERE C.CONSTRAINT_TYPE = 'PRIMARY KEY'
                        AND C.`CONSTRAINT_SCHEMA` ='{ConnectionInfo.Database}'";

            if (tableNames != null && tableNames.Count() > 0)
            {
                string strTableNames = StringHelper.GetSingleQuotedString(tableNames);
                sql += $" AND C.TABLE_NAME IN ({ strTableNames })";
            }

            return base.GetTablePrimaryKeys(dbConnector, sql);
        }
        #endregion

        #region Table Foreign Key
        public override List<TableForeignKey> GetTableForeignKeys(params string[] tableNames)
        {
            DbConnector dbConnector = this.GetDbConnector();

            string sql = $@"SELECT C.`CONSTRAINT_SCHEMA` AS `Owner`, K.TABLE_NAME AS TableName, K.CONSTRAINT_NAME AS KeyName, K.COLUMN_NAME AS ColumnName, K.`REFERENCED_TABLE_NAME` AS ReferencedTableName,K.`REFERENCED_COLUMN_NAME` AS ReferencedColumnName,
                        CASE RC.UPDATE_RULE WHEN 'CASCADE' THEN 1 ELSE 0 END AS `UpdateCascade`, 
                        CASE RC.`DELETE_RULE` WHEN 'CASCADE' THEN 1 ELSE 0 END AS `DeleteCascade`
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS C
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS K ON C.TABLE_NAME = K.TABLE_NAME AND C.CONSTRAINT_CATALOG = K.CONSTRAINT_CATALOG AND C.CONSTRAINT_SCHEMA = K.CONSTRAINT_SCHEMA AND C.CONSTRAINT_NAME = K.CONSTRAINT_NAME
                        JOIN INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS RC ON RC.CONSTRAINT_SCHEMA=C.CONSTRAINT_SCHEMA AND RC.CONSTRAINT_NAME=C.CONSTRAINT_NAME AND C.TABLE_NAME=RC.TABLE_NAME                        
                        WHERE C.CONSTRAINT_TYPE = 'FOREIGN KEY'
                        AND C.`CONSTRAINT_SCHEMA` ='{ConnectionInfo.Database}'";

            if (tableNames != null && tableNames.Count() > 0)
            {
                string strTableNames = StringHelper.GetSingleQuotedString(tableNames);
                sql += $" AND C.TABLE_NAME IN ({ strTableNames })";
            }

            return base.GetTableForeignKeys(dbConnector, sql);
        }
        #endregion

        #region Table Index
        public override List<TableIndex> GetTableIndexes(params string[] tableNames)
        {
            DbConnector dbConnector = this.GetDbConnector();

            string sql = $@"SELECT  C.`CONSTRAINT_SCHEMA` AS `Owner`, K.TABLE_NAME AS TableName, K.CONSTRAINT_NAME AS IndexName, K.COLUMN_NAME AS ColumnName,
                        CASE C.`CONSTRAINT_TYPE` WHEN 'UNIQUE' THEN 1 ELSE 0 END AS `IsUnique`, K.`ORDINAL_POSITION` AS `Order`,0 AS `IsDesc`
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS C
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS K ON C.TABLE_NAME = K.TABLE_NAME AND C.CONSTRAINT_CATALOG = K.CONSTRAINT_CATALOG AND C.CONSTRAINT_SCHEMA = K.CONSTRAINT_SCHEMA AND C.CONSTRAINT_NAME = K.CONSTRAINT_NAME
                        WHERE C.`CONSTRAINT_TYPE` NOT IN('PRIMARY KEY', 'FOREIGN KEY') 
                        AND C.`CONSTRAINT_SCHEMA` ='{ConnectionInfo.Database}'";

            if (tableNames != null && tableNames.Count() > 0)
            {
                string strTableNames = StringHelper.GetSingleQuotedString(tableNames);
                sql += $" AND C.TABLE_NAME IN ({ strTableNames })";
            }

            return base.GetTableIndexes(dbConnector, sql);
        }
        #endregion

        #region Generate Schema Scripts 

        public override string GenerateSchemaScripts(SchemaInfo schemaInfo)
        {            
            StringBuilder sb = new StringBuilder();

            #region Create Table
            foreach (Table table in schemaInfo.Tables)
            {
                string tableName = table.Name;
                string quotedTableName = this.GetQuotedTableName(table);               

                IEnumerable<TableColumn> tableColumns = schemaInfo.Columns.Where(item => item.TableName == tableName).OrderBy(item => item.Order);

                string primaryKey = "";

                IEnumerable<TablePrimaryKey> primaryKeys = schemaInfo.TablePrimaryKeys.Where(item => item.TableName == tableName);

                #region Primary Key
                if (Option.GenerateKey && primaryKeys.Count() > 0)
                {
                    //string primaryKeyName = primaryKeys.First().KeyName;
                    //if(primaryKeyName=="PRIMARY")
                    //{
                    //    primaryKeyName = "PK_" + tableName ;
                    //}
                    primaryKey =
$@"
,PRIMARY KEY
(
{string.Join(Environment.NewLine, primaryKeys.Select(item => $"{GetQuotedString(item.ColumnName)},")).TrimEnd(',')}
)";
                }
                #endregion

                List<string> foreignKeysLines = new List<string>();
                #region Foreign Key
                if (Option.GenerateKey)
                {
                    IEnumerable<TableForeignKey> foreignKeys = schemaInfo.TableForeignKeys.Where(item => item.TableName == tableName);
                    if (foreignKeys.Count() > 0)
                    {
                        ILookup<string, TableForeignKey> foreignKeyLookup = foreignKeys.ToLookup(item => item.KeyName);

                        IEnumerable<string> keyNames = foreignKeyLookup.Select(item => item.Key);

                        foreach (string keyName in keyNames)
                        {
                            TableForeignKey tableForeignKey = foreignKeyLookup[keyName].First();

                            string columnNames = string.Join(",", foreignKeyLookup[keyName].Select(item => GetQuotedString(item.ColumnName)));
                            string referenceColumnName = string.Join(",", foreignKeyLookup[keyName].Select(item => $"{GetQuotedString(item.ReferencedColumnName)}"));

                            string line = $"CONSTRAINT {GetQuotedString(keyName)} FOREIGN KEY ({columnNames}) REFERENCES {GetQuotedString(tableForeignKey.ReferencedTableName)}({referenceColumnName})";
                            
                            if (tableForeignKey.UpdateCascade)
                            {
                                line += " ON UPDATE CASCADE";
                            }
                            else
                            {
                                line += " ON UPDATE NO ACTION";
                            }

                            if (tableForeignKey.DeleteCascade)
                            {
                                line += " ON DELETE CASCADE";
                            }
                            else
                            {
                                line += " ON DELETE NO ACTION";
                            }

                            foreignKeysLines.Add(line);
                        }
                    }
                }
                #endregion

                #region Create Table
                sb.Append(
$@"
CREATE TABLE {quotedTableName}(
{string.Join(","+ Environment.NewLine, tableColumns.Select(item => this.TranslateColumn(table, item)))}{primaryKey}
{(foreignKeysLines.Count > 0 ? (","+string.Join(","+ Environment.NewLine, foreignKeysLines)):"")}
){(!string.IsNullOrEmpty(table.Comment)? ($"comment='{ValueHelper.TransferSingleQuotation(table.Comment)}'"):"")}
DEFAULT CHARSET={DbCharset};"); 
                #endregion              

                sb.AppendLine();             

                #region Index
                if (Option.GenerateIndex)
                {
                    IEnumerable<TableIndex> indices = schemaInfo.TableIndices.Where(item => item.TableName == tableName).OrderBy(item => item.Order);
                    if (indices.Count() > 0)
                    {
                        sb.AppendLine();

                        List<string> indexColumns = new List<string>();


                        ILookup<string, TableIndex> indexLookup = indices.ToLookup(item => item.IndexName);
                        IEnumerable<string> indexNames = indexLookup.Select(item => item.Key);
                        foreach (string indexName in indexNames)
                        {
                            TableIndex tableIndex = indexLookup[indexName].First();

                            string columnNames = string.Join(",", indexLookup[indexName].Select(item => $"{GetQuotedString(item.ColumnName)}"));

                            if (indexColumns.Contains(columnNames))
                            {
                                continue;
                            }
                            sb.AppendLine($"ALTER TABLE {quotedTableName} ADD {(tableIndex.IsUnique ? "UNIQUE" : "")} INDEX {tableIndex.IndexName} ({columnNames});");
                            if (!indexColumns.Contains(columnNames))
                            {
                                indexColumns.Add(columnNames);
                            }
                        }
                    }
                }
                #endregion

                //#region Default Value
                //if (options.GenerateDefaultValue)
                //{
                //    IEnumerable<TableColumn> defaultValueColumns = columns.Where(item => item.Owner== table.Owner && item.TableName == tableName && !string.IsNullOrEmpty(item.DefaultValue));
                //    foreach (TableColumn column in defaultValueColumns)
                //    {
                //        sb.AppendLine($"ALTER TABLE {quotedTableName} ALTER COLUMN {GetQuotedString(column.ColumnName)} SET DEFAULT {column.DefaultValue};");
                //    }
                //}
                //#endregion
            }
            #endregion

            return sb.ToString();
        }

        public override string TranslateColumn(Table table, TableColumn column)
        {
            string dataType = column.DataType;
            bool isChar = DataTypeHelper.IsCharType(dataType.ToLower());
          
            if (column.DataType.IndexOf("(") < 0)
            {
                if (isChar)
                {
                    dataType = $"{dataType}({column.MaxLength.ToString()})";
                }
                else if (!this.IsNoLengthDataType(dataType))
                {
                    long precision = column.Precision.HasValue ? column.Precision.Value : column.MaxLength.Value;
                    int scale = column.Scale.HasValue ? column.Scale.Value : 0;

                    dataType = $"{dataType}({precision},{scale})";
                }

                if(isChar || DataTypeHelper.IsTextType(dataType.ToLower()))
                {
                    dataType += $" CHARACTER SET {DbCharset} COLLATE {DbCharsetCollation} ";
                }
            }

            return $@"{GetQuotedString(column.ColumnName)} {dataType} {(column.IsRequired ? "NOT NULL" : "NULL")} {( this.Option.GenerateIdentity && column.IsIdentity ? $"AUTO_INCREMENT" : "")} {(string.IsNullOrEmpty(column.DefaultValue) ? "" : " DEFAULT " + column.DefaultValue)} {(!string.IsNullOrEmpty(column.Comment)? $"comment '{ValueHelper.TransferSingleQuotation(column.Comment)}'":"")}";
        }

        private bool IsNoLengthDataType(string dataType)
        {
            string[] flags = { "date", "time", "int", "text", "longblob", "longtext" };

            return flags.Any(item => dataType.ToLower().Contains(item));
        }

        #endregion

        #region Generate Data Script
        public override long GetTableRecordCount(DbConnection connection, Table table)
        {
            string sql = $"SELECT COUNT(1) FROM {this.GetQuotedTableName(table)}";

            return base.GetTableRecordCount(connection, sql);
        }      
        public override string GenerateDataScripts(SchemaInfo schemaInfo)
        {
            return base.GenerateDataScripts(schemaInfo);
        }

        protected override string GetPagedSql(string tableName, string columnNames, string primaryKeyColumns, string whereClause, long pageNumber, int pageSize)
        {
            var startEndRowNumber = PaginationHelper.GetStartEndRowNumber(pageNumber, pageSize);         

            var pagedSql = $@"SELECT {columnNames}
							  FROM {tableName}
                             {whereClause} 
                             ORDER BY {(!string.IsNullOrEmpty(primaryKeyColumns)? primaryKeyColumns:"1")}
                             LIMIT { startEndRowNumber.StartRowNumber -1 } , {pageSize}";

            return pagedSql;
        }
        #endregion       
    }
}