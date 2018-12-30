﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseMigration.Core
{
    public class TableColumn
    {
        public string Owner { get; set; }
        public string TableName { get; set; }
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public bool IsRequired { get; set; }
        public bool IsIdentity { get; set; }
        public long? MaxLength { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public int Order { get; set; }
        public string DefaultValue { get; set; }
        public string Comment { get; set; }
    }
}