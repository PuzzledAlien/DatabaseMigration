﻿using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;

namespace DatabaseMigration.Core
{
    public class ColumnTranslator
    {
        public static List<TableColumn> TranslateColumn(List<TableColumn> columns, DatabaseType sourceDbType, DatabaseType targetDbType)
        {
            if (sourceDbType == targetDbType)
            {
                return columns;
            }

            string configRootFolder = Path.Combine(PathHelper.GetAssemblyFolder(), "Configs");
            string dataTypeMappingFilePath = Path.Combine(configRootFolder, $"DataTypeMapping/{sourceDbType.ToString()}2{targetDbType.ToString()}.xml");
            string functionMappingFilePath = Path.Combine(configRootFolder, "FunctionMapping.xml");

            #region DataType Mapping
            XDocument dataTypeMappingDoc = XDocument.Load(dataTypeMappingFilePath);

            List<DataTypeMapping> dataTypeMappings = dataTypeMappingDoc.Root.Elements("mapping").Select(item =>
             new DataTypeMapping()
             {
                 Source = new DataTypeMappingSource(item),
                 Tareget = new DataTypeMappingTarget(item),
                 Specials = item.Elements("special")?.Select(t => new DataTypeMappingSpecial(t)).ToList()
             })
             .ToList();
            #endregion

            #region Function Mapping
            XDocument functionMappingDoc = XDocument.Load(functionMappingFilePath);
            List<IEnumerable<FunctionMapping>> functionMappings = functionMappingDoc.Root.Elements("mapping").Select(item =>
            item.Elements().Select(t => new FunctionMapping() { DbType = t.Name.ToString(), Function = t.Value }))
            .ToList();
            #endregion

            foreach (TableColumn column in columns)
            {
                string sourceDataType = GetTrimedDataType(column);
                column.DataType = sourceDataType;
                DataTypeMapping dataTypeMapping = dataTypeMappings.FirstOrDefault(item => item.Source.Type?.ToLower() == column.DataType?.ToLower());
                if (dataTypeMapping != null)
                {
                    column.DataType = dataTypeMapping.Tareget.Type;

                    bool isChar = DataTypeHelper.IsCharType(column.DataType);

                    if (isChar)
                    {
                        if (!string.IsNullOrEmpty(dataTypeMapping.Tareget.Length))
                        {
                            column.MaxLength = int.Parse(dataTypeMapping.Tareget.Length);
                        }

                        bool hasSpecial = false;
                        if (dataTypeMapping.Specials != null && dataTypeMapping.Specials.Count > 0)
                        {
                            DataTypeMappingSpecial special = dataTypeMapping.Specials.FirstOrDefault(item => item.SourceMaxLength == column.MaxLength.ToString());
                            if (special != null)
                            {
                                column.DataType = special.Type;
                                hasSpecial = true;

                                if (!string.IsNullOrEmpty(special.TargetMaxLength))
                                {
                                    column.MaxLength = int.Parse(special.TargetMaxLength);
                                }
                            }
                        }

                        if (!hasSpecial && column.DataType.ToLower().StartsWith("n")) //nchar,nvarchar
                        {
                            if (column.MaxLength > 0 && !sourceDataType.ToLower().StartsWith("n"))
                            {
                                column.MaxLength = column.MaxLength / 2;
                            }
                        }


                    }
                    else
                    {
                        if (dataTypeMapping.Specials != null && dataTypeMapping.Specials.Count > 0)
                        {
                            DataTypeMappingSpecial special = dataTypeMapping.Specials.FirstOrDefault(item => item.SourceMaxLength == column.MaxLength.ToString());
                            if (special != null)
                            {
                                column.DataType = special.Type;
                            }
                            else
                            {
                                special = dataTypeMapping.Specials.FirstOrDefault(item => item.SourcePrecision == column.Precision?.ToString() && item.SourceScale == column.Scale?.ToString());
                                if (special != null)
                                {
                                    column.DataType = special.Type;
                                }
                            }

                            if (special != null && !string.IsNullOrEmpty(special.TargetMaxLength))
                            {
                                column.MaxLength = int.Parse(special.TargetMaxLength);
                            }
                        }

                        if (!string.IsNullOrEmpty(dataTypeMapping.Tareget.Precision))
                        {
                            column.Precision = int.Parse(dataTypeMapping.Tareget.Precision);
                        }

                        if (!string.IsNullOrEmpty(dataTypeMapping.Tareget.Scale))
                        {
                            column.Scale = int.Parse(dataTypeMapping.Tareget.Scale);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(column.DefaultValue))
                {
                    string defaultValue = sourceDbType == DatabaseType.SqlServer ? GetTrimedDefaultValue(column.DefaultValue) : GetQuotedDefaultValue(column.DefaultValue);
                    IEnumerable<FunctionMapping> funcMappings = functionMappings.FirstOrDefault(item => item.Any(t => t.DbType == sourceDbType.ToString() && t.Function == defaultValue));
                    if (funcMappings != null)
                    {
                        defaultValue = funcMappings.FirstOrDefault(item => item.DbType == targetDbType.ToString())?.Function;
                    }
                    column.DefaultValue = defaultValue;
                }
            }

            return columns;
        }

        private static string GetTrimedDataType(TableColumn column)
        {
            string dataType = column.DataType;
            int index = dataType.IndexOf("(");
            if (index > 0)
            {
                return dataType.Substring(0, index);
            }
            return dataType;
        }

        private static string GetTrimedDefaultValue(string defaultValue)
        {
            if (!string.IsNullOrEmpty(defaultValue))
            {
                defaultValue = defaultValue.TrimStart('(').TrimEnd(')');
                if (defaultValue.EndsWith("("))
                {
                    defaultValue += ")";
                }
                return defaultValue;
            }
            return defaultValue;
        }

        private static string GetQuotedDefaultValue(string defaultValue)
        {
            if (!string.IsNullOrEmpty(defaultValue))
            {
                if (!(defaultValue.StartsWith("(") && defaultValue.EndsWith(")")))
                {
                    return "(" + defaultValue + ")";
                }
            }
            return defaultValue;
        }
    }
}
