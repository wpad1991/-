using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using static StockData.DataStruct;

namespace StockData
{
    public class DataStore
    {
        public List<string> StockHoldList = new List<string>();
        public Privileges Account_Privileges = new Privileges();
        public StockTimer.Timer StockTimer = new StockTimer.Timer("Timer1");
        public StockTimer.Timer StockTimer_Tight = new StockTimer.Timer("Timer2");
        public StockTimer.Timer StockTimer_Tick = new StockTimer.Timer("Timer_Tick");
        public StockTimer.Timer StockTimer_AskBid = new StockTimer.Timer("Timer_AskBid");
        public StockTimer.Timer StockTimer_System = new StockTimer.Timer("Timer_System");

        public Dictionary<string, AskBidPrice> AskBidData = new Dictionary<string, AskBidPrice>();
        Dictionary<string, RealTimeData_v2> stockRealTimeData = new Dictionary<string, RealTimeData_v2>();

        List<string> kospiList = new List<string>();
        List<string> kosdaqList = new List<string>();
        List<string> etfList = new List<string>();
        List<string> holdStockCode = new List<string>();
        public string ExternalIPAddress = "";
        public DBAccessInfo AccessInfo = new DBAccessInfo();
        public WorkInformation WorkInfo = new WorkInformation();
        AccountData accountData = new AccountData();
        //List<TimerInformation> timerInfo = new List<TimerInformation>();
        int screenNumber = 0;
        object obj = new object();
        public string ConfigPath = "";
        char[] pmfilter = new char[] { '+', '-' };

        public void QueryLogSend(string group, string message)
        {

            DBManager.MariaClient mdb = new DBManager.MariaClient(StockData.Singleton.Store.AccessInfo.Url, StockData.Singleton.Store.AccessInfo.Port, StockData.Singleton.Store.AccessInfo.User, StockData.Singleton.Store.AccessInfo.Password, StockData.Singleton.Store.AccessInfo.DBName);

            try
            {
                bool dbSuccess = mdb.Connect();

                if (!dbSuccess)
                {
                    StockLog.Logger.LOG.WriteLog("Error", "DB Connection Fail: [QuerySend]");
                    return;
                }

                DBLogUnit log = new DBLogUnit();
                log.hostip = ExternalIPAddress;
                log.wk_dtm = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                log.group_name = group;
                log.message = StockData.Singleton.Store.Account.ID + ", " + message;

                string sql = MakeInsertUpdateQuery<DBLogUnit>("tb_log_system", log);

                mdb.Execute(sql);

                mdb.Close();
            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                mdb.Close();
            }
        }

        public Dictionary<string, RealTimeData_v2> StockRealTimeData
        {
            get
            {
                return stockRealTimeData;
            }
        }

        public List<string> HoldStockCode
        {
            get { return holdStockCode; }
            set { holdStockCode = value; }
        }

        public List<string> KospiList
        {
            get
            {
                return kospiList;
            }
            set
            {
                kospiList = value;
            }
        }
        public List<string> KosdaqList { get => kosdaqList; set => kosdaqList = value; }

        public AccountData Account
        {
            get { return accountData; }
            set { accountData = value; }
        }

        public List<string> ETFList { get => etfList; set => etfList = value; }

        public DataStore()
        {
            ConfigPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\Prost\\Config";
        }

        public void LoadSetting()
        {
            lock (obj)
            {
                try
                {
                    AccessInfo = (DBAccessInfo)DeserializeXML(ConfigPath + "\\accessinfo.xml", typeof(DBAccessInfo));
                    if (AccessInfo == null)
                    {
                        AccessInfo = new DBAccessInfo();
                        SerializeXML(ConfigPath, "accessinfo.xml", typeof(DBAccessInfo), AccessInfo);
                    }

                    WorkInfo = (WorkInformation)DeserializeXML(ConfigPath + "\\workinfo.xml", typeof(WorkInformation));
                    if (WorkInfo == null)
                    {
                        WorkInfo = new WorkInformation();
                        SerializeXML(ConfigPath, "workinfo.xml", typeof(WorkInformation), WorkInfo);
                    }

                    SetPythonEnvironment();

                    //AccountInfo = (AccountInformation)DeserializeXML(ConfigPath + "\\accountinfo.xml", typeof(AccountInformation));
                    //if (AccountInfo == null)
                    //{
                    //    AccountInfo = new AccountInformation();
                    //    SerializeXML(ConfigPath, "accountinfo.xml", typeof(AccountInformation), AccountInfo);
                    //}

                    //timerInfo = (List<TimerInformation>)DeserializeXML(ConfigPath + "\\timerInfo.xml", typeof(List<TimerInformation>));
                    //if (timerInfo == null)
                    //{
                    //    timerInfo = new List<TimerInformation>();
                    //    SerializeXML(ConfigPath, "workinfo.xml", typeof(List<TimerInformation>), timerInfo);
                    //}
                }
                catch (Exception except)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                }
            }
        }

