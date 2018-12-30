﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace DatabaseMigration.Core
{
    public delegate void FeedbackHandle(FeedbackInfo info);

    public class DbConvertor: IObserver<FeedbackInfo>
    {       
        public DbConvetorInfo Source { get; set; }
        public DbConvetorInfo Target { get; set; }

        public DbConvertorOption Option { get; set; } = new DbConvertorOption();
       
        public event FeedbackHandle OnFeedback;


        public DbConvertor(DbConvetorInfo source, DbConvetorInfo target)
        {
            this.Source = source;
            this.Target = target;
        }

        public DbConvertor(DbConvetorInfo source, DbConvetorInfo target, DbConvertorOption option)
        {
            this.Source = source;
            this.Target = target;
            if(option!=null)
            {
                this.Option = option;
            }           
        }

        public void Convert(params string[] tableNames)
        {
            DbInterpreter sourceInterpreter = this.Source.DbInterpreter;

            sourceInterpreter.Option.TreatBytesAsNullForScript = true;

            tableNames = sourceInterpreter.GetTables(tableNames).Select(item => item.Name).ToArray();

            SchemaInfo sourceSchemaInfo = sourceInterpreter.GetSchemaInfo(tableNames);
            SchemaInfo targetSchemaInfo = SchemaInfoHelper.Clone(sourceSchemaInfo);

            if(!string.IsNullOrEmpty(this.Target.DbOwner))
            {
                SchemaInfoHelper.TransformOwner(targetSchemaInfo, this.Target.DbOwner);
            }            

            targetSchemaInfo.Columns = ColumnTranslator.TranslateColumn(targetSchemaInfo.Columns, this.Source.DbInterpreter.DatabaseType, this.Target.DbInterpreter.DatabaseType);

            if(this.Option.EnsurePrimaryKeyNameUnique)
            {
                SchemaInfoHelper.EnsurePrimaryKeyNameUnique(targetSchemaInfo);
            }
            
            if(this.Option.EnsureIndexNameUnique)
            {
                SchemaInfoHelper.EnsureIndexNameUnique(targetSchemaInfo);
            }         

            DbInterpreter targetInterpreter = this.Target.DbInterpreter;
            string script = "";

            sourceInterpreter.Subscribe(this);
            targetInterpreter.Subscribe(this);

            if(this.Option.GenerateScriptMode.HasFlag(GenerateScriptMode.Schema))
            {
                script= targetInterpreter.GenerateSchemaScripts(targetSchemaInfo);

                if (string.IsNullOrEmpty(script))
                {
                    throw new Exception($"The script to create schema is null.");
                }

                targetInterpreter.Feedback(FeedbackInfoType.Info, "Begin to sync schema...");
                if (!this.Option.SplitScriptsToExecute)
                {
                    targetInterpreter.ExecuteNonQuery(script);
                }
                else
                {
                    string[] sqls = script.Split(new char[] { this.Option.ScriptSplitChar }, StringSplitOptions.RemoveEmptyEntries);
                    int count = sqls.Count();

                    int i = 0;
                    foreach (string sql in sqls)
                    {
                        if (!string.IsNullOrEmpty(sql.Trim()))
                        {
                            i++;
                            targetInterpreter.Feedback(FeedbackInfoType.Info, $"({i}/{count}), executing {sql}");
                            targetInterpreter.ExecuteNonQuery(sql.Trim());
                        }                
                    }
                }
                targetInterpreter.Feedback(FeedbackInfoType.Info, "End sync schema.");
            }           

            if(this.Option.GenerateScriptMode.HasFlag(GenerateScriptMode.Data))
            {
                if (this.Option.PickupTable != null)
                {
                    sourceSchemaInfo.PickupTable = this.Option.PickupTable;
                }

                sourceInterpreter.AppendScriptsToFile("", GenerateScriptMode.Data, true);
                targetInterpreter.AppendScriptsToFile("", GenerateScriptMode.Data, true);

                using (DbConnection dbConnection = targetInterpreter.GetDbConnector().CreateConnection())
                {
                    sourceInterpreter.OnDataRead += (table, columns, data) =>
                    {
                        try
                        {
                            StringBuilder sb = new StringBuilder();

                            (Table Table, List<TableColumn> Columns) targetTableAndColumns = this.GetTargetTableColumns(targetSchemaInfo, this.Target.DbOwner, table, columns);

                            Dictionary<string, object> paramters = targetInterpreter.AppendDataScripts(this.Target.DbInterpreter.Option, sb, targetTableAndColumns.Table, targetTableAndColumns.Columns, new Dictionary<long, List<Dictionary<string, object>>>() { { 1, data } });

                            script = sb.ToString();

                            if (!this.Option.SplitScriptsToExecute)
                            {
                                targetInterpreter.ExecuteNonQuery(dbConnection, script, paramters, false);
                            }
                            else
                            {
                                string[] sqls = script.Split(new char[] { this.Option.ScriptSplitChar }, StringSplitOptions.RemoveEmptyEntries);

                                foreach (string sql in sqls)
                                {
                                    if (!string.IsNullOrEmpty(sql.Trim()))
                                    {
                                        targetInterpreter.ExecuteNonQuery(dbConnection, sql, paramters, false);
                                    }
                                }
                            }

                            targetInterpreter.FeedbackInfo($"End write data to table {table.Name}, handled rows count:{data.Count}.");
                        }
                        catch (Exception ex)
                        {
                            ConnectionInfo sourceConnectionInfo = sourceInterpreter.ConnectionInfo;
                            ConnectionInfo targetConnectionInfo = targetInterpreter.ConnectionInfo;

                            throw new TableDataTransferException(ex)
                            {
                                SourceServer = sourceConnectionInfo.Server,
                                SourceDatabase = sourceConnectionInfo.Database,
                                SourceTableName = table.Name
                            ,
                                TargetServer = targetConnectionInfo.Server,
                                TargetDatabase = targetConnectionInfo.Database,
                                TargetTableName = table.Name
                            };
                        }
                    };

                    sourceInterpreter.GenerateDataScripts(sourceSchemaInfo);
                }
            }           
        }

        private (Table Table, List<TableColumn> Columns) GetTargetTableColumns(SchemaInfo targetSchemaInfo, string targetOwner, Table sourceTable, List<TableColumn> sourceColumns)
        {
            Table targetTable = targetSchemaInfo.Tables.FirstOrDefault(item => (item.Owner == targetOwner || string.IsNullOrEmpty(targetOwner)) && item.Name == sourceTable.Name);
            if(targetTable==null)
            {
                throw new Exception($"Source table {sourceTable.Name} cannot get a target table.");
            }

            List<TableColumn> targetTableColumns = new List<TableColumn>();
            foreach(TableColumn sourceColumn in sourceColumns)
            {
                TableColumn targetTableColumn = targetSchemaInfo.Columns.FirstOrDefault(item => (item.Owner == targetOwner || string.IsNullOrEmpty(targetOwner)) && item.TableName == sourceColumn.TableName && item.ColumnName == sourceColumn.ColumnName);
                if(targetTableColumn==null)
                {
                    throw new Exception($"Source column {sourceColumn.TableName} of table {sourceColumn.TableName} cannot get a target column.");
                }
                targetTableColumns.Add(targetTableColumn);
            }
            return (targetTable, targetTableColumns);           
        }       

        #region IObserver<FeedbackInfo>
        void IObserver<FeedbackInfo>.OnCompleted()
        {
            
        }

        void IObserver<FeedbackInfo>.OnError(Exception error)
        {
           
        }

        void IObserver<FeedbackInfo>.OnNext(FeedbackInfo info)
        {
            if(this.OnFeedback!=null)
            {
                this.OnFeedback(info);
            }
        }
        #endregion
    }
}
