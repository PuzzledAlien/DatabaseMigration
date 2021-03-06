﻿using System.Collections.Generic;
using System.Linq;

namespace DatabaseMigration.Core
{
    public class TableReferenceHelper
    {
        public static List<string> ResortTableNames(string[] tableNames, List<TableForeignKey> tableForeignKeys)
        {
            List<string> sortedTableNames = new List<string>();
            IEnumerable<string> primaryTableNames = tableForeignKeys.Select(item => item.ReferencedTableName);
            IEnumerable<string> foreignTableNames = tableForeignKeys.Select(item => item.TableName);

            IEnumerable<string> noReferenceTableNames = tableNames.Where(item => !primaryTableNames.Contains(item) && !foreignTableNames.Contains(item)).OrderBy(item => item);
            sortedTableNames.AddRange(noReferenceTableNames);

            IEnumerable<string> topReferencedTableNames = tableForeignKeys.Where(item => (!foreignTableNames.Contains(item.ReferencedTableName))
                 || (item.TableName == item.ReferencedTableName && tableForeignKeys.Any(t => t.KeyName != item.KeyName && item.TableName == t.ReferencedTableName)
                 && !tableForeignKeys.Any(t => t.KeyName != item.KeyName && item.ReferencedTableName == t.TableName)))
                .Select(item => item.ReferencedTableName).Distinct().OrderBy(item => item);

            sortedTableNames.AddRange(topReferencedTableNames);
            List<string> childTableNames = new List<string>();
            foreach (string tableName in topReferencedTableNames)
            {
                childTableNames.AddRange(GetForeignTables(tableName, tableForeignKeys, sortedTableNames.Concat(childTableNames)));
            }

            List<string> sortedChildTableNames = GetSortedTableNames(childTableNames, tableForeignKeys);

            sortedChildTableNames = sortedChildTableNames.Distinct().ToList();

            sortedTableNames.AddRange(sortedChildTableNames);

            IEnumerable<string> selfReferencedTableNames = tableForeignKeys.Where(item => item.TableName == item.ReferencedTableName).Select(item => item.TableName).OrderBy(item => item);
            sortedTableNames.AddRange(selfReferencedTableNames.Where(item => !sortedTableNames.Contains(item)));

            return sortedTableNames;
        }

        private static List<string> GetSortedTableNames(List<string>tableNames, List<TableForeignKey> tableForeignKeys)
        {
            List<string> sortedTableNames = new List<string>();

            for (int i = 0; i <= tableNames.Count - 1; i++)
            {
                string tableName = tableNames[i];
                IEnumerable<TableForeignKey> foreignKeys = tableForeignKeys.Where(item => item.TableName == tableName && item.TableName != item.ReferencedTableName);
                               
                if (foreignKeys.Any())
                {
                    foreach (TableForeignKey foreignKey in foreignKeys)
                    {
                        int referencedTableIndex = tableNames.IndexOf(foreignKey.ReferencedTableName);
                        if (referencedTableIndex >= 0 && referencedTableIndex > i)
                        {
                            sortedTableNames.Add(foreignKey.ReferencedTableName);                          
                        }
                    }
                }

                sortedTableNames.Add(tableName);
            }

            sortedTableNames = sortedTableNames.Distinct().ToList();

            bool needSort = false;
            for (int i = 0; i <= sortedTableNames.Count - 1; i++)
            {
                string tableName = sortedTableNames[i];
                IEnumerable<TableForeignKey> foreignKeys = tableForeignKeys.Where(item => item.TableName == tableName && item.TableName != item.ReferencedTableName);
                               
                if (foreignKeys.Any())
                {
                    foreach (TableForeignKey foreignKey in foreignKeys)
                    {
                        int referencedTableIndex = sortedTableNames.IndexOf(foreignKey.ReferencedTableName);
                        if (referencedTableIndex >= 0 && referencedTableIndex > i)
                        {
                            needSort = true;
                            break;
                        }
                    }
                }

                if (needSort)
                {
                    return GetSortedTableNames(sortedTableNames, tableForeignKeys);
                }
            }

            return sortedTableNames;
        }

        private static List<string> GetForeignTables(string tableName, List<TableForeignKey> tableForeignKeys, IEnumerable<string> sortedTableNames)
        {
            List<string> tableNames = new List<string>();

            IEnumerable<string> foreignTableNames = tableForeignKeys.Where(item => item.ReferencedTableName == tableName && item.TableName != tableName && !sortedTableNames.Contains(item.TableName)).Select(item => item.TableName);

            tableNames.AddRange(foreignTableNames);

            IEnumerable<string> childForeignTableNames = tableForeignKeys.Where(item => foreignTableNames.Contains(item.ReferencedTableName)).Select(item => item.TableName);
            if (childForeignTableNames.Count() > 0)
            {
                List<string> childNames = foreignTableNames.SelectMany(item => GetForeignTables(item, tableForeignKeys, sortedTableNames)).ToList();
                tableNames.AddRange(childNames.Where(item => !tableNames.Contains(item)));
            }

            return tableNames;
        }

        public static bool IsSelfReference(string tableName, List<TableForeignKey> tableForeignKeys)
        {
            return tableForeignKeys.Any(item => item.TableName == tableName && item.TableName == item.ReferencedTableName);
        }
    }
}
