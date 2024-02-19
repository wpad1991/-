using Npgsql;
using System;
using System.Collections.Generic;
using System.Text;

namespace DBManager
{
    public class PostgresClient
    {
        private string url;
        private string user;
        private string password;
        private string database;


        public string Url { get => url; }
        public string User { get => user; }
        public string Password { get => password; }
        public string Database { get => database; }

        public PostgresClient(string url, string user, string password, string database)
        {
            this.url = url;
            this.user = user;
            this.password = password;
            this.database = database;
        }
    }
}
