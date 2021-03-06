﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseMigration.Core
{
    public class MySqlConnectionBuilder : IConnectionBuilder
    {
        public string BuildConntionString(ConnectionInfo connectionInfo)
        {
            StringBuilder sb = new StringBuilder($"server={connectionInfo.Server};database={connectionInfo.Database};");

            if(connectionInfo.IntegratedSecurity)
            {
                sb.Append($"Integrated Security=True;");
            }
            else
            {
                sb.Append($"user id={connectionInfo.UserId};password={connectionInfo.Password};SslMode=none;");
            }

            return sb.ToString();
        }
    }
}