        public void SaveSetting()
        {
            lock (obj)
            {
                try
                {
                    StockLog.Logger.LOG.WriteLog("Console", "SaveSetting Execute");
                    SerializeXML(ConfigPath, "accessinfo.xml", typeof(DBAccessInfo), AccessInfo);
                    SerializeXML(ConfigPath, "workinfo.xml", typeof(WorkInformation), WorkInfo);
                    //SerializeXML(ConfigPath, "accountinfo.xml", typeof(AccountInformation), AccountInfo);
                    //SerializeXML(ConfigPath, "timerInfo.xml", typeof(List<TimerInformation>), timerInfo);   
                }
                catch (Exception except)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                }
            }
        }

        private void SetPythonEnvironment()
        {
            //try
            //{
            //    string PYTHON_HOME = Environment.ExpandEnvironmentVariables(@"C:\Users\jisu kim\AppData\Local\Programs\Python\Python39");

            //    StockLog.Logger.LOG.WriteLog("Console", ">>>>: " + PYTHON_HOME);
            //}
            //catch (Exception except)
            //{
            //    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
            //}
        }

        public string GetScreenNumber()
        {
            string num;
            lock (obj)
            {
                num = (screenNumber++ % 2000).ToString().PadLeft(4, '0');
            }
            return num;

        }

        public void GetDataTable<T>(DataRow dataRow, T obj)
        {
            Type type = obj.GetType();
            FieldInfo[] fieldinfo = type.GetFields();

            foreach (FieldInfo tmp in fieldinfo)
            {
                if (tmp.FieldType == typeof(DateTime))
                {
                    tmp.SetValue(obj, dataRow[tmp.Name]);
                }
                else if (tmp.FieldType == typeof(int))
                {
                    tmp.SetValue(obj, dataRow[tmp.Name]);
                }
                else
                {
                    tmp.SetValue(obj, dataRow[tmp.Name].ToString());
                }
            }
        }

        public void GetDataTableRT(DataRow dataRow, RealTimeData data)
        {
            Type type = data.GetType();
            FieldInfo[] fieldinfo = type.GetFields();


            foreach (FieldInfo tmp in fieldinfo)
            {
                if (tmp.Name == "RT_AM") { tmp.SetValue(data, double.Parse(dataRow[tmp.Name].ToString())); }
                else if (tmp.Name == "BF_PRE_AM") { tmp.SetValue(data, double.Parse(dataRow[tmp.Name].ToString())); }
                else if (tmp.Name == "RT_RATE") { tmp.SetValue(data, double.Parse(dataRow[tmp.Name].ToString())); }
                else if (tmp.Name == "SEL_AM") { tmp.SetValue(data, double.Parse(dataRow[tmp.Name].ToString())); }
                else if (tmp.Name == "BUY_AM") { tmp.SetValue(data, double.Parse(dataRow[tmp.Name].ToString())); }
                else if (tmp.Name == "AC_VOL") { tmp.SetValue(data, int.Parse(dataRow[tmp.Name].ToString())); }
                else if (tmp.Name == "AC_TR_AM") { tmp.SetValue(data, double.Parse(dataRow[tmp.Name].ToString())); }
                else if (tmp.Name == "NOW_AM") { tmp.SetValue(data, double.Parse(dataRow[tmp.Name].ToString())); }
                else if (tmp.Name == "HIGH_AM") { tmp.SetValue(data, double.Parse(dataRow[tmp.Name].ToString())); }
                else if (tmp.Name == "LOW_AM") { tmp.SetValue(data, double.Parse(dataRow[tmp.Name].ToString())); }
                else if (tmp.Name == "BF_PRE_VOL") { tmp.SetValue(data, double.Parse(dataRow[tmp.Name].ToString())); }
                else if (tmp.Name == "BF_PRE_RT_AM") { tmp.SetValue(data, double.Parse(dataRow[tmp.Name].ToString())); }
                else if (tmp.Name == "CHG_AM") { tmp.SetValue(data, double.Parse(dataRow[tmp.Name].ToString())); }
                else if (tmp.Name == "FEE_AM") { tmp.SetValue(data, double.Parse(dataRow[tmp.Name].ToString())); }
                else if (tmp.Name == "ITM_VOL") { tmp.SetValue(data, double.Parse(dataRow[tmp.Name].ToString())); }
                else { tmp.SetValue(data, dataRow[tmp.Name].ToString()); }

            }
        }

        public void GetDataTableRT_Min(DataRow dataRow, RealTimeData_v2 data)
        {
            Type type = data.GetType();
            FieldInfo[] fieldinfo = type.GetFields();

            foreach (FieldInfo tmp in fieldinfo)
            {
                if (tmp.Name == "WK_MILI" || tmp.Name == "SEQ")
                {
                    continue;
                }
                tmp.SetValue(data, dataRow[tmp.Name].ToString());
            }
        }

        public void GetDataTableRT_v2(DataRow dataRow, RealTimeData_v2 data)
        {
            Type type = data.GetType();
            FieldInfo[] fieldinfo = type.GetFields();

            foreach (FieldInfo tmp in fieldinfo)
            {
                tmp.SetValue(data, dataRow[tmp.Name].ToString());
            }
        }

        public bool ContainNull<T>(T obj)
        {
            Type type = obj.GetType();
            FieldInfo[] fieldinfo = type.GetFields();

            foreach (FieldInfo tmp in fieldinfo)
            {
                if (tmp.GetValue(obj) + "" == "")
                {
                    return true;
                }
            }

            return false;
        }

        public string MakeInsertUpdateQuery<T>(string tableName, T obj)
        {
            return GetInsertUpdateQuery(tableName, GetMemberValue<T>(obj));
        }

        public string CreateQueryAllString<T>(string tableName, T obj)
        {
            StringBuilder sb = new StringBuilder();


            Type type = obj.GetType();
            FieldInfo[] fieldinfo = type.GetFields();

            sb.Append("create table ");
            sb.Append(tableName);
            sb.Append(" (");

            foreach (FieldInfo tmp in fieldinfo)
            {
                sb.Append(tmp.Name);
                sb.Append(" varchar(20),");
            }

            string str = sb.ToString().TrimEnd(',');

            str += ")";

            return str;
        }

        public void SetMemberValueZero<T>(T obj)
        {
            Type type = obj.GetType();
            FieldInfo[] fieldinfo = type.GetFields();

            foreach (FieldInfo tmp in fieldinfo)
            {
                if (tmp.GetValue(obj) + "" == "")
                {
                    tmp.SetValue(obj, "0");
                }
            }

        }

        public Dictionary<string, string> GetMemberValue<T>(T obj)
        {

            Dictionary<string, string> dic = new Dictionary<string, string>();

            Type type = obj.GetType();
            FieldInfo[] fieldinfo = type.GetFields();

            foreach (FieldInfo tmp in fieldinfo)
            {
                if (tmp.FieldType == typeof(DateTime))
                {
                    dic[tmp.Name] = ((DateTime)tmp.GetValue(obj)).ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    dic[tmp.Name] = tmp.GetValue(obj) + "";
                }
            }

            return dic;
        }

        public string GetInsertQuery(string tableName, Dictionary<string, string> data)
        {

            StringBuilder sb = new StringBuilder();
            StringBuilder sb_header = new StringBuilder();
            StringBuilder sb_value = new StringBuilder();

            sb.Append("insert into ");
            sb.Append(tableName);
            sb.Append(" (");

            foreach (string key in data.Keys)
            {
                sb_header.Append(key);
                sb_header.Append(",");

                sb_value.Append(data[key]);
                sb_value.Append(",");
            }

            sb.Append(sb_header.ToString().TrimEnd(','));
            sb.Append(") ");
            sb.Append("values (");
            sb.Append(sb_value.ToString().TrimEnd(','));
            sb.Append(")");

            return sb.ToString();
        }

        public string GetInsertMultiQueryRT(string tableName, List<RealTimeData> dataList)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sb_value = new StringBuilder();

            sb.Append("insert into ");
            sb.Append(tableName);
            sb.Append(" (ITM_C,WK_DT,WK_TM,RT_AM,BF_PRE_AM,RT_RATE,SEL_AM,BUY_AM,AC_VOL,AC_TR_AM,NOW_AM,HIGH_AM,LOW_AM,BF_PRE_VOL,BF_PRE_RT_AM,CHG_AM,FEE_AM,ITM_VOL,UP_LIM_TM,DOWN_LIM_TM) values ");

            foreach (RealTimeData data in dataList)
            {
                sb_value.Append("('");
                sb_value.Append(data.ITM_C);
                sb_value.Append("','");
                sb_value.Append(data.WK_DT);
                sb_value.Append("','");
                sb_value.Append(data.WK_TM);
                sb_value.Append("','");
                sb_value.Append(data.RT_AM);
                sb_value.Append("','");
                sb_value.Append(data.BF_PRE_AM);
                sb_value.Append("','");
                sb_value.Append(data.RT_RATE);
                sb_value.Append("','");
                sb_value.Append(data.SEL_AM);
                sb_value.Append("','");
                sb_value.Append(data.BUY_AM);
                sb_value.Append("','");
                sb_value.Append(data.AC_VOL);
                sb_value.Append("','");
                sb_value.Append(data.AC_TR_AM);
                sb_value.Append("','");
                sb_value.Append(data.NOW_AM);
                sb_value.Append("','");
                sb_value.Append(data.HIGH_AM);
                sb_value.Append("','");
                sb_value.Append(data.LOW_AM);
                sb_value.Append("','");
                sb_value.Append(data.BF_PRE_VOL);
                sb_value.Append("','");
                sb_value.Append(data.BF_PRE_RT_AM);
                sb_value.Append("','");
                sb_value.Append(data.CHG_AM);
                sb_value.Append("','");
                sb_value.Append(data.FEE_AM);
                sb_value.Append("','");
                sb_value.Append(data.ITM_VOL);
                sb_value.Append("','");
                sb_value.Append(data.UP_LIM_TM);
                sb_value.Append("','");
                sb_value.Append(data.DOWN_LIM_TM);
                sb_value.Append("'),");
            }
            sb.Append(sb_value.ToString().TrimEnd(','));

            return sb.ToString();
        }

        public string GetInsertMultiQueryRT_v2(string tableName, List<RealTimeData_v2> dataList, string dt, string tm)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sb_value = new StringBuilder();

            sb.Append("insert into ");
            sb.Append(tableName);
            sb.Append(" (WK_DT,WK_TM,ITM_C,VOLUME,PRICE,ACC_VOLUME,ACC_AM,VOLUME_POWER) values ");

            foreach (RealTimeData_v2 data in dataList)
            {
                if (data.ITM_C == "" || data.VOLUME == "" || data.PRICE == "" || data.ACC_VOLUME == "" || data.ACC_AM == "" || data.VOLUME_POWER == "")
                {
                    StockLog.Logger.LOG.WriteLog("Error", "[GetInsertMultiQueryRT_v2]: Error Data: " + data.ITM_C + ", " + data.VOLUME + ", " + data.PRICE + ", " + data.ACC_VOLUME + ", " + data.ACC_AM + ", " + data.VOLUME_POWER);
                    continue;
                }

                sb_value.Append("('");
                sb_value.Append(dt);
                sb_value.Append("','");
                sb_value.Append(tm);
                sb_value.Append("','");
                sb_value.Append(data.ITM_C);
                sb_value.Append("','");
                sb_value.Append(data.VOLUME);
                sb_value.Append("','");
                sb_value.Append(data.PRICE);
                sb_value.Append("','");
                sb_value.Append(data.ACC_VOLUME);
                sb_value.Append("','");
                sb_value.Append(data.ACC_AM);
                sb_value.Append("','");
                sb_value.Append(data.VOLUME_POWER);
                sb_value.Append("'),");
            }

            if (sb_value.Length == 0)
            {
                return "";
            }

            sb.Append(sb_value.ToString().TrimEnd(','));

            return sb.ToString();
        }

        public string GetInsertMultiQueryRT_v2(string tableName, List<AskBidPrice> dataList, string dt, string tm, string dtm)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sb_value = new StringBuilder();

            sb.Append("insert into ");
            sb.Append(tableName);
            sb.Append(" ");
            sb.Append("(stock_code,wk_dt,wk_tm,wk_dtm,");
            sb.Append("ask1,ask1_qty,bid1,bid1_qty,");
            sb.Append("ask2,ask2_qty,bid2,bid2_qty,");
            sb.Append("ask3,ask3_qty,bid3,bid3_qty,");
            sb.Append("ask4,ask4_qty,bid4,bid4_qty,");
            sb.Append("ask5,ask5_qty,bid5,bid5_qty,");
            sb.Append("ask6,ask6_qty,bid6,bid6_qty,");
            sb.Append("ask7,ask7_qty,bid7,bid7_qty,");
            sb.Append("ask8,ask8_qty,bid8,bid8_qty,");
            sb.Append("ask9,ask9_qty,bid9,bid9_qty,");
            sb.Append("ask10,ask10_qty,bid10,bid10_qty");
            sb.Append(") values ");

            foreach (AskBidPrice data in dataList)
            {
                sb_value.Append("('");
                sb_value.Append(data.stock_code);
                sb_value.Append("','");
                sb_value.Append(dt);
                sb_value.Append("','");
                sb_value.Append(tm);
                sb_value.Append("','");
                sb_value.Append(dtm);
                sb_value.Append("','");
                sb_value.Append(data.ask1.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask1_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid1.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid1_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask2.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask2_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid2.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid2_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask3.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask3_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid3.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid3_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask4.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask4_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid4.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid4_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask5.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask5_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid5.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid5_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask6.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask6_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid6.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid6_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask7.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask7_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid7.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid7_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask8.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask8_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid8.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid8_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask9.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask9_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid9.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid9_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask10.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.ask10_qty.Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid10.TrimStart(pmfilter).Trim());
                sb_value.Append("','");
                sb_value.Append(data.bid10_qty.Trim());
                sb_value.Append("'),");
            }

            if (sb_value.Length == 0)
            {
                return "";
            }

            sb.Append(sb_value.ToString().TrimEnd(','));

            return sb.ToString();
        }

        public string GetInsertMultiQueryDay(string tableName, List<DayData> dataList)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sb_value = new StringBuilder();

            sb.Append("insert into ");
            sb.Append(tableName);
            sb.Append(" (code,date,start_price,high_price,row_price,end_price,diff_price,diff_rate,trading_volume,trading_price,credit_price,local_trading,foreigner_trading,agency_trading,foreign_trading,program_trading,foreign_rate,trading_power,foreign_owner,foreign_ratio,foreigner_net_purchase,agency_net_purchase,local_net_purchase,credit_balance_rate) values ");

            foreach (DayData data in dataList)
            {
                sb_value.Append("('");
                sb_value.Append(data.code);
                sb_value.Append("','");
                sb_value.Append(data.date);
                sb_value.Append("','");
                sb_value.Append(data.start_price);
                sb_value.Append("','");
                sb_value.Append(data.high_price);
                sb_value.Append("','");
                sb_value.Append(data.row_price);
                sb_value.Append("','");
                sb_value.Append(data.end_price);
                sb_value.Append("','");
                sb_value.Append(data.diff_price);
                sb_value.Append("','");
                sb_value.Append(data.diff_rate);
                sb_value.Append("','");
                sb_value.Append(data.trading_volume);
                sb_value.Append("','");
                sb_value.Append(data.trading_price);
                sb_value.Append("','");
                sb_value.Append(data.credit_price);
                sb_value.Append("','");
                sb_value.Append(data.local_trading);
                sb_value.Append("','");
                sb_value.Append(data.foreigner_trading);
                sb_value.Append("','");
                sb_value.Append(data.agency_trading);
                sb_value.Append("','");
                sb_value.Append(data.foreign_trading);
                sb_value.Append("','");
                sb_value.Append(data.program_trading);
                sb_value.Append("','");
                sb_value.Append(data.foreign_rate);
                sb_value.Append("','");
                sb_value.Append(data.trading_power);
                sb_value.Append("','");
                sb_value.Append(data.foreign_owner);
                sb_value.Append("','");
                sb_value.Append(data.foreign_ratio);
                sb_value.Append("','");
                sb_value.Append(data.foreigner_net_purchase);
                sb_value.Append("','");
                sb_value.Append(data.agency_net_purchase);
                sb_value.Append("','");
                sb_value.Append(data.local_net_purchase);
                sb_value.Append("','");
                sb_value.Append(data.credit_balance_rate);
                sb_value.Append("'),");
            }
            sb.Append(sb_value.ToString().TrimEnd(','));

            return sb.ToString();
        }

        public string GetInsertMultiQueryDay(string tableName, List<DailyData> dataList)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sb_value = new StringBuilder();

            sb.Append("insert into ");
            sb.Append(tableName);
            sb.Append(" (WK_DT,ITM_C,VOLUME,PRICE,START_AM,HIGH_AM,LOW_AM) values ");

            foreach (DailyData data in dataList)
            {
                sb_value.Append("('");
                sb_value.Append(data.WK_DT);
                sb_value.Append("','");
                sb_value.Append(data.ITM_C);
                sb_value.Append("','");
                sb_value.Append(data.VOLUME);
                sb_value.Append("','");
                sb_value.Append(data.PRICE);
                sb_value.Append("','");
                sb_value.Append(data.START_AM);
                sb_value.Append("','");
                sb_value.Append(data.HIGH_AM);
                sb_value.Append("','");
                sb_value.Append(data.LOW_AM);
                //sb_value.Append("','");
                //sb_value.Append(data.FIX_PRICE_YN);
                //sb_value.Append("','");
                //sb_value.Append(data.FIX_RATE);
                //sb_value.Append("','");
                //sb_value.Append(data.FIX_EVENT);
                //sb_value.Append("','");
                //sb_value.Append(data.WK_DTM);
                sb_value.Append("'),");
            }
            sb.Append(sb_value.ToString().TrimEnd(','));

            return sb.ToString();
        }

        public string GetInsertMultiQueryMin(string tableName, List<MinuteData> dataList)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sb_value = new StringBuilder();

            sb.Append("insert into ");
            sb.Append(tableName);
            sb.Append(" (WK_DT,WK_TM,ITM_C,VOLUME,PRICE,START_AM,HIGH_AM,LOW_AM) values ");

            foreach (MinuteData data in dataList)
            {
                sb_value.Append("('");
                sb_value.Append(data.WK_DT);
                sb_value.Append("','");
                sb_value.Append(data.WK_TM);
                sb_value.Append("','");
                sb_value.Append(data.ITM_C);
                sb_value.Append("','");
                sb_value.Append(data.VOLUME);
                sb_value.Append("','");
                sb_value.Append(data.PRICE);
                sb_value.Append("','");
                sb_value.Append(data.START_AM);
                sb_value.Append("','");
                sb_value.Append(data.HIGH_AM);
                sb_value.Append("','");
                sb_value.Append(data.LOW_AM);
                sb_value.Append("'),");
            }
            sb.Append(sb_value.ToString().TrimEnd(','));

            return sb.ToString();
        }

        public string GetInsertMultiQueryIndexData(string tableName, List<IndexData> dataList)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sb_value = new StringBuilder();

            sb.Append("insert into ");
            sb.Append(tableName);
            sb.Append(" (" +
                "wk_dt," +
                "wk_tm," +
                "index_code," +
                "index_name," +
                "index_value," +
                "cost_symbol," +
                "net_change," +
                "pct_change," +
                "trade_volume," +
                "trade_cost," +
                "higher_limit," +
                "higher," +
                "flat," +
                "lower," +
                "lower_limit," +
                "listed_item" +
                ") values ");

            foreach (IndexData data in dataList)
            {
                sb_value.Append("('");
                sb_value.Append(data.wk_dt);
                sb_value.Append("','");
                sb_value.Append(data.wk_tm);
                sb_value.Append("','");
                sb_value.Append(data.index_code);
                sb_value.Append("','");
                sb_value.Append(data.index_name);
                sb_value.Append("','");
                sb_value.Append(data.index_value);
                sb_value.Append("','");
                sb_value.Append(data.cost_symbol);
                sb_value.Append("','");
                sb_value.Append(data.net_change);
                sb_value.Append("','");
                sb_value.Append(data.pct_change);
                sb_value.Append("','");
                sb_value.Append(data.trade_volume);
                sb_value.Append("','");
                sb_value.Append(data.trade_cost);
                sb_value.Append("','");
                sb_value.Append(data.higher_limit);
                sb_value.Append("','");
                sb_value.Append(data.higher);
                sb_value.Append("','");
                sb_value.Append(data.flat);
                sb_value.Append("','");
                sb_value.Append(data.lower);
                sb_value.Append("','");
                sb_value.Append(data.lower_limit);
                sb_value.Append("','");
                sb_value.Append(data.listed_item);
                sb_value.Append("'),");
            }
            sb.Append(sb_value.ToString().TrimEnd(','));

            return sb.ToString();
        }

        public string GetInsertMultiQueryIndexDataDay(string tableName, List<IndexDataDay> dataList)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sb_value = new StringBuilder();

            sb.Append("insert into ");
            sb.Append(tableName);
            sb.Append(" (" +
                "wk_dt," +
                "index_code," +
                "index_value," +
                "volume," +
                "open," +
                "high," +
                "low," +
                "trade_cost" +
                ") values ");

            foreach (IndexDataDay data in dataList)
            {
                sb_value.Append("('");
                sb_value.Append(data.wk_dt);
                sb_value.Append("','");
                sb_value.Append(data.index_code);
                sb_value.Append("','");
                sb_value.Append(data.index_value);
                sb_value.Append("','");
                sb_value.Append(data.volume);
                sb_value.Append("','");
                sb_value.Append(data.open);
                sb_value.Append("','");
                sb_value.Append(data.high);
                sb_value.Append("','");
                sb_value.Append(data.low);
                sb_value.Append("','");
                sb_value.Append(data.trade_cost);
                sb_value.Append("'),");
            }
            sb.Append(sb_value.ToString().TrimEnd(','));

            return sb.ToString();
        }

        public string GetInsertMultiQueryTick(string tableName, List<TickData> dataList)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sb_value = new StringBuilder();

            sb.Append("insert into ");
            sb.Append(tableName);
            sb.Append(" (WK_DT,WK_TM,ITM_C,SEQ,VOLUME,PRICE) values ");

            foreach (TickData data in dataList)
            {
                sb_value.Append("('");
                sb_value.Append(data.WK_DT);
                sb_value.Append("','");
                sb_value.Append(data.WK_TM);
                sb_value.Append("','");
                sb_value.Append(data.ITM_C);
                sb_value.Append("','");
                sb_value.Append(data.SEQ);
                sb_value.Append("','");
                sb_value.Append(data.VOLUME);
                sb_value.Append("','");
                sb_value.Append(data.PRICE);
                sb_value.Append("'),");
            }
            sb.Append(sb_value.ToString().TrimEnd(','));

            return sb.ToString();
        }

        public string GetInsertMultiQueryAskBid(string tableName, List<AskBidPrice> dataList)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sb_value = new StringBuilder();

            sb.Append("insert into " + tableName + " (stock_code,wk_dt,wk_tm,seq" +
                  ",top_ask,top_ask_qty,top_bid,top_bid_qty" +
                  ",ask1,ask1_qty,ask1_qty_ratio,bid1,bid1_qty,bid1_qty_ratio" +
                  ",ask2,ask2_qty,ask2_qty_ratio,bid2,bid2_qty,bid2_qty_ratio" +
                  ",ask3,ask3_qty,ask3_qty_ratio,bid3,bid3_qty,bid3_qty_ratio" +
                  ",ask4,ask4_qty,ask4_qty_ratio,bid4,bid4_qty,bid4_qty_ratio" +
                  ",ask5,ask5_qty,ask5_qty_ratio,bid5,bid5_qty,bid5_qty_ratio" +
                  ",ask6,ask6_qty,ask6_qty_ratio,bid6,bid6_qty,bid6_qty_ratio" +
                  ",ask7,ask7_qty,ask7_qty_ratio,bid7,bid7_qty,bid7_qty_ratio" +
                  ",ask8,ask8_qty,ask8_qty_ratio,bid8,bid8_qty,bid8_qty_ratio" +
                  ",ask9,ask9_qty,ask9_qty_ratio,bid9,bid9_qty,bid9_qty_ratio" +
                  ",ask10,ask10_qty,ask10_qty_ratio,bid10,bid10_qty,bid10_qty_ratio" +
                  ",total_ask_qty,total_ask_qty_ratio,total_bid_qty,total_bid_qty_ratio) values ");

            foreach (AskBidPrice data in dataList)
            {
                sb_value.Append("('");
                sb_value.Append(data.stock_code);
                sb_value.Append("','");
                sb_value.Append(data.wk_dt);
                sb_value.Append("','");
                sb_value.Append(data.wk_tm);
                sb_value.Append("','");
                sb_value.Append(data.seq);

                sb_value.Append("','");
                sb_value.Append(data.ask1);
                sb_value.Append("','");
                sb_value.Append(data.ask1_qty);
                sb_value.Append("','");
                sb_value.Append(data.ask1_qty_ratio);
                sb_value.Append("','");
                sb_value.Append(data.bid1);
                sb_value.Append("','");
                sb_value.Append(data.bid1_qty);
                sb_value.Append("','");
                sb_value.Append(data.bid1_qty_ratio);

                sb_value.Append("','");
                sb_value.Append(data.ask2);
                sb_value.Append("','");
                sb_value.Append(data.ask2_qty);
                sb_value.Append("','");
                sb_value.Append(data.ask2_qty_ratio);
                sb_value.Append("','");
                sb_value.Append(data.bid2);
                sb_value.Append("','");
                sb_value.Append(data.bid2_qty);
                sb_value.Append("','");
                sb_value.Append(data.bid2_qty_ratio);

                sb_value.Append("','");
                sb_value.Append(data.ask3);
                sb_value.Append("','");
                sb_value.Append(data.ask3_qty);
                sb_value.Append("','");
                sb_value.Append(data.ask3_qty_ratio);
                sb_value.Append("','");
                sb_value.Append(data.bid3);
                sb_value.Append("','");
                sb_value.Append(data.bid3_qty);
                sb_value.Append("','");
                sb_value.Append(data.bid3_qty_ratio);

                sb_value.Append("','");
                sb_value.Append(data.ask4);
                sb_value.Append("','");
                sb_value.Append(data.ask4_qty);
                sb_value.Append("','");
                sb_value.Append(data.ask4_qty_ratio);
                sb_value.Append("','");
                sb_value.Append(data.bid4);
                sb_value.Append("','");
                sb_value.Append(data.bid4_qty);
                sb_value.Append("','");
                sb_value.Append(data.bid4_qty_ratio);

                sb_value.Append("','");
                sb_value.Append(data.ask5);
                sb_value.Append("','");
                sb_value.Append(data.ask5_qty);
                sb_value.Append("','");
                sb_value.Append(data.ask5_qty_ratio);
                sb_value.Append("','");
                sb_value.Append(data.bid5);
                sb_value.Append("','");
                sb_value.Append(data.bid5_qty);
                sb_value.Append("','");
                sb_value.Append(data.bid5_qty_ratio);

                sb_value.Append("','");
                sb_value.Append(data.ask6);
                sb_value.Append("','");
                sb_value.Append(data.ask6_qty);
                sb_value.Append("','");
                sb_value.Append(data.ask6_qty_ratio);
                sb_value.Append("','");
                sb_value.Append(data.bid6);
                sb_value.Append("','");
                sb_value.Append(data.bid6_qty);
                sb_value.Append("','");
                sb_value.Append(data.bid6_qty_ratio);

                sb_value.Append("','");
                sb_value.Append(data.ask7);
                sb_value.Append("','");
                sb_value.Append(data.ask7_qty);
                sb_value.Append("','");
                sb_value.Append(data.ask7_qty_ratio);
                sb_value.Append("','");
                sb_value.Append(data.bid7);
                sb_value.Append("','");
                sb_value.Append(data.bid7_qty);
                sb_value.Append("','");
                sb_value.Append(data.bid7_qty_ratio);

                sb_value.Append("','");
                sb_value.Append(data.ask8);
                sb_value.Append("','");
                sb_value.Append(data.ask8_qty);
                sb_value.Append("','");
                sb_value.Append(data.ask8_qty_ratio);
                sb_value.Append("','");
                sb_value.Append(data.bid8);
                sb_value.Append("','");
                sb_value.Append(data.bid8_qty);
                sb_value.Append("','");
                sb_value.Append(data.bid8_qty_ratio);

                sb_value.Append("','");
                sb_value.Append(data.ask9);
                sb_value.Append("','");
                sb_value.Append(data.ask9_qty);
                sb_value.Append("','");
                sb_value.Append(data.ask9_qty_ratio);
                sb_value.Append("','");
                sb_value.Append(data.bid9);
                sb_value.Append("','");
                sb_value.Append(data.bid9_qty);
                sb_value.Append("','");
                sb_value.Append(data.bid9_qty_ratio);

                sb_value.Append("','");
                sb_value.Append(data.ask10);
                sb_value.Append("','");
                sb_value.Append(data.ask10_qty);
                sb_value.Append("','");
                sb_value.Append(data.ask10_qty_ratio);
                sb_value.Append("','");
                sb_value.Append(data.bid10);
                sb_value.Append("','");
                sb_value.Append(data.bid10_qty);
                sb_value.Append("','");
                sb_value.Append(data.bid10_qty_ratio);

                sb_value.Append("','");
                sb_value.Append(data.total_ask_qty);
                sb_value.Append("','");
                sb_value.Append(data.total_ask_qty_ratio);
                sb_value.Append("','");
                sb_value.Append(data.total_bid_qty);
                sb_value.Append("','");
                sb_value.Append(data.total_bid_qty_ratio);
                sb_value.Append("'),");
            }
            sb.Append(sb_value.ToString().TrimEnd(','));

            return sb.ToString();
        }

        public string GetInsertUpdateQuery(string tableName, Dictionary<string, string> data)
        {

            StringBuilder sb = new StringBuilder();
            StringBuilder sb_header = new StringBuilder();
            StringBuilder sb_value = new StringBuilder();

            sb.Append("insert into ");
            sb.Append(tableName);
            sb.Append(" (");

            foreach (string key in data.Keys)
            {
                sb_header.Append("`");
                sb_header.Append(key);
                sb_header.Append("`");
                sb_header.Append(",");

                sb_value.Append("'");
                sb_value.Append(data[key]);
                sb_value.Append("'");
                sb_value.Append(",");
            }

            sb.Append(sb_header.ToString().TrimEnd(','));
            sb.Append(") ");
            sb.Append("values (");
            sb.Append(sb_value.ToString().TrimEnd(','));
            sb.Append(") on duplicate key update ");

            foreach (string key in data.Keys)
            {
                sb.Append("`");
                sb.Append(key);
                sb.Append("`");
                sb.Append("=");

                sb.Append("'");
                sb.Append(data[key]);
                sb.Append("'");
                sb.Append(",");
            }

            return sb.ToString().TrimEnd(',') + ";";
        }

        public void SerializeXML(string dirName, string fileName, Type type, object obj)
        {
            if (dirName.Length == 0)
            {
                dirName = ".\\";
            }

            if (dirName.LastIndexOf("\\") != dirName.Length - 1)
            {
                dirName += "\\";
            }

            DirectoryInfo dir = new DirectoryInfo(dirName);

            if (dir.Exists == false)
            {
                dir.Create();
            }

            using (Stream stream = new FileStream(dirName + fileName, FileMode.Create, FileAccess.Write))
            {
                XmlSerializer ser = new XmlSerializer(type);
                ser.Serialize(stream, obj);
            }
        }

        public object DeserializeXML(string fileName, Type type)
        {
            FileInfo file = new FileInfo(fileName);

            if (file.Exists)
            {
                using (Stream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    XmlSerializer ser = new XmlSerializer(type);
                    return ser.Deserialize(stream);
                }
            }
            return null;
        }
    }


}