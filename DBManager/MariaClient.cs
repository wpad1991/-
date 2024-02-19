using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace DBManager
{
    public class MariaDBFactory
    {
        private string url;
        private string port;
        private string user;
        private string password;
        private string database;

        public MariaDBFactory(string url, string port, string user, string password, string database)
        {
            this.url = url;
            this.port = port;
            this.user = user;
            this.password = password;
            this.database = database;
        }
        public MySqlConnection Connect()
        {
            string connectionString = "server=" + url + ";port=" + port + ";database=" + database + ";user=" + user + ";password=" + password;
            MySqlConnection connection = null;

            try
            {
                connection = new MySqlConnection(connectionString);
                connection.Open();
            }
            catch (Exception excep)
            {
                StockLog.Logger.LOG.WriteLog("Exception", excep.ToString());
                return null;
            }
            return connection;
        }

        public DataTable ExecuteDataTable(MySqlConnection conn, string query, int timeout = 0)
        {
            //StockLog.Logger.LOG.WriteLog("Query", query);
            MySqlCommand cmd = conn.CreateCommand();
            
            cmd.CommandTimeout = timeout;
            cmd.CommandText = query;

            MySqlDataReader reader = cmd.ExecuteReader();
            DataTable dt = new DataTable();

            dt.Load(reader);
            reader.Close();
            return dt;
        }

        public MySqlDataReader ExecuteReader(MySqlConnection conn, string query)
        {
            //StockLog.Logger.LOG.WriteLog("Query", query);
            MySqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = query;

            return cmd.ExecuteReader();
        }

        public void Execute(MySqlConnection conn, string query, int timeout = 0)
        {
            //StockLog.Logger.LOG.WriteLog("Query", query);
            MySqlCommand cmd = conn.CreateCommand();

            cmd.CommandTimeout = timeout;
            cmd.CommandText = query;

            cmd.ExecuteReader().Close();
        }

        public List<string> ExecuteGetList(MySqlConnection conn, string query)
        {
            //StockLog.Logger.LOG.WriteLog("Query", query);
            MySqlConnector.MySqlDataReader reader = ExecuteReader(conn, query);

            int cnt = reader.FieldCount;
            if (cnt == 0)
            {
                return null;
            }

            string line = "";
            List<string> dataList = new List<string>();

            while (reader.Read())
            {
                line = "";
                for (int i = 0; i < cnt; i++)
                {
                    line += reader.GetValue(i) + ",";
                }
                dataList.Add(line.TrimEnd(','));
            }
            reader.Close();

            return dataList;
        }
    }

    public class MariaClient
    {
        private string url;
        private string port;
        private string user;
        private string password;
        private string database;
        private MySqlConnection connection;

        public string Url { get => url; }
        public string Port { get => port; }
        public string User { get => user; }
        public string Password { get => password; }
        public string Database { get => database; }

        public MariaClient(string url, string port, string user, string password, string database) {
            this.url = url;
            this.port = port;
            this.user = user;
            this.password = password;
            this.database = database;
        }

        public bool Connect() {
            string connectionString = "server=" + url + ";port=" + port + ";database=" + database + ";user=" + user + ";password=" + password;
            connection = new MySqlConnection(connectionString);

            try
            {
                connection.Open();
            }
            catch (Exception exception)
            {
                StockLog.Logger.LOG.WriteLog("Exception", exception + "");
                return false;
            }
            return true;
        }

        public void Close()
        {
            connection.Close();
        }

        public void UseDatabase(string dbName) {

            MySqlCommand cmd = connection.CreateCommand();
            string sql = "use " + dbName;

            cmd.ExecuteNonQuery();
        }

        public DataTable Execute(string query)
        {
            StockLog.Logger.LOG.WriteLog("Query", query);
            MySqlCommand cmd = connection.CreateCommand();
            cmd.CommandText = query;
            cmd.CommandTimeout = 60;

            MySqlDataReader reader = cmd.ExecuteReader();
            DataTable dt = new DataTable();

            dt.Load(reader);
            reader.Close();
            return dt;
        }

        public void InsertRealTimeData() {
            StringBuilder sb = new StringBuilder();

            sb.Append("insert into tb_stock_price_rt (ITM_C, WK_DT, WK_TM, RT_AM, BF_PRE_AM, RT_RATE, SEL_AM, BUY_AM, AC_VOL, AC_TR_AM, NOW_AM, HIGH_AM, LOW_AM, BF_ORE_AM, BF_PRE_VOL, BF_PRE_RM_AM, CHG_AM, FEE_AM, ITM_VOL, UP_LIM_TM, DOWN_LIM_TM) values ");
            sb.Append("(");
            sb.Append(")");
            sb.Append(", ");


        }
    }

}
