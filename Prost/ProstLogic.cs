using AxKHOpenAPILib;
using MySqlConnector;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Prost
{
    public partial class ProstForm
    {
        readonly object rtLock = new object();
        readonly object tickLock = new object();
        readonly object askbidLock = new object();
        readonly object reqLock = new object();
        readonly object limitOrderLock = new object();
        readonly object recentOrderLock = new object();
        readonly object rtIndexLock = new object();
        readonly object retryLock = new object();


        string dayInforCurrentCode = "";
        string minInforCurrentCode = "";
        string tickInforCurrentCode = "";
        string askbidInforCurrentCode = "";
        bool dayInforContinue = true;
        bool minInforContinue = true;
        bool tickInforContinue = true;
        bool askbidInforContinue = true;
        ManualResetEvent basicInfoStopEvent = new ManualResetEvent(false);
        ManualResetEvent dayInfoStopEvent = new ManualResetEvent(false);
        ManualResetEvent minInfoStopEvent = new ManualResetEvent(false);
        ManualResetEvent tickInfoStopEvent = new ManualResetEvent(false);
        ManualResetEvent askbidInfoStopEvent = new ManualResetEvent(false);
        ManualResetEvent loginRestartEvent = new ManualResetEvent(false);
        ManualResetEvent retryEvent = new ManualResetEvent(false);

        DateTime lastHoldTime = DateTime.Now;
        DateTime queuingRecentTime = DateTime.Now;
        DateTime secondUpdateTime = DateTime.Now;
        DateTime askbidUpdateTime = DateTime.Now;
        List<List<StockData.DataStruct.RealTimeData>> realTimeDataList = new List<List<StockData.DataStruct.RealTimeData>>();
        List<StockData.DataStruct.StockBasic> stockBasicList = new List<StockData.DataStruct.StockBasic>();
        DBManager.MariaDBFactory mDBFactory = null;
        DBManager.MariaDBFactory mDBFactory_dev = null;
        StockData.DataStruct.RequestLimitList requestChecker = new StockData.DataStruct.RequestLimitList();

        Dictionary<string, StockData.DataStruct.OrderInformation> dic_recentOrder = new Dictionary<string, StockData.DataStruct.OrderInformation>();
        Dictionary<string, StockData.DataStruct.StockBasic> dic_basicInfo = new Dictionary<string, StockData.DataStruct.StockBasic>();
        Dictionary<string, StockData.DataStruct.IndexData> dic_indexData = new Dictionary<string, StockData.DataStruct.IndexData>();

        List<Action> RetryQueue = new List<Action>();


        DateTime lastTradeTime = DateTime.Now;
        int lastTradeCount = 0;

        Dictionary<string, DateTime?> RQCheckBox = new Dictionary<string, DateTime?>();
        ManualResetEvent stopHeartBitEvent = new ManualResetEvent(false);
        bool heartBitStarted = false;
        //List<StockData.DataStruct.DailyData> dayDataList = new List<StockData.DataStruct.DailyData>();
        List<string> tickStockList = new List<string>();
        List<string> stockHoldList = new List<string>();
        List<string> tickValueList = new List<string>();
        List<string> askbidValueList = new List<string>();


        Dictionary<string, StockData.DataStruct.OrderInformation> limitOrderBidList = new Dictionary<string, StockData.DataStruct.OrderInformation>();
        Dictionary<string, StockData.DataStruct.OrderInformation> limitOrderAskList = new Dictionary<string, StockData.DataStruct.OrderInformation>();

        Stopwatch apiWatch = new Stopwatch();
        ManualResetEvent apiTimeOut = new ManualResetEvent(false);
        bool dayHistoryContinue = false;
        bool minHistoryContinue = false;
        bool tickHistoryContinue = false;
        char[] pmfilter = new char[] { '+', '-' };
        bool loopStop = false;
        string tick_lastTM = "";
        string askbid_lastTM = "";
        string rt_lastTM = "";
        int tick_index = 1;
        int askbid_index = 1;
        int rt_index = 1;
        bool isBatchStarted = false;

        List<string> realTypeList = new List<string>();

        private bool SystemOperationCheck(string[] args)
        {
            MySqlConnection connection = null;
            try
            {
                connection = mDBFactory.Connect();

                if (connection == null)
                {
                    StockLog.Logger.LOG.WriteLog("Error", "[SystemOperationCheck]: Database Connection Fail");
                    return true;
                }
                List<string> queryList = new List<string>();
                string sql = "";

                sql = "SELECT * FROM tb_operation WHERE client='" + StockData.Singleton.Store.Account.ID + "'";

                DataTable dataTable = mDBFactory.ExecuteDataTable(connection, sql);

                //StockLog.Logger.LOG.WriteLog("Test", "[SystemOperationCheck]: sql: " + sql);

                if (dataTable.Rows.Count > 0)
                {

                    //StockLog.Logger.LOG.WriteLog("Test", "[SystemOperationCheck]: count: " + dataTable.Rows.Count);

                    StockData.DataStruct.System_Operation so = new StockData.DataStruct.System_Operation();
                    StockData.Singleton.Store.GetDataTable<StockData.DataStruct.System_Operation>(dataTable.Rows[0], so);
                    if (so.restart == "1")
                    {
                        so.restart = "0";
                        mDBFactory.Execute(connection, StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.System_Operation>("tb_operation", so));
                        StockData.Singleton.Store.QueryLogSend("System", "Operation Restart Value On.");
                        RestartEvent?.Invoke();
                    }
                    if (so.exit == "1")
                    {
                        so.exit = "0";
                        mDBFactory.Execute(connection, StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.System_Operation>("tb_operation", so));
                        StockData.Singleton.Store.QueryLogSend("System", "Operation Exit Value On.");
                        ExitEvent?.Invoke();
                    }
                }
                else
                {
                    StockLog.Logger.LOG.WriteLog("Error", "[SystemOperationCheck]: tb_operation client: " + StockData.Singleton.Store.Account.ID + " is not found. sql: \n " + sql);
                }

                connection.Close();
            }
            catch (Exception exception)
            {
                StockLog.Logger.LOG.WriteLog("Exception", exception.ToString());
                if (connection != null)
                {
                    connection.Close();
                }
            }

            return true;
        }

        private bool UpdateMinRealTimeData(string[] args)
        {
            MySqlConnection connection = null;
            try
            {
                string tableName = args[0];

                DateTime dateNow = DateTime.Now;
                if (queuingRecentTime.ToString("HHmm00") != dateNow.ToString("HHmm00"))
                {

                    connection = mDBFactory.Connect();

                    if (connection == null)
                    {
                        StockLog.Logger.LOG.WriteLog("Error", "[UpdateMinRealTimeData]: Database Connection Fail");
                        return true;
                    }

                    string sql = @"insert
	into
	tb_stock_price_rt_min
select
	a.wk_dt as wk_dt,
	'" + queuingRecentTime.ToString("HHmm00") + @"' as wk_tm,
	a.itm_c as itm_c,
	a.s_volume as volume,
	b.price as price,
	b.acc_volume as acc_volume,
	b.acc_am as acc_am,
	a.a_volume_power as volume_power
from
	(
	select
		wk_dt,
		max(wk_tm) as wk_tm,
		itm_c,
		max(seq) as seq,
		sum(volume) as s_volume,
		avg(volume_power) a_volume_power
	from
		tb_stock_price_rt_tick
	where
		wk_dt = '" + dateNow.ToString("yyyy-MM-dd") + @"'
		and wk_tm >= '" + queuingRecentTime.ToString("HHmm00") + @"'
		and wk_tm < '" + dateNow.ToString("HHmm00") + @"'
	group by
		wk_dt,
		itm_c) a
join tb_stock_price_rt_tick b on
	a.wk_dt = b.wk_dt
	and a.wk_tm = b.wk_tm
	and a.itm_c = b.itm_c
	and a.seq = b.seq";

                    queuingRecentTime = DateTime.Now;

                    mDBFactory.Execute(connection, sql);
                    //TimeChecker(() =>
                    //{
                    //    lock (rtLock)
                    //    {
                    //        foreach (StockData.DataStruct.RealTimeData_v2 stockData in StockData.Singleton.Store.StockRealTimeData.Values)
                    //        {
                    //            if (stockData.ITM_C != "" && stockData.ITM_C != null)
                    //            {
                    //                if (stockData.PRICE != "")
                    //                {
                    //                    stockList.Add(stockData);

                    //                    if (stockList.Count >= 1000)
                    //                    {
                    //                        queryList.Add(StockData.Singleton.Store.GetInsertMultiQueryRT_v2(tableName, stockList, dt, tm));
                    //                        stockList = new List<StockData.DataStruct.RealTimeData_v2>();
                    //                    }
                    //                }
                    //            }
                    //        }
                    //        if (stockList.Count > 0)
                    //        {
                    //            queryList.Add(StockData.Singleton.Store.GetInsertMultiQueryRT_v2(tableName, stockList, dt, tm));
                    //            stockList = new List<StockData.DataStruct.RealTimeData_v2>();
                    //        }
                    //    }
                    //}, "UpdateMinRealTimeData Get Query");

                    //TimeChecker(() =>
                    //{
                    //    foreach (string sql in queryList)
                    //    {
                    //        mDBFactory.Execute(connection, sql);
                    //    }
                    //}, "UpdateMinRealTimeData SQL Send");

                    if (StockData.Singleton.Store.Account_Privileges.min_update_rt == "1")
                    {
                        dateNow = DateTime.Now;

                        mDBFactory.Execute(connection,
                            "insert into tb_update_time (id, wk_dt, minute_time) values ('main', '" + dateNow.ToString("yyyyMMdd") + "', '" + dateNow.ToString("HHmmssffffff") + "') " +
                            "on duplicate key update id='main', wk_dt='" + dateNow.ToString("yyyyMMdd") + "', minute_time='" + dateNow.ToString("HHmmssffffff") + "'");

                    }

                    connection.Close();
                }
            }
            catch (Exception exception)
            {
                StockLog.Logger.LOG.WriteLog("Exception", exception.ToString());
                if (connection != null)
                {
                    connection.Close();
                }
                return false;
            }

            return true;
        }

        private bool UpdateSecRealTimeData(string[] args)
        {
            MySqlConnection connection = null;
            try
            {
                string tableName = args[0];

                DateTime dateNow = DateTime.Now;
                if (secondUpdateTime.ToString("HHmmss") != dateNow.ToString("HHmmss"))
                {
                    secondUpdateTime = dateNow;
                    if (secondUpdateTime.ToString("HHmmss") == "153001" || secondUpdateTime.ToString("HHmmss") == "153002")
                    {
                        return true;
                    }

                    connection = mDBFactory.Connect();

                    if (connection == null)
                    {
                        StockLog.Logger.LOG.WriteLog("Error", "[UpdateSecRealTimeData]: Database Connection Fail");
                        return true;
                    }
                    List<StockData.DataStruct.RealTimeData_v2> stockList = new List<StockData.DataStruct.RealTimeData_v2>();
                    List<string> queryList = new List<string>();

                    string dtm = dateNow.ToString("yyyy-MM-dd HH:mm:ss");
                    string dt = dateNow.ToString("yyyyMMdd");
                    string tm = dateNow.ToString("HHmmss");

                    TimeChecker(() =>
                    {
                        //all stock list
                        foreach (StockData.DataStruct.RealTimeData_v2 stockData in StockData.Singleton.Store.StockRealTimeData.Values)
                        {
                            if (stockData.ITM_C != "" && stockData.ITM_C != null)
                            {
                                if (stockData.PRICE != "")
                                {
                                    stockList.Add(stockData);

                                    if (stockList.Count >= 1000)
                                    {
                                        queryList.Add(StockData.Singleton.Store.GetInsertMultiQueryRT_v2(tableName, stockList, dt, tm));
                                        stockList = new List<StockData.DataStruct.RealTimeData_v2>();
                                    }
                                }
                            }
                        }
                        if (stockList.Count > 0)
                        {
                            queryList.Add(StockData.Singleton.Store.GetInsertMultiQueryRT_v2(tableName, stockList, dt, tm));
                            stockList = new List<StockData.DataStruct.RealTimeData_v2>();
                        }

                    }, "UpdateSecRealTimeData Get Query");

                    TimeChecker(() =>
                    {
                        foreach (string sql in queryList)
                        {
                            mDBFactory.Execute(connection, sql);

                        }
                    }, "UpdateSecRealTimeData SQL Send");

                    if (StockData.Singleton.Store.Account_Privileges.sec_update_rt == "1")
                    {
                        dateNow = DateTime.Now;

                        mDBFactory.Execute(connection,
                            "insert into tb_update_time (id, wk_dt, second_time) values ('main', '" + dateNow.ToString("yyyyMMdd") + "', '" + dateNow.ToString("HHmmssffffff") + "') " +
                            "on duplicate key update id='main', wk_dt='" + dateNow.ToString("yyyyMMdd") + "', second_time='" + dateNow.ToString("HHmmssffffff") + "'");

                    }


                    connection.Close();
                }
            }
            catch (Exception exception)
            {
                StockLog.Logger.LOG.WriteLog("Exception", exception.ToString());
                if (connection != null)
                {
                    connection.Close();
                }
                return false;
            }

            return true;
        }

        private bool UpdateTickRealTimeData(string[] args)
        {
            MySqlConnection connection = null;
            try
            {
                string tableName = args[0];

                List<string> queryList = new List<string>();
                string sql = "";
                lock (tickLock)
                {
                    if (tickValueList.Count > 0)
                    {
                        queryList = tickValueList;
                        tickValueList = new List<string>();
                    }
                }


                List<string> sqlList = new List<string>();
                if (queryList.Count > 0)
                {
                    int cnt = queryList.Count;
                    TimeChecker(() =>
                    {
                        connection = mDBFactory.Connect();

                        if (connection == null)
                        {
                            StockLog.Logger.LOG.WriteLog("Error", "[UpdateTickRealTimeData]: Database Connection Fail");
                            return;
                        }

                        string header = "insert into " + tableName + " (WK_DT,WK_TM,ITM_C,SEQ,WK_TM_MILLI,WK_TM_REAL,VOLUME,PRICE,ACC_VOLUME,ACC_AM,VOLUME_POWER) values ";

                        for (int i = 0; i < queryList.Count; i++)
                        {
                            sqlList.Add(queryList[i]);

                            if (sqlList.Count > 500)
                            {
                                sql = header + String.Join(",", sqlList);
                                sqlList = new List<string>();

                                try
                                {
                                    mDBFactory.Execute(connection, sql);
                                }
                                catch (Exception except)
                                {
                                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                                    ThreadPool.QueueUserWorkItem(RetryQuery, sql);
                                }
                            }
                        }

                        if (sqlList.Count > 0)
                        {
                            sql = header + String.Join(",", sqlList);
                            sqlList = new List<string>();

                            try
                            {
                                mDBFactory.Execute(connection, sql);
                            }
                            catch (Exception except)
                            {
                                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                                StockLog.Logger.LOG.WriteLog("Exception", "Exception Query: " + sql);
                                ThreadPool.QueueUserWorkItem(RetryQuery, sql);
                            }
                        }

                        connection.Close();
                    }, "UpdateTickRealTimeData Get Query: " + cnt);
                }
            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                if (connection != null)
                {
                    connection.Close();
                }
                return false;
            }
            return true;
        }

        private bool UpdateAskBidRealTimeData(string[] args)
        {
            MySqlConnection connection = null;
            try
            {
                string tableName = args[0];

                List<string> queryList = new List<string>();
                string sql = "";
                lock (askbidLock)
                {
                    if (askbidValueList.Count > 0)
                    {
                        queryList = askbidValueList;
                        askbidValueList = new List<string>();
                    }
                }

                List<string> sqlList = new List<string>();

                if (queryList.Count > 0)
                {
                    int cnt = queryList.Count;

                    TimeChecker(() =>
                    {
                        connection = mDBFactory.Connect();

                        if (connection == null)
                        {
                            StockLog.Logger.LOG.WriteLog("Error", "[UpdateAskBidRealTimeData]: Database Connection Fail");
                            return;
                        }

                        string header = "insert into " + tableName + " (stock_code,wk_dt,wk_tm,seq,wk_tm_milli,wk_tm_real" +
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
              ",total_ask_qty,total_ask_qty_ratio,total_bid_qty,total_bid_qty_ratio) values ";

                        for (int i = 0; i < queryList.Count; i++)
                        {
                            sqlList.Add(queryList[i]);

                            if (sqlList.Count > 500)
                            {
                                sql = header + String.Join(",", sqlList);
                                sqlList = new List<string>();

                                try
                                {
                                    mDBFactory.Execute(connection, sql);
                                }
                                catch (Exception except)
                                {
                                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                                    ThreadPool.QueueUserWorkItem(RetryQuery, sql);
                                }
                            }
                        }

                        if (sqlList.Count > 0)
                        {
                            sql = header + String.Join(",", sqlList);
                            sqlList = new List<string>();

                            try
                            {
                                mDBFactory.Execute(connection, sql);
                            }
                            catch (Exception except)
                            {
                                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                                ThreadPool.QueueUserWorkItem(RetryQuery, sql);
                            }
                        }

                        connection.Close();
                    }, "UpdateAskBidRealTimeData Get Query: " + cnt);
                }
            }
            catch (Exception exception)
            {
                StockLog.Logger.LOG.WriteLog("Exception", exception.ToString());
                if (connection != null)
                {
                    connection.Close();
                }
                return false;
            }

            return true;
        }

        private void RetryQuery(object param)
        {
            MySqlConnection connection = null;
            string sql = (string)param;


            for (int i = 0; i < 3; i++)
            {
                try
                {
                    connection = mDBFactory.Connect();
                    mDBFactory.Execute(connection, sql);

                    StockLog.Logger.LOG.WriteLog("System", "Retry Query Success, Try Count: " + (i + 1));

                    connection.Close();

                    break;
                }
                catch (Exception except)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", "Retry Query Fail, Try Count: " + (i + 1) + ", Exception: " + except.ToString());
                }

                connection.Close();
            }

        }

        private bool InformationChecker(string[] args)
        {
            try
            {

                //CheckHeartBIt();

                //Hold Stock Info

                if (DateTime.Now.ToString("HHmm00") != lastHoldTime.ToString("HHmm00"))
                {
                    // Request 계좌수익률요청
                    lastHoldTime = DateTime.Now;

                    try
                    {
                        StockLog.Logger.LOG.WriteLog("Console", "[InformationChecker]: 계좌수익률요청 Account: " + StockData.Singleton.Store.Account.MainAccount);
                        axKHOpenAPI1.SetInputValue("계좌번호", StockData.Singleton.Store.Account.MainAccount);
                        int error = axKHOpenAPI1.CommRqData("계좌수익률요청", "opt10085", 0, StockData.Singleton.Store.GetScreenNumber());
                        return KiwoomErrorCatch(error);
                    }
                    catch (Exception except)
                    {
                        StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                    }

                    // Request 예수금상세현황요청

                    try
                    {
                        StockLog.Logger.LOG.WriteLog("Console", "[InformationChecker]: 예수금상세현황요청 Account: " + StockData.Singleton.Store.Account.MainAccount);
                        axKHOpenAPI1.SetInputValue("계좌번호", StockData.Singleton.Store.Account.MainAccount);
                        axKHOpenAPI1.SetInputValue("비밀번호", "0000");
                        axKHOpenAPI1.SetInputValue("비밀번호입력매체구분", "00");
                        axKHOpenAPI1.SetInputValue("조회구분", "2");
                        int error = axKHOpenAPI1.CommRqData("예수금상세현황요청", "opw00001", 0, StockData.Singleton.Store.GetScreenNumber());
                        return KiwoomErrorCatch(error);
                    }
                    catch (Exception except)
                    {
                        StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                    }


                    // Set HoldList
                    UpdateHoldList();
                }

            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
            }

            return true;
        }

        private void UpdateHoldList()
        {
            MySqlConnection connection = null;
            try
            {
                connection = mDBFactory.Connect();

                if (connection == null)
                {
                    StockLog.Logger.LOG.WriteLog("Error", "[UpdateHoldList]: Database Connection Fail");
                    return;
                }
                DataTable dataTable = mDBFactory.ExecuteDataTable(connection, "SELECT ITM_C FROM tb_stock_hold GROUP BY ITM_C");

                if (dataTable.Rows.Count > 0)
                {
                    List<string> holdList = new List<string>();
                    for (int i = 0; i < dataTable.Rows.Count; i++)
                    {
                        holdList.Add(dataTable.Rows[i][0] + "");
                    }
                    StockData.Singleton.Store.HoldStockCode = holdList;
                    stockHoldList = holdList;
                }
                connection.Close();
            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());

                if (connection != null)
                {
                    connection.Close();
                }
            }
        }

        private bool RequestCheck(string[] args)
        {
            DateTime dt = DateTime.Now;

            try
            {
                TimeSpan ts = new TimeSpan();
                lock (reqLock)
                {
                    foreach (string key in RQCheckBox.Keys)
                    {
                        if (RQCheckBox[key] != null)
                        {
                            ts = dt - RQCheckBox[key].Value;

                            if (ts.TotalMilliseconds > 10000)
                            {
                                StockLog.Logger.LOG.WriteLog("System", "[RequestCheck] API Timeout and Restart: " + key);
                                StockData.Singleton.Store.QueryLogSend("System", "Request Check Timeout. " + key);
                                RestartEvent?.Invoke();
                            }
                        }
                    }
                }
            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
            }

            try
            {
                dt = DateTime.Now;
                int difsec = 0;

                Dictionary<string, StockData.DataStruct.OrderInformation> orderList = new Dictionary<string, StockData.DataStruct.OrderInformation>();

                lock (limitOrderLock)
                {
                    foreach (string key in limitOrderBidList.Keys)
                    {
                        orderList[key] = limitOrderBidList[key];
                    }
                }

                List<string> removeList = new List<string>();
                foreach (string key in orderList.Keys)
                {

                    StockData.DataStruct.OrderInformation orderinfo = orderList[key];

                    difsec = int.Parse(dt.ToString("HHmmss")) - int.Parse(orderinfo.wk_tm);

                    if (difsec > 5)
                    {
                        StockLog.Logger.LOG.WriteLog("Error", "[RequestCheck] Limit Bid Order Timeout Code: " + orderinfo.stock_code + ", name: " + GetStockName(orderinfo.stock_code.Replace("A", "")) + ", order gubun: " + orderinfo.order_gubun + ", order num: " + key);
                        removeList.Add(key);

                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            try
                            {
                                StockLog.Logger.LOG.WriteLog("Test", "Start Check Pool: limitOrderBidList");
                                int diff = 1;

                                while (diff != 0)
                                {
                                    diff = (int)requestChecker.CheckTime(DateTime.Now);
                                    Thread.Sleep(diff);
                                }

                                int error = axKHOpenAPI1.SendOrder("매수취소", StockData.Singleton.Store.GetScreenNumber(), StockData.Singleton.Store.Account.MainAccount, 3, orderinfo.stock_code.Replace("A", ""), 0, int.Parse(orderinfo.order_qty), "00", key);
                                if (error == 0 || error == 1)
                                {
                                    StockLog.Logger.LOG.WriteLog("TradeLog", orderinfo.stock_code.Replace("A", "") + "Cancel Success!");
                                }
                            }
                            catch (Exception except)
                            {
                                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                            }
                        });

                    }
                }
                lock (limitOrderLock)
                {
                    foreach (string key in removeList)
                    {
                        StockLog.Logger.LOG.WriteLog("TradeLog", "Remove Check Stock: " + GetStockName(limitOrderBidList[key].stock_code.Replace("A", "")) + ", order no: " + key);
                        limitOrderBidList.Remove(key);
                    }
                }
            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
            }

            try
            {
                dt = DateTime.Now;
                int difsec = 0;

                Dictionary<string, StockData.DataStruct.OrderInformation> orderList = new Dictionary<string, StockData.DataStruct.OrderInformation>();
                lock (limitOrderLock)
                {
                    foreach (string key in limitOrderAskList.Keys)
                    {
                        orderList[key] = limitOrderAskList[key];
                    }
                }

                List<string> removeList = new List<string>();
                foreach (string key in orderList.Keys)
                {
                    StockData.DataStruct.OrderInformation orderinfo = orderList[key];

                    difsec = int.Parse(dt.ToString("HHmmss")) - int.Parse(orderinfo.wk_tm);

                    if (difsec > 1)
                    {
                        StockLog.Logger.LOG.WriteLog("TradeLog", "[RequestCheck] Limit Ask Order Timeout Code: " + orderinfo.stock_code + ", name: " + GetStockName(orderinfo.stock_code.Trim('A')) + ", order gubun: " + orderinfo.order_gubun + ", order num: " + key);
                        removeList.Add(key);

                        ThreadPool.QueueUserWorkItem(_ =>
                        {

                            try
                            {
                                StockLog.Logger.LOG.WriteLog("Test", "Start Check Pool: limitOrderAskList");

                                int diff = 1;
                                while (diff != 0)
                                {
                                    diff = (int)requestChecker.CheckTime(DateTime.Now);
                                    Thread.Sleep(diff);
                                }

                                //if (diff != 0)
                                //{
                                //    StockLog.Logger.LOG.WriteLog("TradeLog", "Sleep: " + diff);
                                //    Thread.Sleep(diff);
                                //}

                                int error = axKHOpenAPI1.SendOrder("매수취소", StockData.Singleton.Store.GetScreenNumber(), StockData.Singleton.Store.Account.MainAccount, 3, orderinfo.stock_code.Trim('A'), 0, int.Parse(orderinfo.order_qty), "00", key);
                                if (error == 0 || error == 1)
                                {
                                    StockLog.Logger.LOG.WriteLog("TradeLog", orderinfo.stock_code.Replace("A", "") + " Cancel Success!");
                                }

                                int qty = 0;

                                lock (recentOrderLock)
                                {
                                    if (dic_recentOrder.TryGetValue(orderinfo.stock_code.Replace("A", ""), out var order))
                                    {
                                        if (int.TryParse(order.trading_qty, out qty))
                                        {
                                            StockLog.Logger.LOG.WriteLog("TradeLog", "Sell Stock after limits price , " + GetStockName(orderinfo.stock_code.Replace("A", "")) + ", Sell QTY Change To: " + qty);
                                        }
                                    }
                                    else
                                    {
                                        StockLog.Logger.LOG.WriteLog("Error", "Fail22 Ask Not Exist Hold Qty: " + GetStockName(orderinfo.stock_code.Replace("A", "")) + ", code: " + orderinfo.stock_code.Replace("A", ""));
                                        return;
                                    }
                                }

                                diff = 1;
                                while (diff != 0)
                                {
                                    diff = (int)requestChecker.CheckTime(DateTime.Now);
                                    Thread.Sleep(diff);
                                }

                                //diff = (int)requestChecker.CheckTime(DateTime.Now);

                                //if (diff != 0)
                                //{
                                //    StockLog.Logger.LOG.WriteLog("TradeLog", "Sleep: " + diff);
                                //    Thread.Sleep(diff);
                                //}

                                error = axKHOpenAPI1.SendOrder("종목신규매도", StockData.Singleton.Store.GetScreenNumber(), StockData.Singleton.Store.Account.MainAccount, 2, orderinfo.stock_code.Replace("A", ""), qty, 0, "03", "");
                                if (error == 0 || error == 1)
                                {
                                    StockLog.Logger.LOG.WriteLog("TradeLog", orderinfo.stock_code.Replace("A", "") + "Sell Success!");
                                }
                            }
                            catch (Exception except)
                            {
                                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                            }
                        });
                    }
                }

                lock (limitOrderLock)
                {
                    foreach (string key in removeList)
                    {
                        StockLog.Logger.LOG.WriteLog("TradeLog", "Remove Check Stock: " + GetStockName(limitOrderAskList[key].stock_code.Replace("A", "")) + ", order no: " + key);
                        limitOrderAskList.Remove(key);
                    }
                }

            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
            }
            return true;
        }

        private bool LoadRtData(string[] args)
        {
            StockLog.Logger.LOG.WriteLog("Console", "[LoadRtData]: Start");
            StockData.Singleton.Store.QueryLogSend("Batch", "Execute RealDataMove.");
            MySqlConnection connection = null;

            try
            {
                connection = mDBFactory.Connect();

                if (connection == null)
                {
                    StockLog.Logger.LOG.WriteLog("Error", "[LoadRtData]: Database Connection Fail");
                    return true;
                }


                string sql = @"
select
	count(1)
from
	tb_stock_price_rt_ask_bid a
left outer join (
    select * 
    from tb_stock_price_ask_bid 
    where 1=1
    and wk_dt in (select wk_dt from tb_stock_price_rt_ask_bid group by wk_dt)
) b on
	a.wk_dt = b.wk_dt
	and a.wk_tm = b.wk_tm
	and a.seq = b.seq
	and a.stock_code = b.stock_code
where
    b.wk_tm is NULL;";

                DataTable dataTable = mDBFactory.ExecuteDataTable(connection, sql, 0);

                if (dataTable.Rows.Count > 0)
                {
                    StockLog.Logger.LOG.WriteLog("Console", "rtMoveAskBid Check Count: " + dataTable.Rows[0][0]);

                    if (dataTable.Rows[0][0].ToString() != "0")
                    {
                        sql = @"
insert into
	tb_stock_price_ask_bid
select
	a.*
from
	tb_stock_price_rt_ask_bid a
left outer join (
    select * 
    from tb_stock_price_ask_bid 
    where 1=1
    and wk_dt in (select wk_dt from tb_stock_price_rt_ask_bid group by wk_dt)
) b on
	a.wk_dt = b.wk_dt
	and a.wk_tm = b.wk_tm
	and a.seq = b.seq
	and a.stock_code = b.stock_code
where
    b.wk_tm is NULL;";
                        
                        mDBFactory.Execute(connection, sql, 0);

                        StockLog.Logger.LOG.WriteLog("Console", "[LoadRtData]: Move Askbid RT");
                    }

                    sql = @"
select
	count(1)
from
	tb_stock_price_rt_ask_bid a
left outer join (
    select * 
    from tb_stock_price_ask_bid 
    where 1=1
    and wk_dt in (select wk_dt from tb_stock_price_rt_ask_bid group by wk_dt)
) b on
	a.wk_dt = b.wk_dt
	and a.wk_tm = b.wk_tm
	and a.seq = b.seq
	and a.stock_code = b.stock_code
where
    b.wk_tm is NULL;";

                    dataTable = mDBFactory.ExecuteDataTable(connection, sql, 0);

                    if (dataTable.Rows.Count > 0)
                    {
                        StockLog.Logger.LOG.WriteLog("Console", "rtMoveAskBid Check Count: " + dataTable.Rows[0][0]);

                        if (dataTable.Rows[0][0].ToString() == "0")
                        {
                            sql = "truncate table tb_stock_price_rt_ask_bid;";

                            StockLog.Logger.LOG.WriteLog("Console", "[LoadRtData]: Remove Askbid RT");

                            mDBFactory.Execute(connection, sql, 0);
                        }
                    }
                }
                connection.Close();

            }
            catch (Exception exception)
            {
                StockLog.Logger.LOG.WriteLog("Console", "[LoadRtData]: Fail");
                StockLog.Logger.LOG.WriteLog("Exception", exception.ToString());
                if (connection != null)
                {
                    connection.Close();
                }
            }

            try 
            {
                connection = mDBFactory.Connect();

                if (connection == null)
                {
                    StockLog.Logger.LOG.WriteLog("Error", "[LoadRtData]: Database Connection Fail");
                    return true;
                }


                string sql = @"
select
	count(1)
from
	tb_stock_price_rt_tick a
left outer join (
    select * 
    from tb_stock_price_tick 
    where 1=1
    and wk_dt in (select wk_dt from tb_stock_price_rt_tick group by wk_dt)
) b on
	a.wk_dt = b.wk_dt
	and a.wk_tm = b.wk_tm
	and a.seq = b.seq
	and a.itm_c = b.itm_c
where
	b.wk_tm is null;";

                DataTable dataTable = mDBFactory.ExecuteDataTable(connection, sql, 0);

                if (dataTable.Rows.Count > 0)
                {
                    StockLog.Logger.LOG.WriteLog("Console", "rtMoveTick Check Count: " + dataTable.Rows[0][0]);

                    if (dataTable.Rows[0][0].ToString() != "0")
                    {

                        sql = @"
insert into
	tb_stock_price_tick
select
	a.*
from
	tb_stock_price_rt_tick a
left outer join (
    select * 
    from tb_stock_price_tick 
    where 1=1
    and wk_dt in (select wk_dt from tb_stock_price_rt_tick group by wk_dt)
) b on
	a.wk_dt = b.wk_dt
	and a.wk_tm = b.wk_tm
	and a.seq = b.seq
	and a.itm_c = b.itm_c
where
	b.wk_tm is null;";

                        mDBFactory.Execute(connection, sql, 0);

                        StockLog.Logger.LOG.WriteLog("Console", "[LoadRtData]: Move Tick RT");
                    }
                    sql = @"
select
	count(1)
from
	tb_stock_price_rt_tick a
left outer join (
    select * 
    from tb_stock_price_tick 
    where 1=1
    and wk_dt in (select wk_dt from tb_stock_price_rt_tick group by wk_dt)
) b on
	a.wk_dt = b.wk_dt
	and a.wk_tm = b.wk_tm
	and a.seq = b.seq
	and a.itm_c = b.itm_c
where
	b.wk_tm is null;";

                    dataTable = mDBFactory.ExecuteDataTable(connection, sql, 0);

                    if (dataTable.Rows.Count > 0)
                    {
                        StockLog.Logger.LOG.WriteLog("Console", "rtMoveTick Check Count: " + dataTable.Rows[0][0]);

                        if (dataTable.Rows[0][0].ToString() == "0")
                        {
                            sql = "truncate table tb_stock_price_rt_tick;";

                            mDBFactory.Execute(connection, sql, 0);

                            StockLog.Logger.LOG.WriteLog("Console", "[LoadRtData]: Remove Tick RT");
                        }

                    }
                }
                connection.Close();

                StockLog.Logger.LOG.WriteLog("Console", "[LoadRtData]: End");
                StockData.Singleton.Store.QueryLogSend("Batch", "Complate RealDataMove.");
            }
            catch (Exception exception)
            {
                StockLog.Logger.LOG.WriteLog("Console", "[LoadRtData]: Fail");
                StockLog.Logger.LOG.WriteLog("Exception", exception.ToString());
                if (connection != null)
                {
                    connection.Close();
                }
            }

            return true;
        }

        private void StartHeartBit()
        {
            if (!heartBitStarted)
            {
                heartBitStarted = true;

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    while (!stopHeartBitEvent.WaitOne(1000))
                    {
                        MySqlConnection connection = null;
                        try
                        {
                            connection = mDBFactory.Connect();
                            
                            if (connection == null)
                            {
                                StockLog.Logger.LOG.WriteLog("Error", "[StartHeartBit]: Database Connection Fail");
                                continue;
                            }

                            StockData.DataStruct.HeartBit hb = new StockData.DataStruct.HeartBit();
                            hb.clientid = StockData.Singleton.Store.Account.ID;
                            hb.process_name = "Treader";
                            hb.heartbit_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            mDBFactory.Execute(connection, StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.HeartBit>("tb_client_status", hb));
                            connection.Close();
                        }
                        catch (Exception except)
                        {
                            StockLog.Logger.LOG.WriteLog("Exception", except.ToString());

                            if (connection != null)
                            {
                                connection.Close();
                            }
                        }
                    }
                });
            }
        }

        private void CheckHeartBIt()
        {

            MySqlConnection connection = null;

            try
            {

                connection = mDBFactory.Connect();
                if (connection == null)
                {
                    StockLog.Logger.LOG.WriteLog("Error", "DB Connection Fail: CheckHeartBIt");
                    return;
                }

                DataTable dataTable = mDBFactory.ExecuteDataTable(connection, "select max(heartbit_time) from tb_client_status where clientid='" + StockData.Singleton.Store.AccessInfo.User + "'");

                if (dataTable.Rows.Count > 0)
                {
                    List<StockData.DataStruct.TradeData> dataList = new List<StockData.DataStruct.TradeData>();

                    StockLog.Logger.LOG.WriteLog("Console", "Count: " + dataTable.Rows.Count);

                    StockLog.Logger.LOG.WriteLog("Console", dataTable.Rows[0][0] + "");

                    //for (int i = 0; i < dataTable.Rows.Count; i++)
                    //{
                    //    StockLog.Logger.LOG.WriteLog("Console", dataTable.Rows[i][0] + "");
                    //}
                }

                connection.Close();
            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());

                if (connection != null)
                {
                    connection.Close();
                }
            }
        }

        private bool RestartReserve(string[] args)
        {
            StockLog.Logger.LOG.WriteLog("System", "RestartReserve Timer");
            RestartEvent?.Invoke();
            return true;
        }

        private bool TradeStock(string code, int price, int quantity, int untradeqty, int tradeNum, int orderType, int trade_gubun)
        {
            //            SendOrder(
            //BSTR sRQName, // 사용자 구분명
            //BSTR sScreenNo, // 화면번호
            //BSTR sAccNo,  // 계좌번호 10자리
            //LONG nOrderType,  // 주문유형 1:신규매수, 2:신규매도 3:매수취소, 4:매도취소, 5:매수정정, 6:매도정정
            //BSTR sCode, // 종목코드 (6자리)
            //LONG nQty,  // 주문수량
            //LONG nPrice, // 주문가격
            //BSTR sHogaGb,   // 거래구분(혹은 호가구분)은 아래 참고
            //BSTR sOrgOrderNo  // 원주문번호. 신규주문에는 공백 입력, 정정/취소시 입력합니다.
            //)


            //          서버에 주문을 전송하는 함수 입니다.
            //          9개 인자값을 가진 주식주문 함수이며 리턴값이 0이면 성공이며 나머지는 에러입니다.
            //          1초에 5회만 주문가능하며 그 이상 주문요청하면 에러 -308을 리턴합니다.
            //          ※ 시장가주문시 주문가격은 0으로 입력합니다.
            //          ※ 취소주문일때 주문가격은 0으로 입력합니다.

            //  [거래구분]
            //  00 : 지정가
            //  03 : 시장가
            //  05 : 조건부지정가
            //  06 : 최유리지정가
            //  07 : 최우선지정가
            //  10 : 지정가IOC
            //  13 : 시장가IOC
            //  16 : 최유리IOC
            //  20 : 지정가FOK
            //  23 : 시장가FOK
            //  26 : 최유리FOK
            //  61 : 장전시간외종가
            //  62 : 시간외단일가매매
            //  81 : 장후시간외종가
            //  ※ 모의투자에서는 지정가 주문과 시장가 주문만 가능합니다.

            string tradeMsg = GetOrderTypeName(orderType + "");

            StockLog.Logger.LOG.WriteLog("TradeLog", "Order " + tradeMsg + " Name: " + GetStockName(code) + ", quantity: " + quantity + ", price: " + price + ", t_gubun: " + GetTradeType(trade_gubun.ToString("D2")));

            if (quantity == 0)
            {
                return true;
            }

            return APIExecute(() =>
            {
                int error = 0;

                StockLog.Logger.LOG.WriteLog("TradeLog", "Order1 " + tradeMsg + " Name: " + GetStockName(code) + ", quantity: " + quantity + ", price: " + price + ", t_gubun: " + GetTradeType(trade_gubun.ToString("D2")));

                // 신규 매도 시 기존 매수에 대한 부분 체결 잔량이 남아 있을 경우 해당 거래에 해당 잔량에 대한 부분을 취소한다.
                if (orderType == 2 && untradeqty != 0)
                {
                    if (RetryQueue.Count > 0)
                    {
                        lock (retryLock)
                        {
                            RetryQueue.Add(() =>
                            {
                                string tmpScreen = StockData.Singleton.Store.GetScreenNumber();
                                axKHOpenAPI1.SendOrder(tradeMsg, tmpScreen, StockData.Singleton.Store.Account.MainAccount, 3, code, untradeqty, 0, trade_gubun.ToString("D2"), tradeNum.ToString());
                            });
                        }
                        retryEvent.Set();
                    }
                    else if (requestChecker.CheckTime(DateTime.Now) == 0)
                    {
                        string tmpScreen = StockData.Singleton.Store.GetScreenNumber();
                        axKHOpenAPI1.SendOrder(tradeMsg, tmpScreen, StockData.Singleton.Store.Account.MainAccount, 3, code, untradeqty, 0, trade_gubun.ToString("D2"), tradeNum.ToString());
                    }
                    else
                    {
                        StockLog.Logger.LOG.WriteLog("Error", "Send Order Fail Limit Over Request 5 in 1 Seconds " + "Name: " + GetStockName(code) + ", untrade_quantity: " + untradeqty + ", t_gubun: " + GetTradeType(trade_gubun.ToString("D2")));
                        lock (retryLock)
                        {
                            RetryQueue.Add(() =>
                            {
                                string tmpScreen = StockData.Singleton.Store.GetScreenNumber();
                                axKHOpenAPI1.SendOrder(tradeMsg, tmpScreen, StockData.Singleton.Store.Account.MainAccount, 3, code, untradeqty, 0, trade_gubun.ToString("D2"), tradeNum.ToString());
                            });
                            retryEvent.Set();
                        }
                    }
                }

                if (RetryQueue.Count > 0)
                {
                    StockLog.Logger.LOG.WriteLog("Error", "Enqueue RetryQueue" + "Name: " + GetStockName(code) + ", quantity: " + quantity + ", price: " + price + ", t_gubun: " + GetTradeType(trade_gubun.ToString("D2")));
                    lock (retryLock)
                    {
                        RetryQueue.Add(() =>
                        {
                            string tmpScreen = StockData.Singleton.Store.GetScreenNumber();
                            axKHOpenAPI1.SendOrder(tradeMsg, tmpScreen, StockData.Singleton.Store.Account.MainAccount, orderType, code, quantity, price, trade_gubun.ToString("D2"), "");
                        });
                        retryEvent.Set();
                    }
                }
                else if (requestChecker.CheckTime(DateTime.Now) == 0)
                {
                    error = axKHOpenAPI1.SendOrder(tradeMsg, StockData.Singleton.Store.GetScreenNumber(), StockData.Singleton.Store.Account.MainAccount, orderType, code, quantity, price, trade_gubun.ToString("D2"), "");
                    if (error == 0 || error == 1)
                    {
                        StockLog.Logger.LOG.WriteLog("Console", tradeMsg + "API Success!");
                    }
                }
                else
                {
                    StockLog.Logger.LOG.WriteLog("Error", "Send Order Fail Limit Over Request 5 in 1 Seconds " + "Name: " + GetStockName(code) + ", quantity: " + quantity + ", price: " + price + ", t_gubun: " + GetTradeType(trade_gubun.ToString("D2")));
                    lock (retryLock)
                    {
                        RetryQueue.Add(() =>
                        {
                            string tmpScreen = StockData.Singleton.Store.GetScreenNumber();
                            axKHOpenAPI1.SendOrder(tradeMsg, tmpScreen, StockData.Singleton.Store.Account.MainAccount, orderType, code, quantity, price, trade_gubun.ToString("D2"), "");
                        });
                        retryEvent.Set();
                    }
                }

                return KiwoomErrorCatch(error);


            }, tradeMsg);
        }

        private bool BuySellCheck(string[] args)
        {

            MySqlConnection connection = null;

            try
            {
                connection = mDBFactory.Connect();

                if (connection == null)
                {
                    StockLog.Logger.LOG.WriteLog("Error", "[BuySellCheck] Database Conntion Fail");
                    return true;
                }
                string query = "select * FROM tb_stock_trade WHERE WK_DT=" + DateTime.Now.ToString("yyyyMMdd") + " AND STS_DSC IN (0, 7) AND CLIENT_ID='" + StockData.Singleton.Store.Account.ID + "'";

                //StockLog.Logger.LOG.WriteLog("TradeLog", "query: " + query);

                DataTable dataTable = mDBFactory.ExecuteDataTable(connection, query);

                if (dataTable.Rows.Count > 0)
                {
                    List<StockData.DataStruct.TradeData> dataList = new List<StockData.DataStruct.TradeData>();

                    for (int i = 0; i < dataTable.Rows.Count; i++)
                    {
                        StockData.DataStruct.TradeData td = new StockData.DataStruct.TradeData();
                        td.CLIENT_ID = StockData.Singleton.Store.Account.ID;
                        StockData.Singleton.Store.GetDataTable<StockData.DataStruct.TradeData>(dataTable.Rows[i], td);
                        if (td.BID_DSC == null || td.BID_DSC == "")
                        {
                            td.BID_DSC = "0";
                        }
                        dataList.Add(td);
                    }

                    DateTime dt = DateTime.Now;

                    for (int i = 0; i < dataList.Count; i++)
                    {
                        StockLog.Logger.LOG.WriteLog("TradeLog", "date: " + dataList[i].DATE + ", " + GetStockName(dataList[i].ITM_C) + ", " + (dataList[i].GUBUN == "1" ? "매수" : "매도") + ", qty: " + dataList[i].NUM + ", Trade Type : " + GetTradeType(dataList[i].BID_DSC));

                        int qty = 0;
                        int price = 0;
                        int orderType = 0;
                        int bid_dsc = 0;
                        int untrade_qty = 0;
                        int order_num = 0;

                        if (!int.TryParse(dataList[i].GUBUN, out orderType))
                        {
                            StockLog.Logger.LOG.WriteLog("TradeLog", "Error: invalid GUBUN: " + GetOrderTypeName(dataList[i].GUBUN));
                        }

                        if (!int.TryParse(dataList[i].NUM, out qty))
                        {
                            StockLog.Logger.LOG.WriteLog("TradeLog", "Error: invalid NUM: " + dataList[i].NUM);
                        }

                        if (!int.TryParse(dataList[i].PRICE, out price))
                        {
                            StockLog.Logger.LOG.WriteLog("TradeLog", "Error: invalid PRICE: " + dataList[i].PRICE);
                        }

                        if (!int.TryParse(dataList[i].BID_DSC, out bid_dsc))
                        {
                            StockLog.Logger.LOG.WriteLog("TradeLog", "Error: invalid DIB_DSC: " + dataList[i].BID_DSC);
                        }

                        if (dataList[i].GUBUN == "2")
                        {
                            if (StockData.Singleton.Store.StockRealTimeData.TryGetValue(dataList[i].ITM_C, out StockData.DataStruct.RealTimeData_v2 realTimeData))
                            {
                                if (int.TryParse(realTimeData.PRICE, out int curPrice))
                                {
                                    // 팔려는 가격보다 현재 가격이 높다면 가격을 수정
                                    if (curPrice > price)
                                    {
                                        StockLog.Logger.LOG.WriteLog("TradeLog", "Current Price Over SellPrice " + dataList[i].ITM_C);
                                        price = curPrice;
                                        bid_dsc = 3;
                                    }
                                }
                            }
                        }


                        if (dataList[i].GUBUN == "1" && dataList[i].STS_DSC == "7")
                        {
                            StockLog.Logger.LOG.WriteLog("TradeLog", GetStockName(dataList[i].ITM_C) + ": GUBUN is 1 STS_DSC is 7");

                            if (StockData.Singleton.Store.StockRealTimeData.TryGetValue(dataList[i].ITM_C, out StockData.DataStruct.RealTimeData_v2 realTimeData))
                            {
                                if (!int.TryParse(realTimeData.PRICE, out int curPrice))
                                {
                                    StockLog.Logger.LOG.WriteLog("TradeLog", "curPrice can't parse to int: " + GetStockName(dataList[i].ITM_C) + ", PRICE: " + realTimeData.PRICE);
                                    //dataList[i].STS_DSC = "9";
                                    //dataList[i].WK_DTM = dt.ToString("yyyy-MM-dd HH:mm:ss");
                                    //mDBFactory.Execute(connection, StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.TradeData>("tb_stock_trade", dataList[i]));
                                    //continue;
                                }

                                // order 가격과 현재 가격의 차이가 1퍼센트 이상이라면 매수하지 않는다.
                                // 이 클라이언트가아닐 경우에 알수가없다.
                                //if (curPrice / price >= 1.01)
                                //{
                                //    StockLog.Logger.LOG.WriteLog("TradeLog", "curPrice gap over 1%: " + GetStockName(dataList[i].ITM_C));
                                //    dataList[i].STS_DSC = "9";
                                //    dataList[i].WK_DTM = dt.ToString("yyyy-MM-dd HH:mm:ss");
                                //    mDBFactory.Execute(connection, StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.TradeData>("tb_stock_trade", dataList[i]));
                                //    continue;
                                //}
                            }
                            else
                            {
                                StockLog.Logger.LOG.WriteLog("TradeLog", "it can't find obejct: " + GetStockName(dataList[i].ITM_C));
                                dataList[i].STS_DSC = "9";
                                dataList[i].WK_DTM = dt.ToString("yyyy-MM-dd HH:mm:ss");
                                mDBFactory.Execute(connection, StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.TradeData>("tb_stock_trade", dataList[i]));
                                continue;
                            }
                        }
                        else if (int.TryParse(dt.ToString("HHmmss"), out int dt1) && int.TryParse(dataList[i].WK_TM, out int dt2))
                        {
                            //거래가 올라온지 2초가 지났을 경우 해당 주문은 체결하지 않는다.
                            if (dataList[i].GUBUN == "1" && dt1 - dt2 > 2)
                            {
                                StockLog.Logger.LOG.WriteLog("TradeLog", "Cancel Trade, difference in time of more then 2 seconds. Stock: " + GetStockName(dataList[i].ITM_C));
                                StockLog.Logger.LOG.WriteLog("TradeLog", dataList[i].DATE + ", " + GetStockName(dataList[i].ITM_C) + ", Buy API Fail");
                                dataList[i].STS_DSC = "9";
                                dataList[i].WK_DTM = dt.ToString("yyyy-MM-dd HH:mm:ss");
                                mDBFactory.Execute(connection, StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.TradeData>("tb_stock_trade", dataList[i]));
                                continue;
                            }
                        }
                        else
                        {
                            StockLog.Logger.LOG.WriteLog("TradeLog", "Invalid Time Data dt1: " + dt.ToString("HHmmss") + ", dt2: " + dataList[i].WK_TM);
                            StockLog.Logger.LOG.WriteLog("TradeLog", dataList[i].DATE + ", " + GetStockName(dataList[i].ITM_C) + ", Buy API Fail");
                            dataList[i].STS_DSC = "9";
                            dataList[i].WK_DTM = dt.ToString("yyyy-MM-dd HH:mm:ss");
                            mDBFactory.Execute(connection, StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.TradeData>("tb_stock_trade", dataList[i]));
                            continue;
                        }


                        //취소 주문은 가격을 0으로
                        switch (orderType)
                        {
                            case 3:
                                price = 0;
                                break;
                            case 4:
                                price = 0;
                                break;
                        }

                        //시장가 주문은 가격을 0으로
                        switch (bid_dsc)
                        {
                            //시장가
                            case 3: price = 0; break;
                            //최유리지정가
                            case 6: price = 0; break;
                            //최우선지정가
                            case 7: price = 0; break;
                            //시장가IOC
                            case 13: price = 0; break;
                            //최유리IOC
                            case 16: price = 0; break;
                            //시장가FOK
                            case 23: price = 0; break;
                            //최유리FOK
                            case 26: price = 0; break;
                        }

                        if (orderType != 1)
                        {
                            if (orderType == 2)
                            {
                                lock (recentOrderLock)
                                {
                                    if (dic_recentOrder.TryGetValue(dataList[i].ITM_C, out var order))
                                    {
                                        if (int.TryParse(order.trading_qty, out qty))
                                        {
                                            StockLog.Logger.LOG.WriteLog("TradeLog", GetStockName(dataList[i].ITM_C) + ", " + GetOrderTypeName(dataList[i].GUBUN) + " QTY Change To: " + qty);
                                        }

                                        if (int.TryParse(order.unit_trade_qty, out untrade_qty))
                                        {
                                            StockLog.Logger.LOG.WriteLog("TradeLog", "untrade_qty: " + GetStockName(dataList[i].ITM_C) + ", " + GetOrderTypeName(dataList[i].GUBUN) + " QTY Change To: " + qty);
                                        }

                                        if (int.TryParse(order.order_num, out order_num))
                                        {
                                            StockLog.Logger.LOG.WriteLog("TradeLog", "order_num" + GetStockName(dataList[i].ITM_C) + ", " + GetOrderTypeName(dataList[i].GUBUN) + " QTY Change To: " + qty);
                                        }
                                    }
                                    else
                                    {
                                        StockLog.Logger.LOG.WriteLog("Error", "Fail " + GetOrderTypeName(dataList[i].GUBUN) + " Not Exist Hold Qty: " + GetStockName(dataList[i].ITM_C) + ", code: " + dataList[i].ITM_C);
                                        StockLog.Logger.LOG.WriteLog("TradeLog", dataList[i].DATE + ", " + GetStockName(dataList[i].ITM_C) + ", Fail " + GetOrderTypeName(dataList[i].GUBUN) + " Not Exist Hold Qty");
                                    }

                                }
                            }
                            else if (orderType == 3 || orderType == 4 || orderType == 5 || orderType == 6)
                            {
                                lock (recentOrderLock)
                                {
                                    if (dic_recentOrder.TryGetValue(dataList[i].ITM_C, out var order))
                                    {
                                        if (int.TryParse(order.untraded_qty, out qty))
                                        {
                                            StockLog.Logger.LOG.WriteLog("TradeLog", GetStockName(dataList[i].ITM_C) + ", " + GetOrderTypeName(dataList[i].GUBUN) + " QTY Untrade Change To: " + qty);
                                        }
                                    }
                                    else
                                    {
                                        StockLog.Logger.LOG.WriteLog("Error", "Fail " + GetOrderTypeName(dataList[i].GUBUN) + " Not Exist Hold Qty: " + GetStockName(dataList[i].ITM_C) + ", code: " + dataList[i].ITM_C);
                                        StockLog.Logger.LOG.WriteLog("TradeLog", dataList[i].DATE + ", " + GetStockName(dataList[i].ITM_C) + ", Fail " + GetOrderTypeName(dataList[i].GUBUN) + " Not Exist Hold Qty");
                                    }
                                }
                            }
                        }

                        //GUBUN 1
                        if (TradeStock(dataList[i].ITM_C, price, qty, untrade_qty, order_num, orderType, bid_dsc))
                        {
                            // this is a sts_dsc change code
                            StockLog.Logger.LOG.WriteLog("TradeLog", "Trade: " + dataList[i].DATE + ", " + GetStockName(dataList[i].ITM_C) + ", " + qty + ", API Success");
                            dataList[i].STS_DSC = "1";
                            dataList[i].WK_DTM = dt.ToString("yyyy-MM-dd HH:mm:ss");
                            mDBFactory.Execute(connection, StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.TradeData>("tb_stock_trade", dataList[i]));
                        }
                        else
                        {
                            StockLog.Logger.LOG.WriteLog("TradeLog", dataList[i].DATE + ", " + GetStockName(dataList[i].ITM_C) + ", API Fail");
                            dataList[i].STS_DSC = "9";
                            dataList[i].WK_DTM = dt.ToString("yyyy-MM-dd HH:mm:ss");
                            mDBFactory.Execute(connection, StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.TradeData>("tb_stock_trade", dataList[i]));
                        }

                    }

                }
                connection.Close();
            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                if (connection != null)
                {
                    connection.Close();
                }
            }

            return true;

        }

        private string makeQuery(string db_name, int gubun, string[,] arr)
        {
            //makeQuery(DB명,작업구분,PK KEY갯수,arr)
            // 1: insert 2:insert dup update 3:delete 4:select
            string query = "";
            switch (gubun)
            {
                case 1: //insert
                    query = "insert into " + db_name + "(" + makeBox(query, arr) + ") VALUES(" + makeParm(query, arr) + ");";
                    break;

                case 2:
                    query = "insert into " + db_name + "(" + makeBox(query, arr) + ") VALUES(" + makeParm(query, arr) + ")"
                             + " ON DUPLICATE KEY UPDATE " + makeEqual(query, arr);
                    break;
            }

            return query;

            string makeBox(string q, string[,] data)
            {
                for (int i = 0; i < data.GetLength(0); i++)
                {
                    q = q + data[i, 0];
                    if (i < data.GetLength(0) - 1) q = q + ",";
                }
                return q;
            }
            string makeParm(string q, string[,] data)
            {
                for (int i = 0; i < data.GetLength(0); i++)
                {
                    q = q + "'" + data[i, 1] + "'";
                    if (i < data.GetLength(0) - 1) q = q + ",";
                }
                return q;
            }
            string makeEqual(string q, string[,] data)
            {
                for (int i = 0; i < data.GetLength(0); i++)
                {
                    q = q + data[i, 0] + " = '" + data[i, 1] + "' ";
                    if (i < data.GetLength(0) - 1) q = q + ",";
                }
                return q;
            }

        }
        
        private string GetFIDName(string fid)
        {
            if (fid == "9201") { return "계좌번호"; }
            if (fid == "9203") { return "주문번호"; }
            if (fid == "9001") { return "종목코드"; }
            if (fid == "913") { return "주문상태"; }
            if (fid == "302") { return "종목명"; }
            if (fid == "900") { return "주문수량"; }
            if (fid == "901") { return "주문가격"; }
            if (fid == "902") { return "미체결수량"; }
            if (fid == "903") { return "체결누계금액"; }
            if (fid == "904") { return "원주문번호"; }
            if (fid == "905") { return "주문구분"; }
            if (fid == "906") { return "매매구분"; }
            if (fid == "907") { return "매도수구분"; }
            if (fid == "908") { return "주문/체결시간"; }
            if (fid == "909") { return "체결번호"; }
            if (fid == "910") { return "체결가"; }
            if (fid == "911") { return "체결량"; }
            if (fid == "10") { return "현재가"; }
            if (fid == "27") { return "(최우선)매도호가"; }
            if (fid == "28") { return "(최우선)매수호가"; }
            if (fid == "914") { return "단위체결가"; }
            if (fid == "915") { return "단위체결량"; }
            if (fid == "919") { return "거부사유"; }
            if (fid == "920") { return "화면번호"; }
            if (fid == "917") { return "신용구분"; }
            if (fid == "916") { return "대출일"; }
            if (fid == "930") { return "보유수량"; }
            if (fid == "931") { return "매입단가"; }
            if (fid == "932") { return "총매입가"; }
            if (fid == "933") { return "주문가능수량"; }
            if (fid == "945") { return "당일순매수수량"; }
            if (fid == "946") { return "매도/매수구분"; }
            if (fid == "950") { return "당일총매도손일"; }
            if (fid == "951") { return "예수금 (지원안함)"; }
            if (fid == "307") { return "기준가"; }
            if (fid == "8019") { return "손익율"; }
            if (fid == "957") { return "신용금액"; }
            if (fid == "958") { return "신용이자"; }
            if (fid == "918") { return "만기일"; }
            if (fid == "990") { return "당일실현손익(유가)"; }
            if (fid == "991") { return "당일실현손익률(유가)"; }
            if (fid == "992") { return "당일실현손익(신용)"; }
            if (fid == "993") { return "당일실현손익률(신용)"; }
            if (fid == "397") { return "파생상품거래단위"; }
            if (fid == "305") { return "상한가"; }
            if (fid == "306") { return "하한가"; }

            return "None";
        }

        private string GetFIDValue(int fid, StockData.DataStruct.OrderInformation orderinfo)
        {
            if (fid == 9201) { return orderinfo.account = axKHOpenAPI1.GetChejanData(fid); } // "계좌번호"; }
            if (fid == 9203) { return orderinfo.order_num = axKHOpenAPI1.GetChejanData(fid); } // "주문번호"; }
            if (fid == 9001) { return orderinfo.stock_code = axKHOpenAPI1.GetChejanData(fid); } // "종목코드"; }
                                                                                                //if (fid == 9205) { return orderinfo.stock_code = axKHOpenAPI1.GetChejanData(fid); } // "관리자사번"; }
            if (fid == 913) { return orderinfo.order_state = axKHOpenAPI1.GetChejanData(fid); } // "주문상태"; }
            if (fid == 302) { return orderinfo.order_name = axKHOpenAPI1.GetChejanData(fid); } // "종목명"; }
            if (fid == 900) { return orderinfo.order_qty = axKHOpenAPI1.GetChejanData(fid); } // "주문수량"; }
            if (fid == 901) { return orderinfo.order_price = axKHOpenAPI1.GetChejanData(fid); } // "주문가격"; }
            if (fid == 902) { return orderinfo.untraded_qty = axKHOpenAPI1.GetChejanData(fid); } // "미체결수량"; }
            if (fid == 903) { return orderinfo.trading_amount = axKHOpenAPI1.GetChejanData(fid); } // "체결누계금액"; }
            if (fid == 904) { return orderinfo.original_order_num = axKHOpenAPI1.GetChejanData(fid); } // "원주문번호"; }
            if (fid == 905) { return orderinfo.order_gubun = axKHOpenAPI1.GetChejanData(fid); } // "주문구분"; }
            if (fid == 906) { return orderinfo.sell_gubun = axKHOpenAPI1.GetChejanData(fid); } // "매매구분"; }
            if (fid == 907) { return orderinfo.medosugubun = axKHOpenAPI1.GetChejanData(fid); } // "매도수구분"; }
            if (fid == 908) { return orderinfo.order_trade_time = axKHOpenAPI1.GetChejanData(fid); } // "주문/체결시간"; }
            if (fid == 909) { return orderinfo.trading_num = axKHOpenAPI1.GetChejanData(fid); } // "체결번호"; }
            if (fid == 910) { return orderinfo.trading_price = axKHOpenAPI1.GetChejanData(fid); } // "체결가"; }
            if (fid == 911) { return orderinfo.trading_qty = axKHOpenAPI1.GetChejanData(fid); } // "체결량"; }
            if (fid == 10) { return orderinfo.current_price = axKHOpenAPI1.GetChejanData(fid); } // "현재가"; }
            if (fid == 27) { return orderinfo.bid = axKHOpenAPI1.GetChejanData(fid); } // "(최우선)매도호가"; }
            if (fid == 28) { return orderinfo.ask = axKHOpenAPI1.GetChejanData(fid); } // "(최우선)매수호가"; }
            if (fid == 914) { return orderinfo.unit_trade_price = axKHOpenAPI1.GetChejanData(fid); } // "단위체결가"; }
            if (fid == 915) { return orderinfo.unit_trade_qty = axKHOpenAPI1.GetChejanData(fid); } // "단위체결량"; }
            if (fid == 919) { return orderinfo.reason_rejection = axKHOpenAPI1.GetChejanData(fid); } // "거부사유"; }
            if (fid == 920) { return orderinfo.screen_number = axKHOpenAPI1.GetChejanData(fid); } // "화면번호"; }
                                                                                                  //if (fid == 917) { return orderinfo.credit_gubun = axKHOpenAPI1.GetChejanData(fid); } // "신용구분"; }
                                                                                                  //if (fid == 916) { return orderinfo.loan_day = axKHOpenAPI1.GetChejanData(fid); } // "대출일"; }
                                                                                                  //if (fid == 930) { return orderinfo.holding_qty = axKHOpenAPI1.GetChejanData(fid); } // "보유수량"; }
                                                                                                  //if (fid == 931) { return orderinfo.purchase_unit_price = axKHOpenAPI1.GetChejanData(fid); } // "매입단가"; }
                                                                                                  //if (fid == 932) { return orderinfo.total_purchase_price = axKHOpenAPI1.GetChejanData(fid); } // "총매입가"; }
                                                                                                  //if (fid == 933) { return orderinfo.order_avail_qty = axKHOpenAPI1.GetChejanData(fid); } // "주문가능수량"; }
                                                                                                  //if (fid == 945) { return orderinfo.net_buying_qty = axKHOpenAPI1.GetChejanData(fid); } // "당일순매수수량"; }
                                                                                                  //if (fid == 946) { return orderinfo.sell_buy_gubun = axKHOpenAPI1.GetChejanData(fid); } // "매도/매수구분"; }
                                                                                                  //if (fid == 950) { return orderinfo.today_gun_sell_sonil = axKHOpenAPI1.GetChejanData(fid); } // "당일총매도손일"; }
                                                                                                  //if (fid == 951) { return orderinfo.jesus_money = axKHOpenAPI1.GetChejanData(fid); } // "예수금 (지원안함)"; }
                                                                                                  //if (fid == 307) { return orderinfo.standard_price = axKHOpenAPI1.GetChejanData(fid); } // "기준가"; }
                                                                                                  //if (fid == 8019) { return orderinfo.margin_ratio = axKHOpenAPI1.GetChejanData(fid); } // "손익율"; }
                                                                                                  //if (fid == 957) { return orderinfo.credit_price = axKHOpenAPI1.GetChejanData(fid); } // "신용금액"; }
                                                                                                  //if (fid == 958) { return orderinfo.credit_interest = axKHOpenAPI1.GetChejanData(fid); } // "신용이자"; }
                                                                                                  //if (fid == 918) { return orderinfo.mangil = axKHOpenAPI1.GetChejanData(fid); } // "만기일"; }
                                                                                                  //if (fid == 990) { return orderinfo.today_get_margin_uga = axKHOpenAPI1.GetChejanData(fid); } // "당일실현손익(유가)"; }
                                                                                                  //if (fid == 991) { return orderinfo.today_get_margin_ratio_uga = axKHOpenAPI1.GetChejanData(fid); } // "당일실현손익률(유가)"; }
                                                                                                  //if (fid == 992) { return orderinfo.today_get_margin_credit = axKHOpenAPI1.GetChejanData(fid); } // "당일실현손익(신용)"; }
                                                                                                  //if (fid == 993) { return orderinfo.today_get_margin_ratio_credit = axKHOpenAPI1.GetChejanData(fid); } // "당일실현손익률(신용)"; }
                                                                                                  //if (fid == 397) { return orderinfo.pasangpumtradeunit = axKHOpenAPI1.GetChejanData(fid); } // "파생상품거래단위"; }
                                                                                                  //if (fid == 305) { return orderinfo.upper_limit = axKHOpenAPI1.GetChejanData(fid); } // "상한가"; }
                                                                                                  //if (fid == 306) { return orderinfo.lower_limit = axKHOpenAPI1.GetChejanData(fid); } // "하한가"; }
            return "" + fid;
        }

        private string nvl(string str, string to) { if (str == "") str = to; return str; }

        private List<StockData.DataStruct.DayData> GetDayData(_DKHOpenAPIEvents_OnReceiveTrDataEvent e, string code)
        {
            List<StockData.DataStruct.DayData> dayList = new List<StockData.DataStruct.DayData>();

            int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);

            for (int i = 0; i < nCnt; i++)
            {

                StockData.DataStruct.DayData dd = new StockData.DataStruct.DayData();

                dd.code = code;
                dd.date = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "날짜").Trim();
                int buf_int = 0;
                double buf_double = 0;
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "시가").Trim(), out buf_int))
                {
                    dd.start_price = buf_int;
                }
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "고가").Trim(), out buf_int))
                {
                    dd.high_price = buf_int;
                }
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "저가").Trim(), out buf_int))
                {
                    dd.row_price = buf_int;
                }
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종가").Trim(), out buf_int))
                {
                    dd.end_price = buf_int;
                }
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "전일비").Trim(), out buf_int))
                {
                    dd.diff_price = buf_int;
                }
                if (double.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "등락률").Trim(), out buf_double))
                {
                    dd.diff_rate = buf_double;
                }
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래량").Trim(), out buf_int))
                {
                    dd.trading_volume = buf_int;
                }
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "금액(백만)").Trim(), out buf_int))
                {
                    dd.trading_price = buf_int;
                }
                if (double.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "신용비").Trim(), out buf_double))
                {
                    dd.credit_price = buf_double;
                }
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "개인").Trim(), out buf_int))
                {
                    dd.local_trading = buf_int;
                }
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "기관").Trim(), out buf_int))
                {
                    dd.agency_trading = buf_int;
                }
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "외인수량").Trim(), out buf_int))
                {
                    dd.foreigner_trading = buf_int;
                }
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "외국계").Trim(), out buf_int))
                {
                    dd.foreign_trading = buf_int;
                }
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "프로그램").Trim(), out buf_int))
                {
                    dd.program_trading = buf_int;
                }
                if (double.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "외인비").Trim(), out buf_double))
                {
                    dd.foreign_rate = buf_double;
                }
                if (double.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결강도").Trim(), out buf_double))
                {
                    dd.trading_power = buf_double;
                }
                if (double.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "외인보유").Trim(), out buf_double))
                {
                    dd.foreign_owner = buf_double;
                }
                if (double.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "외인비중").Trim(), out buf_double))
                {
                    dd.foreign_ratio = buf_double;
                }
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "외인순매수").Trim(), out buf_int))
                {
                    dd.foreigner_net_purchase = buf_int;
                }
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "기관순매수").Trim(), out buf_int))
                {
                    dd.agency_net_purchase = buf_int;
                }
                if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "개인순매수").Trim(), out buf_int))
                {
                    dd.local_net_purchase = buf_int;
                }
                if (double.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "신용잔고율").Trim(), out buf_double))
                {
                    dd.credit_balance_rate = buf_double;
                }

                dayList.Add(dd);
            }

            return dayList;
        }

        private List<string> GetBasicStockList()
        {
            List<string> stockList = new List<string>();

            stockList.AddRange(StockData.Singleton.Store.KospiList);
            stockList.AddRange(StockData.Singleton.Store.KosdaqList);
            stockList.AddRange(StockData.Singleton.Store.ETFList);

            stockList.Sort();
            //try
            //{
            //    DBManager.MariaClient mdb = new DBManager.MariaClient(StockData.Singleton.Store.AccessInfo.Url, StockData.Singleton.Store.AccessInfo.Port, StockData.Singleton.Store.AccessInfo.User, StockData.Singleton.Store.AccessInfo.Password, StockData.Singleton.Store.AccessInfo.DBName);
            //    bool dbSuccess = mdb.Connect();

            //    if (!dbSuccess)
            //    {
            //        StockLog.Logger.LOG.WriteLog("Error", "DB Connection Fail: [getDailyStockData]");
            //        return null;
            //    }

            //    DataTable dataTable = mdb.Execute("SELECT DISTINCT(ITM_C) FROM tb_stock_bsc");
            //    if (dataTable.Rows.Count > 0)
            //    {
            //        for (int i = 0; i < dataTable.Rows.Count; i++)
            //        {
            //            stockList.Add(dataTable.Rows[i]["ITM_C"] + "");
            //        }
            //    }
            //    mdb.Close();
            //}
            //catch (Exception except)
            //{
            //    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
            //}

            return stockList;
        }

        private List<StockData.DataStruct.DailyData> GetDailyData(_DKHOpenAPIEvents_OnReceiveTrDataEvent e, string code)
        {
            List<StockData.DataStruct.DailyData> dayList = new List<StockData.DataStruct.DailyData>();

            int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);

            DateTime dt = DateTime.Now;

            for (int i = 0; i < nCnt; i++)
            {
                try
                {
                    StockData.DataStruct.DailyData dd = new StockData.DataStruct.DailyData();

                    dd.ITM_C = code;
                    dd.WK_DT = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "날짜").Trim();
                    dd.WK_DT = DateTime.ParseExact(dd.WK_DT, "yyyyMMdd", null).ToString("yyyyMMdd");
                    //dd.WK_DT = DateTime.ParseExact(dd.WK_DT, "yyyyMMdd", null).ToString("yyyy-MM-dd");
                    int buf_int = 0;
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "시가").Trim(), out buf_int))
                    {
                        dd.START_AM = buf_int < 0 ? buf_int * -1 : buf_int;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "고가").Trim(), out buf_int))
                    {
                        dd.HIGH_AM = buf_int < 0 ? buf_int * -1 : buf_int;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "저가").Trim(), out buf_int))
                    {
                        dd.LOW_AM = buf_int < 0 ? buf_int * -1 : buf_int;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종가").Trim(), out buf_int))
                    {
                        dd.PRICE = buf_int < 0 ? buf_int * -1 : buf_int;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래량").Trim(), out buf_int))
                    {
                        dd.VOLUME = buf_int < 0 ? buf_int * -1 : buf_int;
                    }

                    dayList.Add(dd);
                }
                catch (Exception except)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                }
            }

            return dayList;
        }

        private List<StockData.DataStruct.MinuteData> GetMinuteData(_DKHOpenAPIEvents_OnReceiveTrDataEvent e, string code)
        {
            List<StockData.DataStruct.MinuteData> minList = new List<StockData.DataStruct.MinuteData>();

            int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);

            DateTime dt = DateTime.Now;

            for (int i = 0; i < nCnt; i++)
            {
                try
                {
                    StockData.DataStruct.MinuteData md = new StockData.DataStruct.MinuteData();

                    md.ITM_C = code;

                    //StockLog.Logger.LOG.WriteLog("Console", "체결시간: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결시간").Trim());
                    DateTime tTime = DateTime.ParseExact(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결시간").Trim(), "yyyyMMddHHmmss", null);
                    md.WK_DT = tTime.ToString("yyyy-MM-dd");
                    md.WK_TM = tTime.ToString("HHmmss");

                    int buf_int = 0;
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "현재가").Trim(), out buf_int))
                    {
                        md.PRICE = buf_int < 0 ? buf_int * -1 : buf_int;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "시가").Trim(), out buf_int))
                    {
                        md.START_AM = buf_int < 0 ? buf_int * -1 : buf_int;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "고가").Trim(), out buf_int))
                    {
                        md.HIGH_AM = buf_int < 0 ? buf_int * -1 : buf_int;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "저가").Trim(), out buf_int))
                    {
                        md.LOW_AM = buf_int < 0 ? buf_int * -1 : buf_int;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래량").Trim(), out buf_int))
                    {
                        md.VOLUME = buf_int < 0 ? buf_int * -1 : buf_int;
                    }

                    minList.Add(md);
                }
                catch (Exception except)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                }
            }

            return minList;
        }

        private List<StockData.DataStruct.TickData> GetTickData(_DKHOpenAPIEvents_OnReceiveTrDataEvent e, string code)
        {
            List<StockData.DataStruct.TickData> tickList = new List<StockData.DataStruct.TickData>();

            int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);

            DateTime dt = DateTime.Now;

            for (int i = 0; i < nCnt; i++)
            {
                try
                {
                    StockData.DataStruct.TickData td = new StockData.DataStruct.TickData();

                    td.ITM_C = code;

                    //StockLog.Logger.LOG.WriteLog("Console", "체결시간: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결시간").Trim());
                    DateTime tTime = DateTime.ParseExact(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결시간").Trim(), "yyyyMMddHHmmss", null);
                    td.WK_DT = tTime.ToString("yyyy-MM-dd");
                    td.WK_TM = tTime.ToString("HHmmss");

                    int buf_int = 0;
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "현재가").Trim(), out buf_int))
                    {
                        td.PRICE = buf_int < 0 ? buf_int * -1 : buf_int;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래량").Trim(), out buf_int))
                    {
                        td.VOLUME = buf_int;
                    }
                    else
                    {
                        StockLog.Logger.LOG.WriteLog("Error", "Error Volume Parse: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래량").Trim());
                    }



                    tickList.Add(td);
                }
                catch (Exception except)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                }
            }

            return tickList;
        }

        private List<StockData.DataStruct.AskBidPrice> GetAskBidData(_DKHOpenAPIEvents_OnReceiveTrDataEvent e, string code)
        {

            List<StockData.DataStruct.AskBidPrice> askbidList = new List<StockData.DataStruct.AskBidPrice>();

            int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);

            DateTime dt = DateTime.Now;

            for (int i = 0; i < nCnt; i++)
            {
                try
                {
                    StockData.DataStruct.AskBidPrice abp = new StockData.DataStruct.AskBidPrice();

                    abp.stock_code = code;

                    //StockLog.Logger.LOG.WriteLog("Console", "체결시간: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결시간").Trim());

                    string standtime = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "호가잔량기준시간").Trim();
                    //DateTime tTime = DateTime.ParseExact(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "호가잔량기준시간").Trim(), "yyyyMMddHHmmss", null);
                    //abp.wk_dt = tTime.ToString("yyyy-MM-dd");
                    abp.wk_tm = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "호가잔량기준시간").Trim();

                    abp.top_ask = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도최우선호가").Trim();
                    abp.top_ask_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도최우선잔량").Trim();
                    abp.top_bid = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수최우선호가").Trim();
                    abp.top_bid_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수최우선잔량").Trim();

                    abp.ask1 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도1차선호가").Trim();
                    abp.ask1_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도1차선잔량").Trim();
                    abp.ask1_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도1차선잔량대비").Trim();
                    abp.bid1 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수1차선호가").Trim();
                    abp.bid1_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수1차선잔량").Trim();
                    abp.bid1_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수1차선잔량대비").Trim();

                    abp.ask2 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도2차선호가").Trim();
                    abp.ask2_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도2차선잔량").Trim();
                    abp.ask2_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도2차선잔량대비").Trim();
                    abp.bid2 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수2차선호가").Trim();
                    abp.bid2_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수2차선잔량").Trim();
                    abp.bid2_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수2차선잔량대비").Trim();

                    abp.ask3 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도3차선호가").Trim();
                    abp.ask3_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도3차선잔량").Trim();
                    abp.ask3_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도3차선잔량대비").Trim();
                    abp.bid3 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수3차선호가").Trim();
                    abp.bid3_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수3차선잔량").Trim();
                    abp.bid3_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수3차선잔량대비").Trim();

                    abp.ask4 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도4차선호가").Trim();
                    abp.ask4_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도4차선잔량").Trim();
                    abp.ask4_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도4차선잔량대비").Trim();
                    abp.bid4 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수4차선호가").Trim();
                    abp.bid4_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수4차선잔량").Trim();
                    abp.bid4_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수4차선잔량대비").Trim();

                    abp.ask5 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도5차선호가").Trim();
                    abp.ask5_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도5차선잔량").Trim();
                    abp.ask5_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도5차선잔량대비").Trim();
                    abp.bid5 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수5차선호가").Trim();
                    abp.bid5_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수5차선잔량").Trim();
                    abp.bid5_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수5차선잔량대비").Trim();

                    abp.ask6 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도6차선호가").Trim();
                    abp.ask6_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도6차선잔량").Trim();
                    abp.ask6_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도6차선잔량대비").Trim();
                    abp.bid6 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수6차선호가").Trim();
                    abp.bid6_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수6차선잔량").Trim();
                    abp.bid6_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수6차선잔량대비").Trim();

                    abp.ask7 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도7차선호가").Trim();
                    abp.ask7_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도7차선잔량").Trim();
                    abp.ask7_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도7차선잔량대비").Trim();
                    abp.bid7 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수7차선호가").Trim();
                    abp.bid7_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수7차선잔량").Trim();
                    abp.bid7_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수7차선잔량대비").Trim();

                    abp.ask8 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도8차선호가").Trim();
                    abp.ask8_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도8차선잔량").Trim();
                    abp.ask8_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도8차선잔량대비").Trim();
                    abp.bid8 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수8차선호가").Trim();
                    abp.bid8_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수8차선잔량").Trim();
                    abp.bid8_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수8차선잔량대비").Trim();

                    abp.ask1 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도9차선호가").Trim();
                    abp.ask9_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도9차선잔량").Trim();
                    abp.ask9_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도9차선잔량대비").Trim();
                    abp.bid9 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수9차선호가").Trim();
                    abp.bid9_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수9차선잔량").Trim();
                    abp.bid9_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수9차선잔량대비").Trim();

                    abp.ask10 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도10차선호가").Trim();
                    abp.ask10_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도10차선잔량").Trim();
                    abp.ask10_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도10차선잔량대비").Trim();
                    abp.bid10 = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수10차선호가").Trim();
                    abp.bid10_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수10차선잔량").Trim();
                    abp.bid10_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수10차선잔량대비").Trim();

                    abp.total_ask_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "총매도잔량").Trim();
                    abp.total_ask_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "총매도잔량직전대비").Trim();
                    abp.total_bid_qty = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "총매수잔량").Trim();
                    abp.total_bid_qty_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "총매수잔량직전대비").Trim();

                    askbidList.Add(abp);
                }
                catch (Exception except)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                }
            }


            return askbidList;
        }

        private string GetStockGroup(string code)
        {

            if (StockData.Singleton.Store.KospiList.Contains(code))
            {
                return "kospi";
            }
            else if (StockData.Singleton.Store.KosdaqList.Contains(code))
            {
                return "kosdaq";
            }
            else if (StockData.Singleton.Store.ETFList.Contains(code))
            {
                return "etf";
            }

            return "";
        }

        private string GetStockName(string code)
        {

            if (dic_basicInfo.TryGetValue(code, out StockData.DataStruct.StockBasic value))
            {
                return value.stock_name;
            }

            return code;
        }

        private string GetOrderTypeName(string orderType)
        {
            switch (orderType)
            {
                case "1": return "종목신규매수";
                case "2": return "종목신규매도";
                case "3": return "종목매수취소";
                case "4": return "종목매도취소";
                case "5": return "종목매수정정";
                case "6": return "종목매도정정";
            }
            return orderType;

        }

        private string GetTradeType(string type)
        {

            switch (type)
            {
                case "00": return "지정가";
                case "03": return "시장가";
                case "05": return "조건부지정가";
                case "06": return "최유리지정가";
                case "07": return "최우선지정가";
                case "10": return "지정가IOC";
                case "13": return "시장가IOC";
                case "16": return "최유리IOC";
                case "20": return "지정가FOK";
                case "23": return "시장가FOK";
                case "26": return "최유리FOK";
                case "61": return "장전시간외종가";
                case "62": return "시간외단일가매매";
            }

            return type;

        }

        private void RetryAction()
        {
            StockLog.Logger.LOG.WriteLog("APIOperation", "RetryAction Start");

            while (retryEvent.WaitOne())
            {
                StockLog.Logger.LOG.WriteLog("APIOperation", "RetryAction Action");

                retryEvent.Reset();
                lock (retryLock)
                {
                    StockLog.Logger.LOG.WriteLog("APIOperation", "RetryAction Action Count: " + RetryQueue.Count);
                    for (int i = 0; i < RetryQueue.Count; i++)
                    {
                        while (requestChecker.CheckTime(DateTime.Now) != 0)
                        {
                            Thread.Sleep(1);
                        }

                        RetryQueue[i]();

                    }
                    RetryQueue = new List<Action>();
                    StockLog.Logger.LOG.WriteLog("APIOperation", "RetryAction Action End");
                }
            }
        }

        private void SetInitAskBidData()
        {
            List<string> stockList = GetBasicStockList();

            for (int i = 0; i < stockList.Count; i++)
            {
                StockData.DataStruct.AskBidPrice buf = new StockData.DataStruct.AskBidPrice();
                buf.stock_code = stockList[i];
                StockData.Singleton.Store.AskBidData[stockList[i]] = buf;
            }
        }

        private void SetSotckCode()
        {
            StockLog.Logger.LOG.WriteLog("Console", "SetSotckCode");

            APIExecute(() =>
            {
                string accountlist = axKHOpenAPI1.GetLoginInfo("ACCLIST");
                string[] account = accountlist.Split(';');

                //전 종목 코드를 리턴 해줌
                AutoCompleteStringCollection stockcollection = new AutoCompleteStringCollection();

                string stockCode = axKHOpenAPI1.GetCodeListByMarket("");
                string[] stockCodeArray = stockCode.Split(';');
                for (int i = 0; i < stockCodeArray.Length; i++)
                {
                    StockData.DataStruct.RealTimeData_v2 si = new StockData.DataStruct.RealTimeData_v2();// = new StockData.DataStruct.RealTimeData(result);//, axKHOpenAPI1.GetMasterCodeName(stockCodeArray[i]));
                    si.ITM_C = stockCodeArray[i];
                    StockData.Singleton.Store.StockRealTimeData[stockCodeArray[i]] = si;
                }
                return true;
            }, "");
        }

        private void SetBasicInfo()
        {
            StockLog.Logger.LOG.WriteLog("Console", "SetBasicInfo");

            MySqlConnection connection = null;
            try
            {

                connection = mDBFactory.Connect();

                DataTable dataTable = mDBFactory.ExecuteDataTable(connection, "select * from tb_stock_bsc_v2");

                if (dataTable.Rows.Count > 0)
                {
                    List<StockData.DataStruct.StockBasic> dataList = new List<StockData.DataStruct.StockBasic>();

                    for (int i = 0; i < dataTable.Rows.Count; i++)
                    {
                        StockData.DataStruct.StockBasic sb = new StockData.DataStruct.StockBasic();
                        StockData.Singleton.Store.GetDataTable<StockData.DataStruct.StockBasic>(dataTable.Rows[i], sb);

                        dic_basicInfo[sb.stock_code] = sb;

                        //StockLog.Logger.LOG.WriteLog("Test", "code: " + sb.stock_code + ", " + sb.stock_name);
                    }
                }
                else
                {
                    StockLog.Logger.LOG.WriteLog("Error", "Not Exist tb_stock_bsc_v2");
                }

                connection.Close();
            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                if (connection != null)
                {
                    connection.Close();
                }

            }


            return;
        }

        private bool BatchDailyStockData(string[] args)
        {
            if (isBatchStarted)
            {
                StockLog.Logger.LOG.WriteLog("Console", "Batch Already Started, Cancel: BatchDailyStockData");
                return true;
            }

            isBatchStarted = true;

            StockLog.Logger.LOG.WriteLog("APIOperation", "GetDailyStockData Start");

            if (StockData.Singleton.Store.WorkInfo.DayCurrentCode == "")
            {
                StockData.Singleton.Store.QueryLogSend("Batch", "Execute GetDailyStockData.");
            }
            StockData.Singleton.Store.WorkInfo.DayStarted = true;
            StockData.Singleton.Store.SaveSetting();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                MySqlConnection connection = null;
                string tableName = "tb_stock_price_day_sub";
                try
                {
                    bool cflag = true;
                    int sleepTime = 600;
                    List<string> stockList = GetBasicStockList();

                    StockLog.Logger.LOG.WriteLog("APIOperation", "GetDailyStockData GetBasicStockList Count: " + stockList.Count);

                    for (int i = 0; i < stockList.Count; i++)
                    {
                        dayInforCurrentCode = stockList[i];

                        if (cflag)
                        {
                            if (dayHistoryContinue && !StockData.Singleton.Store.WorkInfo.DayHistoryEnd.Contains(stockList[i]))
                            {
                                cflag = false;
                            }
                            if (!dayHistoryContinue && (stockList[i] == StockData.Singleton.Store.WorkInfo.DayCurrentCode || StockData.Singleton.Store.WorkInfo.DayCurrentCode == ""))
                            {
                                cflag = false;
                            }

                            if (cflag)
                            {
                                continue;
                            }
                        }

                        if (i == 0)
                        {
                            connection = mDBFactory.Connect();
                            if (connection == null)
                            {
                                StockLog.Logger.LOG.WriteLog("Error", "[BatchDailyStockData]: Database Connection Fail. Batch Stop.");
                                isBatchStarted = false;
                                return;
                            }
                            mDBFactory.Execute(connection, "truncate table " + tableName);
                            connection.Close();
                        }

                        StockLog.Logger.LOG.WriteLog("APIOperation", dayInforCurrentCode + ": Stock Day Data Request Start");

                        dayInforContinue = true;
                        dayInfoStopEvent.Reset();

                        StockLog.Logger.LOG.WriteLog("APIOperation", dayInforCurrentCode + ": Stock Day Data Request First");
                        int error = RequestDayInformation(dayInforCurrentCode, 0);
                        KiwoomErrorCatch(error);

                        Thread.Sleep(sleepTime);

                        while (dayInfoStopEvent.WaitOne())
                        {
                            if (dayInforContinue)
                            {
                                dayInfoStopEvent.Reset();
                                StockLog.Logger.LOG.WriteLog("APIOperation", dayInforCurrentCode + ": Stock Day Data Request Next");
                                error = RequestDayInformation(dayInforCurrentCode, 2);
                                KiwoomErrorCatch(error);

                                Thread.Sleep(sleepTime);
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (loopStop)
                        {
                            loopStop = false;
                            break;
                        }
                    }

                    connection = mDBFactory.Connect();
                    if (connection == null)
                    {
                        StockLog.Logger.LOG.WriteLog("Error", "[BatchDailyStockData]: Database Connection Fail. Batch Stop.");
                        isBatchStarted = false;
                        return;
                    }
                    mDBFactory.Execute(connection, @"INSERT
	INTO
	tb_stock_price_day
SELECT
	a.wk_dt,
	a.itm_c,
	a.volume,
	a.price,
	a.start_am,
	a.high_am,
	a.low_am
FROM
	tb_stock_price_day_sub a
LEFT OUTER JOIN tb_stock_price_day b ON
	a.wk_dt = b.wk_dt
	AND a.itm_c = b.itm_c
WHERE
	a.wk_dt IN (
	SELECT
		DISTINCT wk_dt
	FROM
		tb_stock_price_day_sub)
	AND b.itm_c IS NULL");
                    connection.Clone();

                    StockLog.Logger.LOG.WriteLog("Console", "GetDailyStockData Complete!!");
                    StockLog.Logger.LOG.WriteLog("APIOperation", "dayInfoCurrentCode Init!!");
                    StockData.Singleton.Store.WorkInfo.DayStarted = false;
                    StockData.Singleton.Store.WorkInfo.DayCurrentCode = "";
                    StockData.Singleton.Store.SaveSetting();
                    dayInforCurrentCode = "";

                    StockData.Singleton.Store.QueryLogSend("Batch", "Complate GetDailyStockData.");
                }
                catch (Exception except)
                {
                    if (connection != null)
                    {
                        connection.Close();
                    }
                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                    RestartEvent?.Invoke();
                }
                isBatchStarted = false;
            });
            return true;
        }

        private bool BatchMinuteStockData(string[] args)
        {
            if (isBatchStarted)
            {
                StockLog.Logger.LOG.WriteLog("Console", "Batch Already Started, Cancel: BatchMinuteStockData");
                return true;
            }

            isBatchStarted = true;

            StockLog.Logger.LOG.WriteLog("APIOperation", "GetMinuteStockData Start");
            if (StockData.Singleton.Store.WorkInfo.MinCurrentCode == "")
            {
                StockData.Singleton.Store.QueryLogSend("Batch", "Execute GetMinuteStockData.");
            }

            StockData.Singleton.Store.WorkInfo.MinStarted = true;
            StockData.Singleton.Store.SaveSetting();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                MySqlConnection connection = null;
                string tableName = "tb_stock_price_min_sub";
                bool firstReq = true;

                List<string> completeList = new List<string>();
                try
                {
                    bool cflag = true;
                    int sleepTime = 600;
                    List<string> stockList = GetBasicStockList();

                    StockLog.Logger.LOG.WriteLog("APIOperation", "GetMinuteStockData GetBasicStockList Count: " + stockList.Count);

                    for (int i = 0; i < stockList.Count; i++)
                    {
                        minInforCurrentCode = stockList[i];

                        if (cflag)
                        {
                            if (minHistoryContinue && !StockData.Singleton.Store.WorkInfo.MinHistoryEnd.Contains(stockList[i]))
                            {
                                cflag = false;
                            }
                            if (!minHistoryContinue && (stockList[i] == StockData.Singleton.Store.WorkInfo.MinCurrentCode || StockData.Singleton.Store.WorkInfo.MinCurrentCode == ""))
                            {
                                cflag = false;
                            }

                            if (cflag)
                            {
                                continue;
                            }
                        }

                        if (completeList.Contains(minInforCurrentCode))
                        {
                            StockLog.Logger.LOG.WriteLog("Test", "Exist Code" + minInforCurrentCode);
                            continue;
                        }

                        completeList.Add(minInforCurrentCode);

                        StockData.Singleton.Store.WorkInfo.MinCurrentCode = minInforCurrentCode;
                        StockData.Singleton.Store.SaveSetting();

                        if (i == 0)
                        {
                            connection = mDBFactory_dev.Connect();
                            if (connection == null)
                            {
                                StockLog.Logger.LOG.WriteLog("Error", "[BatchMinuteStockData]: Database Connection Fail. Batch Stop.");
                                isBatchStarted = false;
                                return;
                            }
                            mDBFactory_dev.Execute(connection, "truncate table " + tableName);
                            connection.Close();
                        }
                        else if (firstReq)
                        {
                            firstReq = false;
                            connection = mDBFactory_dev.Connect();
                            if (connection == null)
                            {
                                StockLog.Logger.LOG.WriteLog("Error", "[BatchMinuteStockData]: Database Connection Fail. Batch Stop.");
                                isBatchStarted = false;
                                return;
                            }
                            mDBFactory_dev.Execute(connection, "delete from " + tableName + " where itm_c='" + minInforCurrentCode + "'");
                            connection.Close();
                        }

                        StockLog.Logger.LOG.WriteLog("APIOperation", minInforCurrentCode + ": Stock Min Data Request Start");

                        minInforContinue = true;
                        minInfoStopEvent.Reset();

                        StockLog.Logger.LOG.WriteLog("APIOperation", minInforCurrentCode + ": Stock Min Data Request First");
                        int error = RequestMinInformation(minInforCurrentCode, 0);
                        KiwoomErrorCatch(error);

                        Thread.Sleep(sleepTime);

                        while (minInfoStopEvent.WaitOne())
                        {
                            if (minInforContinue)
                            {
                                minInfoStopEvent.Reset();
                                StockLog.Logger.LOG.WriteLog("APIOperation", minInforCurrentCode + ": Stock Min Data Request Next");
                                error = RequestMinInformation(minInforCurrentCode, 2);
                                KiwoomErrorCatch(error);

                                Thread.Sleep(sleepTime);
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (loopStop)
                        {
                            loopStop = false;
                            break;
                        }
                    }

                    connection = mDBFactory_dev.Connect();
                    if (connection == null)
                    {
                        StockLog.Logger.LOG.WriteLog("Error", "[BatchMinuteStockData]: Database Connection Fail. Batch Stop.");
                        isBatchStarted = false;
                        return;
                    }
                    mDBFactory_dev.Execute(connection, "insert into tb_stock_price_min select a.wk_dt, a.wk_tm, a.itm_c, a.volume, a.price, a.start_am, a.high_am, a.low_am from tb_stock_price_min_sub a left outer join tb_stock_price_min b on a.WK_DT = b.WK_DT and a.WK_TM = b.WK_TM and a.ITM_C = b.ITM_C and b.wk_dt=(select max(wk_dt) from tb_stock_price_min_sub) where 1 = 1 and b.ITM_C is null and a.wk_dt=(select max(wk_dt) from tb_stock_price_min_sub)");
                    mDBFactory_dev.Execute(connection, "truncate tb_stock_price_rt_min");
                    connection.Clone();

                    StockLog.Logger.LOG.WriteLog("Console", "GetMinuteStockData Complete!!");
                    StockLog.Logger.LOG.WriteLog("APIOperation", "minInfoCurrentCode Init!!");
                    StockData.Singleton.Store.WorkInfo.MinStarted = false;
                    StockData.Singleton.Store.WorkInfo.MinCurrentCode = "";
                    StockData.Singleton.Store.SaveSetting();

                    StockData.Singleton.Store.QueryLogSend("Batch", "Complate GetMinuteStockData.");
                }
                catch (Exception except)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                    if (connection != null)
                    {
                        connection.Close();
                    }
                    RestartEvent?.Invoke();
                }
                isBatchStarted = false;
            });
            return true;
        }

        private bool BatchTickStockData(string[] args)
        {
            if (isBatchStarted)
            {
                StockLog.Logger.LOG.WriteLog("Console", "Batch Already Started, Cancel: BatchTickStockData");
                return true;
            }

            isBatchStarted = true;


            StockLog.Logger.LOG.WriteLog("APIOperation", "BatchTickStockData Start");
            if (StockData.Singleton.Store.WorkInfo.TickCurrentCode == "")
            {
                StockData.Singleton.Store.QueryLogSend("Batch", "Execute BatchTickStockData.");
            }

            StockData.Singleton.Store.WorkInfo.TickStarted = true;
            StockData.Singleton.Store.SaveSetting();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                MySqlConnection connection = null;
                string tableName = "tb_stock_price_tick_sub";
                bool firstReq = true;

                List<string> completeList = new List<string>();

                try
                {
                    bool cflag = true;
                    int sleepTime = 600;
                    List<string> stockList = GetBasicStockList();

                    StockLog.Logger.LOG.WriteLog("APIOperation", "BatchTickStockData GetBasicStockList Count: " + stockList.Count);

                    for (int i = 0; i < stockList.Count; i++)
                    {
                        tickInforCurrentCode = stockList[i];

                        if (cflag)
                        {
                            if (tickHistoryContinue && !StockData.Singleton.Store.WorkInfo.TickHistoryEnd.Contains(stockList[i]))
                            {
                                cflag = false;
                            }
                            if (!tickHistoryContinue && (stockList[i] == StockData.Singleton.Store.WorkInfo.TickCurrentCode || StockData.Singleton.Store.WorkInfo.TickCurrentCode == ""))
                            {
                                cflag = false;
                            }

                            if (cflag)
                            {
                                continue;
                            }
                        }

                        if (completeList.Contains(tickInforCurrentCode))
                        {
                            StockLog.Logger.LOG.WriteLog("Test", "Exist Code" + tickInforCurrentCode);
                            continue;
                        }

                        completeList.Add(tickInforCurrentCode);

                        StockData.Singleton.Store.WorkInfo.TickCurrentCode = tickInforCurrentCode;
                        StockData.Singleton.Store.SaveSetting();



                        if (i == 0)
                        {
                            connection = mDBFactory_dev.Connect();
                            if (connection == null)
                            {
                                StockLog.Logger.LOG.WriteLog("Error", "[BatchTickStockData]: Database Connection Fail. Batch Stop.");
                                isBatchStarted = false;
                                return;
                            }
                            mDBFactory_dev.Execute(connection, "truncate table " + tableName);
                            connection.Close();
                        }
                        else if (firstReq)
                        {
                            firstReq = false;
                            connection = mDBFactory_dev.Connect();
                            if (connection == null)
                            {
                                StockLog.Logger.LOG.WriteLog("Error", "[BatchTickStockData]: Database Connection Fail. Batch Stop.");
                                isBatchStarted = false;
                                return;
                            }
                            mDBFactory_dev.Execute(connection, "delete from " + tableName + " where itm_c='" + tickInforCurrentCode + "'");
                            connection.Close();
                        }
                        

                        StockLog.Logger.LOG.WriteLog("APIOperation", tickInforCurrentCode + ": Stock Tick Data Request Start");

                        tickInforContinue = true;
                        tickInfoStopEvent.Reset();

                        StockLog.Logger.LOG.WriteLog("APIOperation", tickInforCurrentCode + ": Stock Tick Data Request First");
                        int error = RequestTickInformation(tickInforCurrentCode, 0);
                        KiwoomErrorCatch(error);

                        Thread.Sleep(sleepTime);

                        while (tickInfoStopEvent.WaitOne())
                        {
                            if (tickInforContinue)
                            {
                                tickInfoStopEvent.Reset();
                                StockLog.Logger.LOG.WriteLog("APIOperation", tickInforCurrentCode + ": Stock Tick Data Request Next");
                                error = RequestTickInformation(tickInforCurrentCode, 2);
                                KiwoomErrorCatch(error);
                                Thread.Sleep(sleepTime);
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (loopStop)
                        {
                            loopStop = false;
                            break;
                        }
                    }

                    connection = mDBFactory_dev.Connect();

                    mDBFactory_dev.Execute(connection, @"
insert
	into
	tb_stock_price_tick_batch 
select
	a.wk_dt,
	a.wk_tm,
	a.itm_c,
	a.seq,
	a.volume,
	a.price
from
	tb_stock_price_tick_sub a
left outer join tb_stock_price_tick_batch b on
	a.WK_DT = b.WK_DT
	and a.WK_TM = b.WK_TM
	and a.ITM_C = b.ITM_C
	and a.SEQ = b.SEQ
where
	1 = 1
	and b.ITM_C is null;
", 6000);
                    mDBFactory_dev.Execute(connection, "truncate table tb_stock_price_tick_sub");

                    connection.Clone();

                    StockLog.Logger.LOG.WriteLog("Console", "GetTickStockData Complete!!");
                    StockLog.Logger.LOG.WriteLog("APIOperation", "tickInfoCurrentCode Init!!");
                    StockData.Singleton.Store.WorkInfo.TickStarted = false;
                    StockData.Singleton.Store.WorkInfo.TickCurrentCode = "";
                    StockData.Singleton.Store.SaveSetting();
                    tickInforCurrentCode = "";

                    StockData.Singleton.Store.QueryLogSend("Batch", "Complete BatchTickStockData.");

                }
                catch (Exception except)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                    if (connection != null)
                    {
                        connection.Close();
                    }
                    RestartEvent?.Invoke();
                }

                isBatchStarted = false;
            });
            return true;
        }

        private bool BatchAskBidStockData(string[] args)
        {
            if (isBatchStarted)
            {
                StockLog.Logger.LOG.WriteLog("Console", "Batch Already Started, Cancel: BatchAskBidStockData");
                return true;
            }

            isBatchStarted = true;


            StockLog.Logger.LOG.WriteLog("APIOperation", "BatchAskBidStockData Start");
            if (StockData.Singleton.Store.WorkInfo.AskBidCurrentCode == "")
            {
                StockData.Singleton.Store.QueryLogSend("Batch", "Execute BatchAskBidStockData.");
            }

            StockData.Singleton.Store.WorkInfo.AskBidStarted = true;
            StockData.Singleton.Store.SaveSetting();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                MySqlConnection connection = null;
                string tableName = "tb_stock_price_ask_bid_sub";
                bool firstReq = true;
                try
                {
                    bool cflag = true;
                    int sleepTime = 600;
                    List<string> stockList = GetBasicStockList();

                    StockLog.Logger.LOG.WriteLog("APIOperation", "BatchAskBidStockData GetBasicStockList Count: " + stockList.Count);

                    for (int i = 0; i < stockList.Count; i++)
                    {
                        askbidInforCurrentCode = stockList[i];

                        if (cflag)
                        {
                            if (askbidInforContinue && !StockData.Singleton.Store.WorkInfo.AskBidHistoryEnd.Contains(stockList[i]))
                            {
                                cflag = false;
                            }
                            if (!askbidInforContinue && (stockList[i] == StockData.Singleton.Store.WorkInfo.AskBidCurrentCode || StockData.Singleton.Store.WorkInfo.AskBidCurrentCode == ""))
                            {
                                cflag = false;
                            }

                            if (cflag)
                            {
                                continue;
                            }
                        }

                        if (i == 0)
                        {
                            connection = mDBFactory_dev.Connect();
                            mDBFactory_dev.Execute(connection, "truncate table " + tableName);
                            connection.Close();
                        }
                        else if (firstReq)
                        {
                            firstReq = false;
                            connection = mDBFactory_dev.Connect();
                            mDBFactory_dev.Execute(connection, "delete from " + tableName + " where stock_code='" + askbidInforCurrentCode + "'");
                            connection.Close();
                        }

                        StockLog.Logger.LOG.WriteLog("APIOperation", askbidInforCurrentCode + ": Stock Tick Data Request Start");

                        if (this.InvokeRequired)
                        {
                            askbidInforContinue = true;
                            askbidInfoStopEvent.Reset();

                            this.Invoke(new MethodInvoker(delegate ()
                            {
                                StockLog.Logger.LOG.WriteLog("APIOperation", askbidInforCurrentCode + ": Stock Tick Data Request First");
                                int error = RequestAskBidInformation(askbidInforCurrentCode, 0);
                                KiwoomErrorCatch(error);
                            }));

                            Thread.Sleep(sleepTime);

                            while (askbidInfoStopEvent.WaitOne())
                            {
                                if (askbidInforContinue)
                                {
                                    askbidInfoStopEvent.Reset();
                                    StockLog.Logger.LOG.WriteLog("APIOperation", askbidInforCurrentCode + ": Stock Tick Data Request Next");
                                    this.Invoke(new MethodInvoker(delegate ()
                                    {
                                        int error = RequestAskBidInformation(askbidInforCurrentCode, 2);
                                        KiwoomErrorCatch(error);
                                    }));
                                    Thread.Sleep(sleepTime);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        else
                        {
                            throw new Exception("InvokeRequired Error");
                        }

                        if (loopStop)
                        {
                            loopStop = false;
                            break;
                        }
                    }

                    connection = mDBFactory_dev.Connect();

                    mDBFactory_dev.Execute(connection, "insert into tb_stock_price_askbid select a.wk_dt, a.wk_tm, a.stock_code, a.seq, a.top_ask, a.top_ask_qty, a.top_bid, a.top_bid_qty, a.ask1, a.ask1_qty, a.ask1_qty_ratio, a.bid1, a.bid1_qty, a.bid1_qty_ratio, a.ask2, a.ask2_qty, a.ask2_qty_ratio, a.bid2, a.bid2_qty, a.bid2_qty_ratio, a.ask3, a.ask3_qty, a.ask3_qty_ratio, a.bid3, a.bid3_qty, a.bid3_qty_ratio, a.ask4, a.ask4_qty, a.ask4_qty_ratio, a.bid4, a.bid4_qty, a.bid4_qty_ratio, a.ask5, a.ask5_qty, a.ask5_qty_ratio, a.bid5, a.bid5_qty, a.bid5_qty_ratio, a.ask6, a.ask6_qty, a.ask6_qty_ratio, a.bid6, a.bid6_qty, a.bid6_qty_ratio, a.ask7, a.ask7_qty, a.ask7_qty_ratio, a.bid7, a.bid7_qty, a.bid7_qty_ratio, a.ask8, a.ask8_qty, a.ask8_qty_ratop, a.bid8, a.bid8_qty, a.bid8_qty_ratio, a.ask9, a.ask9_qty, a.ask9_qty_ratio, a.bid9, a.bid9_qty, a.bid9_qty_ratio, a.ask10, a.ask10_qty, a.ask10_qty_ratio, a.bid10, a.bid10_qty, a.bid10_qty_ratio, a.total_ask_qty, a.total_ask_qty_ratio, a.total_bid_qty, a.total_bid_qty_ratio from tb_stock_price_askbid_sub a left outer join tb_stock_price_askbid b on a.wk_dt = b.wk_dt and a.wk_tm = b.wk_tm and a.stock_code = b.stock_code and a.seq = b.seq where 1 = 1 and b.stock_code is null");

                    connection.Clone();

                    StockLog.Logger.LOG.WriteLog("Console", "GetAskBidStockData Complete!!");
                    StockLog.Logger.LOG.WriteLog("APIOperation", "BatchAskBidStockData Init!!");
                    StockData.Singleton.Store.WorkInfo.AskBidStarted = false;
                    StockData.Singleton.Store.WorkInfo.AskBidCurrentCode = "";
                    StockData.Singleton.Store.SaveSetting();
                    askbidInforCurrentCode = "";

                    StockData.Singleton.Store.QueryLogSend("Batch", "Complete BatchAskBidStockData.");

                }
                catch (Exception except)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                    if (connection != null)
                    {
                        connection.Close();
                    }
                    RestartEvent?.Invoke();
                }

                isBatchStarted = false;
            });
            return true;
        }

        private bool BatchIndexData(string[] args)
        {

            StockLog.Logger.LOG.WriteLog("APIOperation", "BatchIndexData Start");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                //업종코드 = 001:종합(KOSPI), 002:대형주, 003:중형주, 004:소형주 101:종합(KOSDAQ), 201:KOSPI200, 302:KOSTAR, 701: KRX100 나머지 ※ 업종코드 참고
                List<string> industryCode = new List<string>();
                industryCode.Add("001");
                industryCode.Add("002");
                industryCode.Add("003");
                industryCode.Add("004");
                industryCode.Add("101");
                industryCode.Add("201");
                industryCode.Add("302");
                industryCode.Add("701");

                for (int i = 0; i < industryCode.Count; i++)
                {
                    StockLog.Logger.LOG.WriteLog("Test", "Request oPT20003: " + industryCode[i]);
                    axKHOpenAPI1.SetInputValue("업종코드", industryCode[i]);
                    axKHOpenAPI1.CommRqData("전업종지수요청" + industryCode[i], "OPT20003", 0, StockData.Singleton.Store.GetScreenNumber());

                    Thread.Sleep(500);
                }
            });
            return true;
        }

        private bool BatchStockBasic(string[] args)
        {
            if (isBatchStarted)
            {
                StockLog.Logger.LOG.WriteLog("Console", "Batch Already Started, Cancel: BatchStockBasic");
                return true;
            }

            isBatchStarted = true;

            StockLog.Logger.LOG.WriteLog("APIOperation", "GetStockBasic Start");
            if (StockData.Singleton.Store.WorkInfo.BasicCurrentCode == "")
            {
                StockData.Singleton.Store.QueryLogSend("Batch", "Execute GetStockBasic.");
            }

            StockData.Singleton.Store.WorkInfo.BasicStarted = true;
            StockData.Singleton.Store.SaveSetting();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    bool cflag = true;
                    int sleepTime = 600;
                    List<string> stockList = GetBasicStockList();

                    StockLog.Logger.LOG.WriteLog("APIOperation", "GetStockBasic GetBasicStockList Count: " + stockList.Count);

                    for (int i = 0; i < stockList.Count; i++)
                    {
                        if (cflag)
                        {
                            if (stockList[i] == StockData.Singleton.Store.WorkInfo.BasicCurrentCode || StockData.Singleton.Store.WorkInfo.BasicCurrentCode == "")
                            {
                                cflag = false;
                            }

                            if (cflag)
                            {
                                continue;
                            }
                        }

                        if (stockList[i] == "")
                        {
                            continue;
                        }

                        basicInfoStopEvent.Reset();

                        StockLog.Logger.LOG.WriteLog("APIOperation", stockList[i] + ": GetStockBasic Data Request First");
                        int error = RequestStockBasicInformation(stockList[i]);
                        KiwoomErrorCatch(error);

                        Thread.Sleep(sleepTime);

                        if (!basicInfoStopEvent.WaitOne(5000))
                        {
                            StockLog.Logger.LOG.WriteLog("Error", stockList[i] + ": basicInfoStopEvent Timeout");
                        }
                    }

                    StockLog.Logger.LOG.WriteLog("Console", "GetStockBasic Complete!!");
                    StockLog.Logger.LOG.WriteLog("APIOperation", "GetStockBasic Init!!");
                    StockData.Singleton.Store.WorkInfo.BasicStarted = false;
                    StockData.Singleton.Store.WorkInfo.BasicCurrentCode = "";
                    StockData.Singleton.Store.SaveSetting();

                    StockData.Singleton.Store.QueryLogSend("Batch", "Complate GetStockBasic.");
                }
                catch (Exception except)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());

                    RestartEvent?.Invoke();
                }
                isBatchStarted = false;
            });

            return true;
        }

        private void SetDailyStockData(object param)
        {

            MySqlConnection connection = null;

            try
            {

                List<object> paramList = (List<object>)param;
                List<StockData.DataStruct.DailyData> dayList = (List<StockData.DataStruct.DailyData>)paramList[0];
                string sPrevNext = (string)paramList[1];
                string tableName = "tb_stock_price_day_sub";
                TimeChecker(() =>
                {
                    if (dayList.Count > 0)
                    {
                        // only log
                        StockLog.Logger.LOG.WriteLog("Console", "[일별데이터조회] Code: " + dayInforCurrentCode + ", Date: " + dayList[0].WK_DT);


                        connection = mDBFactory.Connect();

                        if (connection == null)
                        {
                            StockLog.Logger.LOG.WriteLog("Error", "DB Connection Fail: [일별데이터조회]");
                            return;
                        }

                        List<string> existList = new List<string>();

                        string sql = "select itm_c,wk_dt from " + tableName + " where itm_c='" + dayInforCurrentCode + "' and wk_dt between '" + dayList[dayList.Count - 1].WK_DT + "' and '" + dayList[0].WK_DT + "'";
                        MySqlDataReader reader = mDBFactory.ExecuteReader(connection, sql);

                        string code = dayInforCurrentCode;
                        while (reader.Read())
                        {
                            if (code != reader.GetString(0))
                            {
                                code = reader.GetString(0);
                            }
                            existList.Add(reader.GetString(1));
                        }
                        reader.Close();

                        List<StockData.DataStruct.DailyData> databuf = new List<StockData.DataStruct.DailyData>();

                        if (StockData.Singleton.Store.Account_Privileges.day_update_override == "0")
                        {
                            foreach (StockData.DataStruct.DailyData data in dayList)
                            {
                                if (data.ITM_C == code && !existList.Contains(data.WK_DT))
                                {
                                    databuf.Add(data);
                                }

                                if (databuf.Count > 1000)
                                {
                                    sql = StockData.Singleton.Store.GetInsertMultiQueryDay(tableName, databuf);
                                    mDBFactory.Execute(connection, sql);
                                    databuf = new List<StockData.DataStruct.DailyData>();
                                }
                            }

                            if (databuf.Count > 0)
                            {
                                sql = StockData.Singleton.Store.GetInsertMultiQueryDay(tableName, databuf);
                                mDBFactory.Execute(connection, sql);
                                databuf = new List<StockData.DataStruct.DailyData>();
                            }
                        }
                        else
                        {
                            mDBFactory.Execute(connection, "delete from " + tableName + " where ITM_C='" + dayInforCurrentCode + "' and wk_dt >= '" + dayList[dayList.Count - 1].WK_DT + "' and wk_dt <='" + dayList[0].WK_DT + "'");
                            databuf = new List<StockData.DataStruct.DailyData>();

                            foreach (StockData.DataStruct.DailyData data in dayList)
                            {
                                //dayDataList.AddRange(dayList);
                                databuf.Add(data);

                                if (databuf.Count > 1000)
                                {
                                    sql = StockData.Singleton.Store.GetInsertMultiQueryDay(tableName, databuf);
                                    mDBFactory.Execute(connection, sql);
                                    databuf = new List<StockData.DataStruct.DailyData>();
                                }
                            }
                            if (databuf.Count > 0)
                            {
                                sql = StockData.Singleton.Store.GetInsertMultiQueryDay(tableName, databuf);
                                mDBFactory.Execute(connection, sql);
                                databuf = new List<StockData.DataStruct.DailyData>();
                            }

                            //foreach (StockData.DataStruct.DailyuteData data in DailyList)
                            //{
                            //    sql = StockData.Singleton.Store.GetInsertUpdateQuery("tb_stock_price_Daily", StockData.Singleton.Store.GetMemberValue<StockData.DataStruct.DailyuteData>(data));
                            //    mDBFactory.Execute(connection, sql);
                            //}
                        }

                        if (StockData.Singleton.Store.Account_Privileges.update_check_day2 == "0")
                        {
                            if (StockData.Singleton.Store.WorkInfo.DayHistoryEnd.Contains(dayInforCurrentCode))
                            {
                                StockLog.Logger.LOG.WriteLog("APIOperation", dayInforCurrentCode + ": Data End");
                                dayInforContinue = false;
                                StockData.Singleton.Store.WorkInfo.DayCurrentCode = dayInforCurrentCode;
                                StockData.Singleton.Store.SaveSetting();
                            }
                        }
                        else
                        {
                            if (int.TryParse(StockData.Singleton.Store.Account_Privileges.update_check_day2, out int check_day))
                            {
                                if (DateTime.ParseExact(dayList[dayList.Count - 1].WK_DT, "yyyyMMdd", null) < DateTime.Now.AddDays(check_day * -1))
                                {
                                    StockLog.Logger.LOG.WriteLog("APIOperation", dayInforCurrentCode + ": Data End");
                                    dayInforContinue = false;
                                    StockData.Singleton.Store.WorkInfo.DayCurrentCode = dayInforCurrentCode;
                                    StockData.Singleton.Store.SaveSetting();
                                }
                            }
                        }

                        connection.Close();

                    }
                    if (sPrevNext != "2")
                    {
                        StockLog.Logger.LOG.WriteLog("APIOperation", dayInforCurrentCode + ": Data End");
                        dayInforContinue = false;

                        StockData.Singleton.Store.WorkInfo.DayHistoryEnd.Add(dayInforCurrentCode);
                        StockData.Singleton.Store.WorkInfo.DayCurrentCode = dayInforCurrentCode;
                        StockData.Singleton.Store.SaveSetting();
                    }

                    StockLog.Logger.LOG.WriteLog("APIOperation", dayInforCurrentCode + ": Event Set");
                    dayInfoStopEvent.Set();

                }, "일별데이터조회");
            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                if (connection != null)
                {
                    connection.Close();
                }
                RestartEvent?.Invoke();

            }
        }

        private void SetMinuteStockData(object param)
        {

            MySqlConnection connection = null;

            try
            {
                List<object> paramList = (List<object>)param;
                List<StockData.DataStruct.MinuteData> minList = (List<StockData.DataStruct.MinuteData>)paramList[0];
                string sPrevNext = (string)paramList[1];
                string tableName = "tb_stock_price_min_sub";
                TimeChecker(() =>
                {
                    if (minList.Count > 0)
                    {
                        // only log
                        StockLog.Logger.LOG.WriteLog("Console", "[주식분봉차트조회요청] Code: " + minInforCurrentCode + ", Date: " + minList[0].WK_DT);

                        //DateTime recentdt = DateTime.ParseExact(minList[0].WK_DT, "yyyyMMdd", null);
                        //DateTime dateTimeFilter = DateTime.ParseExact(DateTime.Now.AddYears(-6).ToString("yyyyMMdd"), "yyyyMMdd", null);

                        connection = mDBFactory_dev.Connect();

                        if (connection == null)
                        {
                            StockLog.Logger.LOG.WriteLog("Error", "DB Connection Fail: [주식분봉차트조회요청]");
                            return;
                        }

                        //DateTime dtm = DateTime.ParseExact(minList[0].WK_DTM, "yyyy-MM-dd HH:mm:ss", null);

                        //if (dtm < DateTime.Now.AddYears(-2))
                        //{
                        //    StockLog.Logger.LOG.WriteLog("Console", minInfoCurrentCode + ": history check Data End");
                        //    minInfoContinue = false;
                        //    StockData.Singleton.Store.WorkInfo.MinHistoryEnd.Add(minInfoCurrentCode);
                        //    StockData.Singleton.Store.WorkInfo.MinCurrentCode = minInfoCurrentCode;
                        //    StockData.Singleton.Store.SaveSetting();
                        //    minInfoStopEvent.Set();
                        //    return;
                        //}
                        //else {
                        //    StockLog.Logger.LOG.WriteLog("APIOperation", "Day Check Ok Continue");
                        //}

                        //List<DateTime> existList = new List<DateTime>();

                        string sql = "";
                        //string sql = "select itm_c,wk_dt,wk_tm from " + tableName + " where itm_c='" + minInforCurrentCode + "' " +
                        //"and wk_dt between '" + minList[minList.Count - 1].WK_DT + "' and '" + minList[0].WK_DT + "' " +
                        //"and wk_tm between '" + minList[minList.Count - 1].WK_TM + "' and '" + minList[0].WK_TM + "'";

                        //MySqlDataReader reader = mDBFactory.ExecuteReader(connection, sql);

                        string code = minInforCurrentCode;
                        //while (reader.Read())
                        //{
                        //    if (code != reader.GetString(0))
                        //    {
                        //        code = reader.GetString(0);
                        //    }
                        //    existList.Add(DateTime.ParseExact(reader.GetDateTime(1).ToString("yyyy-MM-dd") + " " + reader.GetString(2), "yyyy-MM-dd HHmmss", null));

                        //}
                        //reader.Close();

                        List<StockData.DataStruct.MinuteData> databuf = new List<StockData.DataStruct.MinuteData>();

                        foreach (StockData.DataStruct.MinuteData data in minList)
                        {
                            databuf.Add(data);

                            if (databuf.Count > 1000)
                            {
                                sql = StockData.Singleton.Store.GetInsertMultiQueryMin(tableName, databuf);
                                mDBFactory.Execute(connection, sql);
                                databuf = new List<StockData.DataStruct.MinuteData>();
                            }
                        }

                        if (databuf.Count > 0)
                        {
                            sql = StockData.Singleton.Store.GetInsertMultiQueryMin(tableName, databuf);
                            mDBFactory.Execute(connection, sql);
                            databuf = new List<StockData.DataStruct.MinuteData>();
                        }


                        if (StockData.Singleton.Store.Account_Privileges.update_check_day2 == "0")
                        {
                            if (StockData.Singleton.Store.WorkInfo.MinHistoryEnd.Contains(minInforCurrentCode))
                            {
                                StockLog.Logger.LOG.WriteLog("APIOperation", minInforCurrentCode + ": Data End");
                                minInforContinue = false;
                                StockData.Singleton.Store.WorkInfo.MinCurrentCode = minInforCurrentCode;
                                StockData.Singleton.Store.SaveSetting();
                            }
                        }
                        else
                        {
                            if (int.TryParse(StockData.Singleton.Store.Account_Privileges.update_check_day2, out int check_day))
                            {
                                if (DateTime.ParseExact(minList[minList.Count - 1].WK_DT + " " + minList[minList.Count - 1].WK_TM, "yyyy-MM-dd HHmmss", null) < DateTime.Now.AddDays(check_day * -1))
                                {
                                    StockLog.Logger.LOG.WriteLog("APIOperation", minInforCurrentCode + ": Data End");
                                    minInforContinue = false;
                                    StockData.Singleton.Store.WorkInfo.MinCurrentCode = minInforCurrentCode;
                                    StockData.Singleton.Store.SaveSetting();
                                }
                            }
                        }

                        connection.Close();
                    }
                    if (sPrevNext != "2")
                    {
                        StockLog.Logger.LOG.WriteLog("APIOperation", minInforCurrentCode + ": Data End");
                        StockLog.Logger.LOG.WriteLog("Console", minInforCurrentCode + ": Data no more");
                        minInforContinue = false;

                        StockData.Singleton.Store.WorkInfo.MinHistoryEnd.Add(minInforCurrentCode);
                        StockData.Singleton.Store.WorkInfo.MinCurrentCode = minInforCurrentCode;
                        StockData.Singleton.Store.SaveSetting();
                    }


                    StockLog.Logger.LOG.WriteLog("APIOperation", minInforCurrentCode + ": Event Set");
                    minInfoStopEvent.Set();

                }, "주식분봉차트조회요청");
            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                if (connection != null)
                {
                    connection.Close();
                }
                RestartEvent?.Invoke();

            }
        }

        private void SetTickStockData(object param)
        {
            MySqlConnection connection = null;

            try
            {
                List<object> paramList = (List<object>)param;
                List<StockData.DataStruct.TickData> tickList = (List<StockData.DataStruct.TickData>)paramList[0];
                string sPrevNext = (string)paramList[1];
                string tableName = "tb_stock_price_tick_sub";

                StockLog.Logger.LOG.WriteLog("APIOperation", "Data Count: " + tickList.Count);

                if (tickList.Count > 0)
                {
                    // only log
                    StockLog.Logger.LOG.WriteLog("Console", "Code: " + tickInforCurrentCode + ", Date: " + tickList[0].WK_DT);

                    connection = mDBFactory_dev.Connect();

                    if (connection == null)
                    {
                        StockLog.Logger.LOG.WriteLog("Error", "DB Connection Fail: [주식틱차트조회요청]");
                        return;
                    }


                    string sql = "";
                    List<StockData.DataStruct.TickData> databuf = new List<StockData.DataStruct.TickData>();

                    foreach (StockData.DataStruct.TickData data in tickList)
                    {
                        if (tick_lastTM != data.WK_TM)
                        {
                            tick_lastTM = data.WK_TM;
                            tick_index = 1;
                        }

                        data.SEQ = tick_index++;

                        databuf.Add(data);

                        if (databuf.Count > 1000)
                        {
                            sql = StockData.Singleton.Store.GetInsertMultiQueryTick(tableName, databuf);
                            mDBFactory.Execute(connection, sql);
                            databuf = new List<StockData.DataStruct.TickData>();
                        }
                    }
                    if (databuf.Count > 0)
                    {
                        sql = StockData.Singleton.Store.GetInsertMultiQueryTick(tableName, databuf);
                        mDBFactory.Execute(connection, sql);
                        databuf = new List<StockData.DataStruct.TickData>();
                    }


                    if (StockData.Singleton.Store.Account_Privileges.update_check_day2 == "0")
                    {
                        if (StockData.Singleton.Store.WorkInfo.TickHistoryEnd.Contains(tickInforCurrentCode))
                        {
                            StockLog.Logger.LOG.WriteLog("APIOperation", tickInforCurrentCode + ": Data End History End");
                            tickInforContinue = false;
                            StockData.Singleton.Store.WorkInfo.TickCurrentCode = tickInforCurrentCode;
                            StockData.Singleton.Store.SaveSetting();
                        }
                    }
                    else
                    {
                        if (int.TryParse(StockData.Singleton.Store.Account_Privileges.update_check_day2, out int check_day))
                        {
                            if (DateTime.ParseExact(tickList[tickList.Count - 1].WK_DT, "yyyy-MM-dd", null) < DateTime.Now.AddDays(check_day * -1))
                            {
                                StockLog.Logger.LOG.WriteLog("APIOperation", tickInforCurrentCode + ": Data End Day Over");
                                tickInforContinue = false;
                                StockData.Singleton.Store.WorkInfo.TickCurrentCode = tickInforCurrentCode;
                                StockData.Singleton.Store.SaveSetting();
                            }
                        }
                    }

                    connection.Close();
                }

                if (sPrevNext != "2")
                {
                    StockLog.Logger.LOG.WriteLog("APIOperation", tickInforCurrentCode + ": Data End");
                    StockLog.Logger.LOG.WriteLog("Console", tickInforCurrentCode + ": Data no more sPrevNext: " + sPrevNext);
                    tickInforContinue = false;

                    StockData.Singleton.Store.WorkInfo.TickHistoryEnd.Add(tickInforCurrentCode);
                    StockData.Singleton.Store.WorkInfo.TickCurrentCode = tickInforCurrentCode;
                    StockData.Singleton.Store.SaveSetting();
                }
                
                StockLog.Logger.LOG.WriteLog("APIOperation", tickInforCurrentCode + ": Event Set");
                tickInfoStopEvent.Set();

            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());

                if (connection != null)
                {
                    connection.Close();
                }
                RestartEvent?.Invoke();

            }
        }

        private void SetAskBidStockData(object param)
        {
            MySqlConnection connection = null;

            try
            {
                List<object> paramList = (List<object>)param;
                List<StockData.DataStruct.AskBidPrice> askbidList = (List<StockData.DataStruct.AskBidPrice>)paramList[0];
                string sPrevNext = (string)paramList[1];
                string tableName = "tb_stock_price_ask_bid_sub";

                StockLog.Logger.LOG.WriteLog("APIOperation", "Data Count: " + askbidList.Count);

                if (askbidList.Count > 0)
                {
                    // only log
                    StockLog.Logger.LOG.WriteLog("Console", "Code: " + askbidInforCurrentCode + ", Date: " + askbidList[0].wk_dt);

                    connection = mDBFactory.Connect();

                    if (connection == null)
                    {
                        StockLog.Logger.LOG.WriteLog("Error", "DB Connection Fail: [주식틱차트조회요청]");
                        return;
                    }

                    string sql = "";
                    List<StockData.DataStruct.AskBidPrice> databuf = new List<StockData.DataStruct.AskBidPrice>();

                    foreach (StockData.DataStruct.AskBidPrice data in askbidList)
                    {
                        if (askbid_lastTM != data.wk_tm)
                        {
                            askbid_lastTM = data.wk_tm;
                            askbid_index = 1;
                        }

                        data.seq = askbid_index++;

                        databuf.Add(data);

                        if (databuf.Count > 1000)
                        {
                            sql = StockData.Singleton.Store.GetInsertMultiQueryAskBid(tableName, databuf);
                            mDBFactory.Execute(connection, sql);
                            databuf = new List<StockData.DataStruct.AskBidPrice>();
                        }
                    }
                    if (databuf.Count > 0)
                    {
                        sql = StockData.Singleton.Store.GetInsertMultiQueryAskBid(tableName, databuf);
                        mDBFactory.Execute(connection, sql);
                        databuf = new List<StockData.DataStruct.AskBidPrice>();
                    }


                    if (StockData.Singleton.Store.Account_Privileges.update_check_day == "0")
                    {
                        if (StockData.Singleton.Store.WorkInfo.AskBidHistoryEnd.Contains(askbidInforCurrentCode))
                        {
                            StockLog.Logger.LOG.WriteLog("APIOperation", askbidInforCurrentCode + ": Data End");
                            askbidInforContinue = false;
                            StockData.Singleton.Store.WorkInfo.AskBidCurrentCode = askbidInforCurrentCode;
                            StockData.Singleton.Store.SaveSetting();
                        }
                    }
                    else
                    {
                        if (int.TryParse(StockData.Singleton.Store.Account_Privileges.update_check_day, out int check_day))
                        {
                            if (DateTime.ParseExact(askbidList[askbidList.Count - 1].wk_dt, "yyyy-MM-dd", null) < DateTime.Now.AddDays(check_day * -1))
                            {
                                StockLog.Logger.LOG.WriteLog("APIOperation", askbidInforCurrentCode + ": Data End");
                                askbidInforContinue = false;
                                StockData.Singleton.Store.WorkInfo.AskBidCurrentCode = askbidInforCurrentCode;
                                StockData.Singleton.Store.SaveSetting();
                            }
                        }
                    }

                    connection.Close();
                }

                if (sPrevNext != "2")
                {
                    StockLog.Logger.LOG.WriteLog("APIOperation", askbidInforCurrentCode + ": Data End");
                    StockLog.Logger.LOG.WriteLog("Console", askbidInforCurrentCode + ": Data no more sPrevNext: " + sPrevNext);
                    askbidInforContinue = false;

                    StockData.Singleton.Store.WorkInfo.AskBidHistoryEnd.Add(askbidInforCurrentCode);
                    StockData.Singleton.Store.WorkInfo.AskBidCurrentCode = askbidInforCurrentCode;
                    StockData.Singleton.Store.SaveSetting();
                }

                StockLog.Logger.LOG.WriteLog("APIOperation", askbidInforCurrentCode + ": Event Set");
                askbidInfoStopEvent.Set();

            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());

                if (connection != null)
                {
                    connection.Close();
                }
                RestartEvent?.Invoke();

            }
        }

        private int RequestAskBidInformation(string code, int nextIdx)
        {
            int result = 0;

            APIExecute(() =>
            {
                axKHOpenAPI1.SetInputValue("종목코드", code);

                result = axKHOpenAPI1.CommRqData("주가호가요청", "opt10004", nextIdx, StockData.Singleton.Store.GetScreenNumber());
                return true;
            }, "주가호가요청");

            return result;
        }

        private int RequestTickInformation(string code, int nextIdx)
        {
            int result = 0;

            APIExecute(() =>
            {
                axKHOpenAPI1.SetInputValue("종목코드", code);

                axKHOpenAPI1.SetInputValue("틱범위", "1:1틱");

                //수정주가구분 = 0 or 1, 수신데이터 1:유상증자, 2:무상증자, 4:배당락, 8:액면분할, 16:액면병합, 32:기업합병, 64:감자, 256:권리락
                //표시구분 = 0:수량, 1:금액(백만원)
                axKHOpenAPI1.SetInputValue("수정주가구분", "0");

                result = axKHOpenAPI1.CommRqData("주식틱차트조회요청", "opt10079", nextIdx, StockData.Singleton.Store.GetScreenNumber());
                return true;
            }, "주식틱차트조회요청");

            return result;
        }

        private int RequestDayInformation(string code, int nextIdx)
        {
            int result = 0;

            APIExecute(() =>
            {
                axKHOpenAPI1.SetInputValue("종목코드", code);

                //기준일자 = YYYYMMDD(20160101 연도4자리, 월 2자리, 일 2자리 형식)
                axKHOpenAPI1.SetInputValue("기준일자", DateTime.Now.ToString("yyyyMMdd"));

                //수정주가구분 = 0 or 1, 수신데이터 1:유상증자, 2:무상증자, 4:배당락, 8:액면분할, 16:액면병합, 32:기업합병, 64:감자, 256:권리락
                //표시구분 = 0:수량, 1:금액(백만원)
                axKHOpenAPI1.SetInputValue("표시구분", "1");

                result = axKHOpenAPI1.CommRqData("일별데이터조회", "opt10086", nextIdx, StockData.Singleton.Store.GetScreenNumber());
                return true;
            }, "일별데이터조회");

            return result;
        }

        private int RequestMinInformation(string code, int nextIdx)
        {
            int result = 0;
            APIExecute(() =>
            {
                axKHOpenAPI1.SetInputValue("종목코드", code);
                axKHOpenAPI1.SetInputValue("틱범위", "1:1분");
                //수정주가구분 = 0 or 1, 수신데이터 1:유상증자, 2:무상증자, 4:배당락, 8:액면분할, 16:액면병합, 32:기업합병, 64:감자, 256:권리락
                axKHOpenAPI1.SetInputValue("수정주가구분", "0");
                result = axKHOpenAPI1.CommRqData("주식분봉차트조회요청", "opt10080", nextIdx, StockData.Singleton.Store.GetScreenNumber());

                return true;
            }, "주식분봉차트조회요청");

            return result;
        }

        private int RequestStockBasicInformation(string code)
        {
            int result = 0;
            APIExecute(() =>
            {
                axKHOpenAPI1.SetInputValue("종목코드", code);
                result = axKHOpenAPI1.CommRqData("주식기본정보요청", "opt10001", 0, StockData.Singleton.Store.GetScreenNumber());

                return true;
            }, "주식기본정보요청");

            return result;
        }

        public void SetInitRealData()
        {

            MySqlConnection connection = null;

            try
            {
                StockLog.Logger.LOG.WriteLog("Console", "SetInitRealData");

                connection = mDBFactory.Connect();
                DateTime dt = DateTime.Now;
                if (connection == null)
                {
                    StockLog.Logger.LOG.WriteLog("Error", "DB Connection Fail: SetInitRealData");
                    return;
                }
                //foreach (string key in StockData.Singleton.Store.StockRealTimeData.Keys)
                //{
                //    StockLog.Logger.LOG.WriteLog("Console", "SetInitValue: " + key);
                //    DataTable dataTable = mdb.Execute("SELECT * FROM tb_stock_price_rt WHERE itm_c='" + key + "' ORDER BY wk_dt desc, wk_tm DESC LIMIT 1");


                //    for (int i = 0; i < dataTable.Rows.Count; i++)
                //    {
                //        StockData.Singleton.Store.GetDataTableRT(dataTable.Rows[i], StockData.Singleton.Store.StockRealTimeData[key]);
                //    }
                //}
                DataTable dataTable = mDBFactory.ExecuteDataTable(connection, @"
with b as
(
  SELECT a.ITM_C, a.WK_DT, MAX(a.WK_TM) as WK_TM
  FROM tb_stock_price_rt_min a
  join
  (
    SELECT ITM_C, MAX(WK_DT) as WK_DT
    FROM tb_stock_price_rt_min
    GROUP BY ITM_C
  ) b
  on a.ITM_C=b.ITM_C and a.WK_DT=b.WK_DT
  GROUP BY a.ITM_C, a.WK_DT
  )
select *
from tb_stock_price_rt_min a
join b
on a.ITM_C=b.ITM_C
  and a.WK_DT=b.WK_DT
  and a.WK_TM=b.WK_TM;
");

                for (int i = 0; i < dataTable.Rows.Count; i++)
                {
                    if (StockData.Singleton.Store.StockRealTimeData.TryGetValue(dataTable.Rows[i]["ITM_C"] + "", out var value))
                    {
                        StockData.Singleton.Store.GetDataTableRT_Min(dataTable.Rows[i], value);
                    }
                    else
                    {
                        StockData.DataStruct.RealTimeData_v2 rtd = new StockData.DataStruct.RealTimeData_v2();
                        rtd.ITM_C = dataTable.Rows[i]["ITM_C"] + "";
                        StockData.Singleton.Store.GetDataTableRT_Min(dataTable.Rows[i], rtd);
                        StockData.Singleton.Store.StockRealTimeData[rtd.ITM_C] = rtd;
                    }

                }
                connection.Close();

            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());

                if (connection != null)
                {
                    connection.Close();
                }
            }
        }

        private void SettingPrivileges()
        {

            MySqlConnection connection = null;
            try
            {
                connection = mDBFactory.Connect();
                if (connection == null)
                {
                    StockLog.Logger.LOG.WriteLog("Error", "[SettingPrivileges] Database Conntion Fail");
                    return;
                }
                DataTable dataTable = mDBFactory.ExecuteDataTable(connection, "SELECT * FROM tb_account_privileges WHERE acc_name='" + StockData.Singleton.Store.Account.ID + "'");

                if (dataTable.Rows.Count > 0)
                {
                    List<StockData.DataStruct.Privileges> dataList = new List<StockData.DataStruct.Privileges>();

                    for (int i = 0; i < dataTable.Rows.Count; i++)
                    {
                        StockData.DataStruct.Privileges pv = new StockData.DataStruct.Privileges();
                        StockData.Singleton.Store.GetDataTable<StockData.DataStruct.Privileges>(dataTable.Rows[i], pv);

                        //if (pv.acc_name == StockData.Singleton.Store.Account.ID)
                        //{
                        StockData.Singleton.Store.Account_Privileges = pv;

                        if (StockData.Singleton.Store.Account.Account.Contains(pv.acc_main))
                        {
                            StockData.Singleton.Store.Account.MainAccount = pv.acc_main;

                            //dev Connector
                            if (pv.dev_url + "" != "" && pv.dev_port + "" != "")
                            {
                                mDBFactory_dev = new DBManager.MariaDBFactory(pv.dev_url, pv.dev_port, StockData.Singleton.Store.AccessInfo.User, StockData.Singleton.Store.AccessInfo.Password, StockData.Singleton.Store.AccessInfo.DBName);
                            }
                        }
                        else
                        {
                            StockLog.Logger.LOG.WriteLog("Error", "StockData.Singleton.Store.Account.Account Not match Acc_Main");
                            //StockData.Singleton.Store.Account.MainAccount = StockData.Singleton.Store.Account.Account[0];
                        }

                        if (StockData.Singleton.Store.Account_Privileges.rt_interval == 0) { 
                            StockLog.Logger.LOG.WriteLog("Console", "Privilige rt interval is 50, because that value is zero");
                            StockData.Singleton.Store.Account_Privileges.rt_interval = 50;
                        }
                        StockLog.Logger.LOG.WriteLog("Console", "Privilige rt interval is " + StockData.Singleton.Store.Account_Privileges.rt_interval);

                        //}
                    }
                }
                else
                {
                    MessageBox.Show("Exception: Not Match Privilesges AccName: " + StockData.Singleton.Store.Account.ID);
                    throw new Exception("Exception: Not Match Privilesges AccName: " + StockData.Singleton.Store.Account.ID);
                }
                connection.Close();

                if (mDBFactory_dev == null)
                {
                    StockLog.Logger.LOG.WriteLog("Error", "Dev Connector Create Fail invaild url and port");
                }
            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());

                if (connection != null)
                {
                    connection.Close();
                }
            }
        }

        private void LoadHoldStock()
        {
            MySqlConnection connection = null;
            try
            {
                connection = mDBFactory.Connect();
                if (connection == null)
                {
                    StockLog.Logger.LOG.WriteLog("Error", "[LoadHoldStock] Database Conntion Fail");
                    return;
                }
                DataTable dataTable = mDBFactory.ExecuteDataTable(connection, "SELECT * FROM tb_stock_hold_v2 WHERE account='" + StockData.Singleton.Store.Account.MainAccount + "'");

                if (dataTable.Rows.Count > 0)
                {
                    List<StockData.DataStruct.OrderInformation> dataList = new List<StockData.DataStruct.OrderInformation>();

                    for (int i = 0; i < dataTable.Rows.Count; i++)
                    {
                        StockData.DataStruct.OrderInformation oi = new StockData.DataStruct.OrderInformation();
                        StockData.Singleton.Store.GetDataTable<StockData.DataStruct.OrderInformation>(dataTable.Rows[i], oi);
                        dic_recentOrder[oi.order_num] = oi;
                    }
                }
                
                connection.Close();
            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());

                if (connection != null)
                {
                    connection.Close();
                }
            }
        }

        public void onEventConnect(object sender, _DKHOpenAPIEvents_OnEventConnectEvent e)
        {
            if (e.nErrCode == 0)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        loginRestartEvent.Set();

                        string accountlist = "";

                        accountlist = axKHOpenAPI1.GetLoginInfo("ACCLIST");
                        StockData.Singleton.Store.Account.Account = accountlist.Split(';');

                        if (StockData.Singleton.Store.Account.Account != null)
                        {
                            for (int i = 0; i < StockData.Singleton.Store.Account.Account.Length; i++)
                            {
                                StockLog.Logger.LOG.WriteLog("APIOperation", "AccList: " + StockData.Singleton.Store.Account.Account[i]);
                            }
                        }

                        StockData.Singleton.Store.Account.ID = axKHOpenAPI1.GetLoginInfo("USER_ID");
                        StockData.Singleton.Store.Account.Name = axKHOpenAPI1.GetLoginInfo("USER_NAME");
                        StockData.Singleton.Store.Account.ConnectType = axKHOpenAPI1.GetLoginInfo("GetServerGubun");


                        StockLog.Logger.LOG.WriteLog("APIOperation", "Account.ID: " + StockData.Singleton.Store.Account.ID);
                        StockLog.Logger.LOG.WriteLog("APIOperation", "Account.Name: " + StockData.Singleton.Store.Account.Name);
                        StockLog.Logger.LOG.WriteLog("APIOperation", "Account.ConnectType: " + StockData.Singleton.Store.Account.ConnectType);


                        SettingPrivileges();

                        LoadHoldStock();
                        //Load Latest Hold Stock StockData.DataStruct.OrderInformation


                        //if (StockData.Singleton.Store.Account.ID == "lsyoup")
                        //string stockCodeList = axKHOpenAPI1.GetCodeListByMarket("0"); // 전종목코드 set

                        List<string> reqList = new List<string>();

                        string[] kospiList = axKHOpenAPI1.GetCodeListByMarket("0").Split(';');
                        string[] kosdaqList = axKHOpenAPI1.GetCodeListByMarket("10").Split(';');
                        string[] etfList = axKHOpenAPI1.GetCodeListByMarket("8").Split(';');

                        StockData.Singleton.Store.KospiList = kospiList.ToList();
                        StockData.Singleton.Store.KosdaqList = kosdaqList.ToList();
                        StockData.Singleton.Store.ETFList = etfList.ToList();

                        StockData.Singleton.Store.KospiList.Remove("");
                        StockData.Singleton.Store.KosdaqList.Remove("");
                        StockData.Singleton.Store.ETFList.Remove("");

                        // 2021. 11. 21. 코스피 분할을 위한 테스트 코드

                        int cnt = StockData.Singleton.Store.KospiList.Count;
                        StockLog.Logger.LOG.WriteLog("Test", "count: " + cnt);
                        List<string> kospi1 = new List<string>();
                        List<string> kospi2 = new List<string>();

                        for (int i = 0; i < StockData.Singleton.Store.KospiList.Count; i++)
                        {
                            string code = StockData.Singleton.Store.KospiList[i].Replace("K", "").Replace("L", "");


                            if (!int.TryParse(code, out int icode))
                            {
                                StockLog.Logger.LOG.WriteLog("Test", "error cocd: " + code);
                            }

                            //0, 1, 2, 3, 4, 5, 6
                            //247, 241, 232, 262, 247, 256, 243
                            if (icode % 7 == 0 || icode % 7 == 3 || icode % 7 == 5)
                            {
                                kospi1.Add(StockData.Singleton.Store.KospiList[i]);
                            }
                            else
                            {
                                kospi2.Add(StockData.Singleton.Store.KospiList[i]);
                            }
                        }


                        StockLog.Logger.LOG.WriteLog("Test", "count1: " + kospi1.Count + ", count2: " + kospi2.Count);


                        cnt = StockData.Singleton.Store.KosdaqList.Count;
                        StockLog.Logger.LOG.WriteLog("Test", "kosdaq count: " + cnt);
                        List<string> kosdaq1 = new List<string>();
                        List<string> kosdaq2 = new List<string>();

                        for (int i = 0; i < StockData.Singleton.Store.KosdaqList.Count; i++)
                        {
                            string code = StockData.Singleton.Store.KosdaqList[i].Replace("K", "").Replace("L", "").Replace("M", "");


                            if (!int.TryParse(code, out int icode))
                            {
                                StockLog.Logger.LOG.WriteLog("Test", "error cocd: " + code);
                            }

                            //0, 1, 2, 3, 4, 5, 6
                            //247, 241, 232, 262, 247, 256, 243
                            if (icode % 7 == 2 || icode % 7 == 5 || icode % 7 == 6)
                            {
                                kosdaq1.Add(StockData.Singleton.Store.KosdaqList[i]);
                            }
                            else
                            {
                                kosdaq2.Add(StockData.Singleton.Store.KosdaqList[i]);
                            }
                        }

                        StockLog.Logger.LOG.WriteLog("Test", "Kosdaq count1: " + kosdaq1.Count + ", count2: " + kosdaq2.Count);

                        for (int i = 0; i < StockData.Singleton.Store.ETFList.Count; i++)
                        {
                            if (StockData.Singleton.Store.KospiList.Contains(StockData.Singleton.Store.ETFList[i]))
                            {
                                StockData.Singleton.Store.KospiList.Remove(StockData.Singleton.Store.ETFList[i]);
                            }

                            if (StockData.Singleton.Store.KosdaqList.Contains(StockData.Singleton.Store.ETFList[i]))
                            {
                                StockData.Singleton.Store.KosdaqList.Remove(StockData.Singleton.Store.ETFList[i]);
                            }
                        }

                        kospiList = StockData.Singleton.Store.KospiList.ToArray();
                        kosdaqList = StockData.Singleton.Store.KosdaqList.ToArray();
                        etfList = StockData.Singleton.Store.ETFList.ToArray();


                        TimeChecker(() =>
                    {
                        SetBasicInfo();
                        SetSotckCode();
                        //SetInitRealData();
                        SetInitAskBidData();
                    }, "onEventConnect Set RealData");


                        string reqstr = "";
                        string fieldList = "10;13;14;16;17;18;20;228;41;61;81;51;71;91;9001;302;9068;1221;1223;1224;1225;302;9201;951;27;28";

                        if (StockData.Singleton.Store.Account_Privileges.get_kospi == "1")
                        {
                            for (int i = 0; i < kospi1.Count; i++)
                            {
                                reqList.Add(kospi1[i]);

                                if (reqList.Count >= 100)
                                {
                                    reqstr = string.Join(";", reqList);
                                    axKHOpenAPI1.SetRealReg(StockData.Singleton.Store.GetScreenNumber(), reqstr, fieldList, "1");
                                    StockLog.Logger.LOG.WriteLog("APIOperation", "req: " + reqstr);

                                    reqList = new List<string>();
                                }
                            }

                            if (reqList.Count > 0)
                            {
                                reqstr = string.Join(";", reqList);
                                axKHOpenAPI1.SetRealReg(StockData.Singleton.Store.GetScreenNumber(), reqstr, fieldList, "1");
                                StockLog.Logger.LOG.WriteLog("APIOperation", "req: " + reqstr);

                                reqList = new List<string>();
                            }
                        }

                        if (StockData.Singleton.Store.Account_Privileges.get_kospi2 == "1")
                        {
                            for (int i = 0; i < kospi2.Count; i++)
                            {
                                reqList.Add(kospi2[i]);

                                if (reqList.Count >= 100)
                                {
                                    reqstr = string.Join(";", reqList);
                                    axKHOpenAPI1.SetRealReg(StockData.Singleton.Store.GetScreenNumber(), reqstr, fieldList, "1");
                                    StockLog.Logger.LOG.WriteLog("APIOperation", "req: " + reqstr);

                                    reqList = new List<string>();
                                }
                            }

                            if (reqList.Count > 0)
                            {
                                reqstr = string.Join(";", reqList);
                                axKHOpenAPI1.SetRealReg(StockData.Singleton.Store.GetScreenNumber(), reqstr, fieldList, "1");
                                StockLog.Logger.LOG.WriteLog("APIOperation", "req: " + reqstr);

                                reqList = new List<string>();
                            }
                        }

                        if (StockData.Singleton.Store.Account_Privileges.get_kosdaq == "1")
                        {
                            for (int i = 0; i < kosdaq1.Count; i++)
                            {
                                reqList.Add(kosdaq1[i]);

                                if (reqList.Count >= 100)
                                {
                                    reqstr = string.Join(";", reqList);
                                    axKHOpenAPI1.SetRealReg(StockData.Singleton.Store.GetScreenNumber(), reqstr, fieldList, "1");
                                    StockLog.Logger.LOG.WriteLog("APIOperation", "req: " + reqstr);

                                    reqList = new List<string>();
                                }
                            }

                            if (reqList.Count > 0)
                            {
                                reqstr = string.Join(";", reqList);
                                axKHOpenAPI1.SetRealReg(StockData.Singleton.Store.GetScreenNumber(), reqstr, fieldList, "1");
                                StockLog.Logger.LOG.WriteLog("APIOperation", "req: " + reqstr);

                                reqList = new List<string>();
                            }
                        }


                        if (StockData.Singleton.Store.Account_Privileges.get_kosdaq2 == "1")
                        {
                            for (int i = 0; i < kosdaq2.Count; i++)
                            {
                                reqList.Add(kosdaq2[i]);

                                if (reqList.Count >= 100)
                                {
                                    reqstr = string.Join(";", reqList);
                                    axKHOpenAPI1.SetRealReg(StockData.Singleton.Store.GetScreenNumber(), reqstr, fieldList, "1");
                                    StockLog.Logger.LOG.WriteLog("APIOperation", "req: " + reqstr);

                                    reqList = new List<string>();
                                }
                            }

                            if (reqList.Count > 0)
                            {
                                reqstr = string.Join(";", reqList);
                                axKHOpenAPI1.SetRealReg(StockData.Singleton.Store.GetScreenNumber(), reqstr, fieldList, "1");
                                StockLog.Logger.LOG.WriteLog("APIOperation", "req: " + reqstr);

                                reqList = new List<string>();
                            }
                        }

                        if (StockData.Singleton.Store.Account_Privileges.get_etf == "1")
                        {
                            for (int i = 0; i < etfList.Length; i++)
                            {
                                reqList.Add(etfList[i]);

                                if (reqList.Count >= 100)
                                {
                                    reqstr = string.Join(";", reqList);
                                    axKHOpenAPI1.SetRealReg(StockData.Singleton.Store.GetScreenNumber(), reqstr, fieldList, "1");
                                    StockLog.Logger.LOG.WriteLog("APIOperation", "req: " + reqstr);

                                    reqList = new List<string>();
                                }
                            }

                            if (reqList.Count > 0)
                            {
                                reqstr = string.Join(";", reqList);
                                axKHOpenAPI1.SetRealReg(StockData.Singleton.Store.GetScreenNumber(), reqstr, fieldList, "1");
                                StockLog.Logger.LOG.WriteLog("APIOperation", "req: " + reqstr);

                                reqList = new List<string>();
                            }
                        }

                        StockTimer.StockSchedInfo RestartSched = new StockTimer.StockSchedInfo("RestartSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "065100", "065100", RestartReserve, null);
                        //StockTimer.StockSchedInfo RestartSched2 = new StockTimer.StockSchedInfo("RestartSched2", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "153100", "153100", RestartReserve, null);
                        StockData.Singleton.Store.StockTimer.SetScheduler(RestartSched);
                        //StockData.Singleton.Store.StockTimer.SetScheduler(RestartSched2);

                        if (StockData.Singleton.Store.Account_Privileges.info_check == "1")
                        {
                            StockTimer.StockSchedInfo infoSched = new StockTimer.StockSchedInfo("infoSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "090003", "153030", InformationChecker, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(infoSched);
                        }

                        if (StockData.Singleton.Store.Account_Privileges.day_update == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: day_update");
                            StockTimer.StockSchedInfo dailyStockSched = new StockTimer.StockSchedInfo("dailyStockSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, StockData.Singleton.Store.Account_Privileges.day_update_time, StockData.Singleton.Store.Account_Privileges.day_update_time, BatchDailyStockData, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(dailyStockSched);
                        }

                        if (StockData.Singleton.Store.Account_Privileges.min_update == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: min_update");
                            StockTimer.StockSchedInfo dailyMinStockSched = new StockTimer.StockSchedInfo("dailyMinStockSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, StockData.Singleton.Store.Account_Privileges.min_update_time, StockData.Singleton.Store.Account_Privileges.min_update_time, BatchMinuteStockData, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(dailyMinStockSched);
                        }

                        if (StockData.Singleton.Store.Account_Privileges.basic_update == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: basic_update");
                            StockTimer.StockSchedInfo basicUpdateSched = new StockTimer.StockSchedInfo("basicUpdateSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, StockData.Singleton.Store.Account_Privileges.basic_update_time, StockData.Singleton.Store.Account_Privileges.basic_update_time, BatchStockBasic, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(basicUpdateSched);
                        }

                        if (StockData.Singleton.Store.Account_Privileges.tick_update == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: tick_update");
                            StockTimer.StockSchedInfo tickUpdateSched = new StockTimer.StockSchedInfo("tickUpdateSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, StockData.Singleton.Store.Account_Privileges.tick_update_time, StockData.Singleton.Store.Account_Privileges.tick_update_time, BatchTickStockData, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(tickUpdateSched);
                        }

                        if (StockData.Singleton.Store.Account_Privileges.ask_bid_update == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: askbid_update");
                            StockTimer.StockSchedInfo askbidUpdateSched = new StockTimer.StockSchedInfo("askbidUpdateSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, StockData.Singleton.Store.Account_Privileges.ask_bid_update_time, StockData.Singleton.Store.Account_Privileges.ask_bid_update_time, BatchAskBidStockData, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(askbidUpdateSched);
                        }

                        if (StockData.Singleton.Store.Account_Privileges.index_update == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: index_update");
                            StockTimer.StockSchedInfo indexUpdateSched_9 = new StockTimer.StockSchedInfo("indexUpdateSched_9", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "090000", "090000", BatchIndexData, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(indexUpdateSched_9);

                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: index_update");
                            StockTimer.StockSchedInfo indexUpdateSched_10 = new StockTimer.StockSchedInfo("indexUpdateSched_10", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "100000", "100000", BatchIndexData, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(indexUpdateSched_10);

                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: index_update");
                            StockTimer.StockSchedInfo indexUpdateSched_11 = new StockTimer.StockSchedInfo("indexUpdateSched_11", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "110000", "110000", BatchIndexData, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(indexUpdateSched_11);

                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: index_update");
                            StockTimer.StockSchedInfo indexUpdateSched_12 = new StockTimer.StockSchedInfo("indexUpdateSched_12", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "120000", "120000", BatchIndexData, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(indexUpdateSched_12);

                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: index_update");
                            StockTimer.StockSchedInfo indexUpdateSched_13 = new StockTimer.StockSchedInfo("indexUpdateSched_13", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "130000", "130000", BatchIndexData, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(indexUpdateSched_13);

                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: index_update");
                            StockTimer.StockSchedInfo indexUpdateSched_14 = new StockTimer.StockSchedInfo("indexUpdateSched_14", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "140000", "140000", BatchIndexData, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(indexUpdateSched_14);

                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: index_update");
                            StockTimer.StockSchedInfo indexUpdateSched_15 = new StockTimer.StockSchedInfo("indexUpdateSched_15", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "150000", "150000", BatchIndexData, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(indexUpdateSched_15);

                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: index_update");
                            StockTimer.StockSchedInfo indexUpdateSched_16 = new StockTimer.StockSchedInfo("indexUpdateSched_16", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "160000", "160000", BatchIndexData, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(indexUpdateSched_16);
                        }

                        if (StockData.Singleton.Store.Account_Privileges.index_day_update == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: index_day_update");
                            StockTimer.StockSchedInfo indexDayUpdateSched_1 = new StockTimer.StockSchedInfo("indexDayUpdateSched_1", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "150000", "150000", opt20006, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(indexDayUpdateSched_1);

                            StockTimer.StockSchedInfo indexDayUpdateSched_2 = new StockTimer.StockSchedInfo("indexDayUpdateSched_2", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "150100", "150100", opt20006, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(indexDayUpdateSched_2);

                            StockTimer.StockSchedInfo indexDayUpdateSched_3 = new StockTimer.StockSchedInfo("indexDayUpdateSched_3", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "150200", "150200", opt20006, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(indexDayUpdateSched_3);
                        }

                        if (StockData.Singleton.Store.Account_Privileges.request_timeout == "1")
                        {
                            StockTimer.StockSchedInfo requestCheckSched = new StockTimer.StockSchedInfo("requestCheckSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "000000", "235959", RequestCheck, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(requestCheckSched);
                        }

                        if (StockData.Singleton.Store.Account_Privileges.load_rt_data == "1")
                        {
                            StockTimer.StockSchedInfo loadRtMoved = new StockTimer.StockSchedInfo("loadRtMoved", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, StockData.Singleton.Store.Account_Privileges.load_rt_time, StockData.Singleton.Store.Account_Privileges.load_rt_time, LoadRtData, null);
                            StockData.Singleton.Store.StockTimer.SetScheduler(loadRtMoved);
                        }

                        StockData.Singleton.Store.StockTimer.Start();

                        if (StockData.Singleton.Store.Account_Privileges.min_update_rt == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: min_update_rt");
                            StockTimer.StockSchedInfo realTimeSched = new StockTimer.StockSchedInfo("realTimeSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "090003", "153030", UpdateMinRealTimeData, new string[] { "tb_stock_price_rt_min" });
                            StockData.Singleton.Store.StockTimer_Tight.SetScheduler(realTimeSched);
                        }
                        else if (StockData.Singleton.Store.Account_Privileges.min_update_rt_sub == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: min_update_rt_sub");
                            StockTimer.StockSchedInfo realTimeSched = new StockTimer.StockSchedInfo("realTimeSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "090003", "153030", UpdateMinRealTimeData, new string[] { "tb_stock_price_rt_min_sub" });
                            StockData.Singleton.Store.StockTimer_Tight.SetScheduler(realTimeSched);
                        }

                        if (StockData.Singleton.Store.Account_Privileges.sec_update_rt == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: sec_update_rt");
                            StockTimer.StockSchedInfo rtSecondSched = new StockTimer.StockSchedInfo("rtSecondSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "090000", "153001", UpdateSecRealTimeData, new string[] { "tb_stock_price_rt_second" });
                            StockData.Singleton.Store.StockTimer_Tight.SetScheduler(rtSecondSched);
                        }
                        else if (StockData.Singleton.Store.Account_Privileges.sec_update_rt_sub == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: sec_update_rt_sub");
                            StockTimer.StockSchedInfo rtSecondSched = new StockTimer.StockSchedInfo("rtSecondSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "090000", "153001", UpdateSecRealTimeData, new string[] { "tb_stock_price_rt_second_sub" });
                            StockData.Singleton.Store.StockTimer_Tight.SetScheduler(rtSecondSched);
                        }
                        if (StockData.Singleton.Store.Account_Privileges.trading_check == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: trading_check");
                            StockTimer.StockSchedInfo buySellSched = new StockTimer.StockSchedInfo("buySellSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "090003", "153030", BuySellCheck, null);
                            StockData.Singleton.Store.StockTimer_Tight.SetScheduler(buySellSched);
                        }


                        StockData.Singleton.Store.StockTimer_Tight.Interval = 100;
                        StockData.Singleton.Store.StockTimer_Tight.Start();

                        if (StockData.Singleton.Store.Account_Privileges.tick_update_rt == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: tick_update_rt");
                            StockTimer.StockSchedInfo rtTickSched = new StockTimer.StockSchedInfo("rtTickSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "080000", "160000", UpdateTickRealTimeData, new string[] { "tb_stock_price_rt_tick" });
                            StockData.Singleton.Store.StockTimer_Tick.SetScheduler(rtTickSched);

                            StockData.Singleton.Store.StockTimer_Tick.Interval = StockData.Singleton.Store.Account_Privileges.rt_interval;
                            StockData.Singleton.Store.StockTimer_Tick.Start();

                        }
                        else if (StockData.Singleton.Store.Account_Privileges.tick_update_rt_sub == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: tick_update_rt_sub");
                            StockTimer.StockSchedInfo rtTickSched = new StockTimer.StockSchedInfo("rtTickSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "080000", "160000", UpdateTickRealTimeData, new string[] { "tb_stock_price_rt_tick_sub" });
                            StockData.Singleton.Store.StockTimer_Tick.SetScheduler(rtTickSched);

                            StockData.Singleton.Store.StockTimer_Tick.Interval = StockData.Singleton.Store.Account_Privileges.rt_interval;
                            StockData.Singleton.Store.StockTimer_Tick.Start();
                        }

                        if (StockData.Singleton.Store.Account_Privileges.ask_bid_update_rt == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: ask_bid_update_rt");
                            StockTimer.StockSchedInfo rtAskBidSched = new StockTimer.StockSchedInfo("rtAskBidSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "080000", "160000", UpdateAskBidRealTimeData, new string[] { "tb_stock_price_rt_ask_bid" });
                            StockData.Singleton.Store.StockTimer_AskBid.SetScheduler(rtAskBidSched);

                            StockData.Singleton.Store.StockTimer_AskBid.Interval = StockData.Singleton.Store.Account_Privileges.rt_interval;
                            StockData.Singleton.Store.StockTimer_AskBid.Start();
                        }
                        else if (StockData.Singleton.Store.Account_Privileges.ask_bid_update_rt_sub == "1")
                        {
                            StockLog.Logger.LOG.WriteLog("Console", "Privileges: ask_bid_update_rt_sub");
                            StockTimer.StockSchedInfo rtAskBidSched = new StockTimer.StockSchedInfo("rtAskBidSched", StockTimer.Cycle.Weekly, StockTimer.WeekCycle.WeekWeekday, 1, "080000", "160000", UpdateAskBidRealTimeData, new string[] { "tb_stock_price_rt_ask_bid_sub" });
                            StockData.Singleton.Store.StockTimer_AskBid.SetScheduler(rtAskBidSched);

                            StockData.Singleton.Store.StockTimer_AskBid.Interval = StockData.Singleton.Store.Account_Privileges.rt_interval;
                            StockData.Singleton.Store.StockTimer_AskBid.Start();
                        }

                        StockData.Singleton.Store.StockTimer_System.Interval = 10000;
                        StockTimer.StockSchedInfo systemCheckSched = new StockTimer.StockSchedInfo("systemCheckSched", StockTimer.Cycle.Daily, StockTimer.WeekCycle.WeekWeekday, 1, "000000", "235959", SystemOperationCheck, null);
                        StockData.Singleton.Store.StockTimer_System.SetScheduler(systemCheckSched);
                        StockData.Singleton.Store.StockTimer_System.Start();

                        if (!dayHistoryContinue && StockData.Singleton.Store.WorkInfo.DayStarted)
                        {
                            ThreadPool.QueueUserWorkItem(x =>
                            {
                                BatchDailyStockData(null);
                            });
                        }

                        if (!minHistoryContinue && StockData.Singleton.Store.WorkInfo.MinStarted)
                        {
                            ThreadPool.QueueUserWorkItem(x =>
                            {
                                BatchMinuteStockData(null);
                            });
                        }

                        if (!tickHistoryContinue && StockData.Singleton.Store.WorkInfo.TickStarted)
                        {
                            ThreadPool.QueueUserWorkItem(x =>
                            {
                                BatchTickStockData(null);
                            });
                        }

                        if (dayHistoryContinue && (DateTime.Now.Hour >= 18 || DateTime.Now.Hour < 8))
                        {
                            ThreadPool.QueueUserWorkItem(x =>
                            {
                                BatchDailyStockData(null);
                            });
                        }

                        if (minHistoryContinue)
                        {
                            ThreadPool.QueueUserWorkItem(x =>
                            {
                                BatchMinuteStockData(null);
                            });
                        }

                        if (tickHistoryContinue && (DateTime.Now.Hour >= 18 || DateTime.Now.Hour < 8))
                        {
                            ThreadPool.QueueUserWorkItem(x =>
                            {
                                BatchTickStockData(null);
                            });
                        }

                        if (StockData.Singleton.Store.WorkInfo.BasicStarted)
                        {
                            ThreadPool.QueueUserWorkItem(x =>
                            {
                                BatchStockBasic(null);
                            });
                        }

                        if (StockData.Singleton.Store.WorkInfo.AskBidStarted)
                        {
                            ThreadPool.QueueUserWorkItem(x =>
                            {
                                BatchAskBidStockData(null);
                            });
                        }

                        ThreadPool.QueueUserWorkItem(x =>
                        {
                            RetryAction();
                        });

                        StockLog.Logger.LOG.WriteLog("Console", "OnConnect Complate.");
                    }
                    catch (Exception except)
                    {
                        StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                    }

                    InformationChecker(null);

                });
            }

        }

        private void onReceiveTrData(object sender, _DKHOpenAPIEvents_OnReceiveTrDataEvent e)
        {
            StockLog.Logger.LOG.WriteLog("onReceiveTrData", e.sRQName);

            lock (reqLock)
            {
                RQCheckBox[e.sRQName] = null;
            }
            if (e.sRQName == "미체결요청")
            {
                int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);

                for (int i = 0; i < nCnt; i++)
                {
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 계좌번호: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "계좌번호").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 주문번호: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "주문번호").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 관리사번: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "관리사번").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 종목코드: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목코드").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 업무구분: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "업무구분").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 주문상태: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "주문상태").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 종목명: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목명").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 주문수량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "주문수량").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 주문가격: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "주문가격").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 미체결수량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "미체결수량").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 체결누계금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결누계금액").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 원주문번호: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "원주문번호").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 주분구분: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "주분구분").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 매매구분: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매매구분").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 시간: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "시간").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 체결번호: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결번호").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 체결가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 체결량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결량").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 현재가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "현재가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 매도호가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도호가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 매수호가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수호가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 단위체결가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "단위체결가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 단위체결량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "단위체결량").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 당일매매수수료: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "당일매매수수료").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 당일매매세금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "당일매매세금").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[미체결요청] 개인투자자: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "개인투자자").Trim());
                }
            }

            if (e.sRQName == "체결요청")
            {
                int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);

                for (int i = 0; i < nCnt; i++)
                {
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 주문번호: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "주문번호").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 종목명: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목명").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 주문구분: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "주문구분").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 주문가격: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "주문가격").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 주문수량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "주문수량").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 체결가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 체결량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결량").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 미체결수량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "미체결수량").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 당일매매수수료: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "당일매매수수료").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 당일매매세금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "당일매매세금").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 주문상태: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "주문상태").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 매매구분: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매매구분").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 원주문번호: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "원주문번호").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 주문시간: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "주문시간").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[체결요청] 종목코드: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목코드").Trim());
                }
            }

            if (e.sRQName == "종목정보요청")
            {
                //RequestInfoCode(e);
            }

            if (e.sRQName == "주식기본정보요청")
            {
                try
                {
                    StockData.DataStruct.StockBasic sb = new StockData.DataStruct.StockBasic();
                    sb.stock_code = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "종목코드").Trim();
                    sb.stock_name = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "종목명").Trim();
                    sb.dt = DateTime.Now.ToString("yyyyMMdd");
                    sb.settlement_month = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "결산월").Trim();
                    sb.face_price = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "액면가").Trim();
                    sb.capital = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "자본금").Trim();
                    sb.shares_outstanding = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "상장주식").Trim();
                    sb.credit_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용비율").Trim().Replace("+", "");
                    sb.year_high_price = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "연중최고").Trim().Replace("+", "").Replace("-", "");
                    sb.year_low_price = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "연중최저").Trim().Replace("+", "").Replace("-", "");
                    sb.market_cap = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "시가총액").Trim();
                    sb.market_weight = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "시가총액비중").Trim();
                    sb.foreign_burnout_rate = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "외인소진률").Trim().Replace("+", "");
                    sb.substitute_price = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "대용가").Trim().Replace("+", "");
                    sb.per = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "PER").Trim();
                    sb.eps = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "EPS").Trim();
                    sb.roe = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "ROE").Trim();
                    sb.pbr = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "PBR").Trim();
                    sb.ev = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "EV").Trim();
                    sb.bps = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "BPS").Trim();
                    sb.sales = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "매출액").Trim();
                    sb.operating_income = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "영업이익").Trim();
                    sb.net_income = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "당기순이익").Trim();
                    sb.open_price = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "시가").Trim().Replace("+", "").Replace("-", "");
                    sb.high_price = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "고가").Trim().Replace("+", "").Replace("-", "");
                    sb.low_price = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "저가").Trim().Replace("+", "").Replace("-", "");
                    sb.upper_limit_price = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "상한가").Trim().Replace("+", "").Replace("-", "");
                    sb.lower_limit_price = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "하한가").Trim().Replace("+", "").Replace("-", "");
                    sb.base_price = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "기준가").Trim().Replace("+", "").Replace("-", "");
                    sb.current_price = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "현재가").Trim().Replace("+", "").Replace("-", "");
                    sb.pre_day_symbol = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "대비기호").Trim();
                    sb.pre_day_change = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "전일대비").Trim();
                    sb.range_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "등락율").Trim().Replace("+", "");
                    sb.volume = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "거래량").Trim();
                    sb.pre_day_volume = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "거래대비").Trim().Replace("+", "");
                    //sb.face_price_unit = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "액면가단위").Trim();
                    sb.circulate_share = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "유통주식").Trim();
                    sb.circulate_ratio = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "유통비율").Trim();
                    sb.stock_group = GetStockGroup(sb.stock_code);
                    DBManager.MariaClient mdb = new DBManager.MariaClient(StockData.Singleton.Store.AccessInfo.Url, StockData.Singleton.Store.AccessInfo.Port, StockData.Singleton.Store.AccessInfo.User, StockData.Singleton.Store.AccessInfo.Password, StockData.Singleton.Store.AccessInfo.DBName);
                    bool dbSuccess = mdb.Connect();
                    if (!dbSuccess)
                    {
                        StockLog.Logger.LOG.WriteLog("Error", "DB Connection Fail: [주식기본정보요청]");
                        return;
                    }

                    StockData.Singleton.Store.SetMemberValueZero<StockData.DataStruct.StockBasic>(sb);

                    if (sb.stock_code != "0")
                    {
                        mdb.Execute(StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.StockBasic>("tb_stock_bsc_v2", sb));


                        StockData.Singleton.Store.WorkInfo.BasicCurrentCode = sb.stock_code;
                        StockData.Singleton.Store.SaveSetting();
                    }
                    else
                    {
                        StockLog.Logger.LOG.WriteLog("Error", "[주식기본정보요청]: stock_code is zero code: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "종목코드").Trim());
                    }

                    mdb.Close();

                    basicInfoStopEvent.Set();

                }
                catch (Exception except)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                }

            }

            if (e.sRQName == "복수종목정보요청")
            {
                int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);

                for (int i = 0; i < nCnt; i++)
                {
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 종목코드: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목코드").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 종목명: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목명").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 현재가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "현재가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 기준가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "기준가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 전일대비: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "전일대비").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 전일대비기호: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "전일대비기호").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 등락율: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "등락율").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 거래량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래량").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 거래대금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래대금").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 체결량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결량").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 체결강도: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결강도").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 전일거래량대비: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "전일거래량대비").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 매도호가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도호가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 매수호가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수호가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 매도1차호가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도1차호가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 매도2차호가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도2차호가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 매도3차호가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도3차호가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 매도4차호가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도4차호가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 매도5차호가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매도5차호가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 매수1차호가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수1차호가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 매수2차호가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수2차호가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 매수3차호가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수3차호가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 매수4차호가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수4차호가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 매수5차호가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매수5차호가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 상한가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "상한가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 하한가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "하한가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 시가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "시가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 고가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "고가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 저가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "저가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 종가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 체결시간: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "체결시간").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 예상체결가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "예상체결가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 예상체결량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "예상체결량").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 자본금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "자본금").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 액면가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "액면가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 시가총액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "시가총액").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 주식수: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "주식수").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 호가시간: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "호가시간").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 일자: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "일자").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 우선매도잔량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "우선매도잔량").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 우선매수잔량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "우선매수잔량").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 우선매도건수: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "우선매도건수").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 우선매수건수: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "우선매수건수").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 총매도잔량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "총매도잔량").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 총매수잔량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "총매수잔량").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 총매도건수: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "총매도건수").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 총매수건수: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "총매수건수").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 래피티: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "래피티").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 기어링: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "기어링").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 손익분기: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "손익분기").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 자본지지: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "자본지지").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] ELW만기일: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "ELW만기일").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 미결제약정: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "미결제약정").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 미결제전일대비: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "미결제전일대비").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 이론가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "이론가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 내재변동성: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "내재변동성").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 델타: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "델타").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 감마: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "감마").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 쎄타: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "쎄타").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 베가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "베가").Trim());
                    StockLog.Logger.LOG.WriteLog("복수종목정보요청", "[복수종목정보요청] 로: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "로").Trim());
                }
            }

            if (e.sRQName == "체결잔고요청")
            {
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 예수금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "예수금").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 예수금D+1: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "예수금D+1").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 에수금D+2: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "에수금D+2").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 출금가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "출금가능금액").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 미수확보금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "미수확보금").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 대용금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "대용금").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 권리대용금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "권리대용금").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 주문가능현금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "주문가능현금").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 현금미수금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "현금미수금").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 신용이자미납금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용이자미납금").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 기타대여금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "기타대여금").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 미상환융자금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "미상환융자금").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 증거금현금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "증거금현금").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 증거금대용: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "증거금대용").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 주식매수총액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "주식매수총액").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 평가금액합계: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "평가금액합계").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 총손익합계: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "총손익합계").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 총손익률: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "총손익률").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 총재매수가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "총재매수가능금액").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 20주문가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "20주문가능금액").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 30주믄가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "30주믄가능금액").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 40주문가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "40주문가능금액").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 50주문가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "50주문가능금액").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 60주문가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "60주문가능금액").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 100주문가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "100주문가능금액").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 신용융자합계: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용융자합계").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 신용융자대주합계: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용융자대주합계").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 신용담보비율: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용담보비율").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 예탁담보대출금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "예탁담보대출금액").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 매도담보대출금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "매도담보대출금액").Trim());
                StockLog.Logger.LOG.WriteLog("Console", "[체결잔고요청]: 조회건수: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "조회건수").Trim());
            }

            if (e.sRQName == "계좌수익률요청")
            {
                StockLog.Logger.LOG.WriteLog("Console", "[계좌수익률요청] Update.");
                int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);
                //List<StockData.DataStruct.DayData> dayList = new List<StockData.DataStruct.DayData>();

                DBManager.MariaClient mdb = new DBManager.MariaClient(StockData.Singleton.Store.AccessInfo.Url, StockData.Singleton.Store.AccessInfo.Port, StockData.Singleton.Store.AccessInfo.User, StockData.Singleton.Store.AccessInfo.Password, StockData.Singleton.Store.AccessInfo.DBName);
                bool dbSuccess = mdb.Connect();
                DateTime dt = DateTime.Now;
                if (!dbSuccess)
                {
                    StockLog.Logger.LOG.WriteLog("Error", "DB Connection Fail: [계좌수익률요청]");
                    return;
                }

                for (int i = 0; i < nCnt; i++)
                {
                    int buf_i = 0;

                    StockData.DataStruct.HoldStock cs = new StockData.DataStruct.HoldStock();

                    //cs.WK_DT = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "일자").Trim();
                    cs.CLIENT_ID = StockData.Singleton.Store.Account.ID;
                    cs.WK_DT = dt.ToString("yyyyMMdd");
                    cs.WK_TM = dt.ToString("HHmm00");
                    cs.ITM_C = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목코드").Trim();
                    cs.ITM_NAME = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목명").Trim();
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "현재가").Trim(), out buf_i))
                    {
                        cs.NOW_PRICE = buf_i < 0 ? buf_i * -1 : buf_i;
                    }

                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매입가").Trim(), out buf_i))
                    {
                        cs.BUY_PRICE = buf_i;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매입금액").Trim(), out buf_i))
                    {
                        cs.BUY_AM = buf_i;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "보유수량").Trim(), out buf_i))
                    {
                        cs.QTY = buf_i;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "당일매도손익").Trim(), out buf_i))
                    {
                        cs.SELL_PROFIT = buf_i;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "당일매매수수료").Trim(), out buf_i))
                    {
                        cs.COMMISSION = buf_i;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "당일매매세금").Trim(), out buf_i))
                    {
                        cs.SELL_TEX = buf_i;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "신용구분").Trim(), out buf_i))
                    {
                        cs.CREDIT = buf_i;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "결제잔고").Trim(), out buf_i))
                    {
                        cs.PAYMENT_BALANCE = buf_i;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "청산가능수량").Trim(), out buf_i))
                    {
                        cs.LIQUID_QTY = buf_i;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "신용금액").Trim(), out buf_i))
                    {
                        cs.CREDIT_PRICE = buf_i;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "신용이자").Trim(), out buf_i))
                    {
                        cs.CREDIT_INTEREST = buf_i;
                    }
                    cs.LOAN_DT = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "대출일").Trim();
                    cs.EXPIRE_DT = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "만기일").Trim();

                    mdb.Execute(StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.HoldStock>("tb_stock_hold", cs));


                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 일자: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "일자").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 종목코드: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목코드").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 종목명: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목명").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 현재가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "현재가").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 매입가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매입가").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 매입금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매입금액").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 보유수량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "보유수량").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 당일매도손익: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "당일매도손익").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 당일매매수수료: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "당일매매수수료").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 당일매매세금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "당일매매세금").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 신용구분: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "신용구분").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 대출일: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "대출일").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 결제잔고: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "결제잔고").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 청산가능수량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "청산가능수량").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 신용금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "신용금액").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 신용이자: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "신용이자").Trim());
                    StockLog.Logger.LOG.WriteLog("계좌수익률요청", "[계좌수익률요청]: 만기일: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "만기일").Trim());
                }
                mdb.Close();
            }

            if (e.sRQName == "예수금상세현황요청")
            {
                try
                {
                    StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청] Update.");
                    DateTime dt = DateTime.Now;
                    StockData.DataStruct.AccountCredit ac = new StockData.DataStruct.AccountCredit();

                    ac.ACC_NAME = StockData.Singleton.Store.Account.ID;
                    ac.WK_DT = dt.ToString("yyyyMMdd");
                    ac.WK_TM = dt.ToString("HHmm00");
                    ac.ACC_KRW_BAC = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, @"100%종목주문가능금액").Trim();
                    ac.ACC_KRW = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "예수금").Trim();
                    ac.ACC_MARGIN = "0";

                    StockLog.Logger.LOG.WriteLog("예수금상세현황요청", ac.ACC_NAME + ", " + ac.WK_DT + ", " + ac.WK_TM + ", " + ac.ACC_KRW_BAC + ", " + ac.ACC_KRW + ", " + ac.ACC_MARGIN);
                    DBManager.MariaClient mdb = new DBManager.MariaClient(StockData.Singleton.Store.AccessInfo.Url, StockData.Singleton.Store.AccessInfo.Port, StockData.Singleton.Store.AccessInfo.User, StockData.Singleton.Store.AccessInfo.Password, StockData.Singleton.Store.AccessInfo.DBName);
                    bool dbSuccess = mdb.Connect();
                    if (!dbSuccess)
                    {
                        StockLog.Logger.LOG.WriteLog("Error", "DB Connection Fail: [예수금상세현황요청]");
                        return;
                    }

                    mdb.Execute(StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.AccountCredit>("tb_account_bsc", ac));
                    mdb.Close();
                }
                catch (Exception except)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                }
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 예수금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "예수금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 주식증거금현금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "주식증거금현금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 수익증권증거금현금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "수익증권증거금현금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 익일수익증권매도정산대금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "익일수익증권매도정산대금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 해외주식원화대용설정금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "해외주식원화대용설정금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 신용보증금현금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용보증금현금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 신용담보금현금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용담보금현금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 추가담보금현금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "추가담보금현금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 기타증거금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "기타증거금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 미수확보금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "미수확보금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 공매도대금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "공매도대금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 신용설정평가금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용설정평가금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 수표입금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "수표입금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 기타수표입금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "기타수표입금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 기타수표입금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "기타수표입금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 신용담보재사용: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용담보재사용").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 코넥스기본예탁금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "코넥스기본예탁금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: ELW예탁평가금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "ELW예탁평가금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 신용대주권리예정금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용대주권리예정금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 생계형가입금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "생계형가입금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 생계형입금가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "생계형입금가능금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 대용금평가금액(합계): " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "대용금평가금액(합계)").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 잔고대용평가금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "잔고대용평가금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 위탁대용잔고평가금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "위탁대용잔고평가금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 수익증권대용평가금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "수익증권대용평가금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 위탁증거금대용: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "위탁증거금대용").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 신용보증금대용: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용보증금대용").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 신용담보금대용: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용담보금대용").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 추가담보금대용: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "추가담보금대용").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 권리대용금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "권리대용금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 출금가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "출금가능금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 랩출금가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "랩출금가능금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 주문가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "주문가능금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 수익증권매수가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "수익증권매수가능금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", @"[예수금상세현황요청]: 20%종목주문가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, @"20%종목주문가능금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", @"[예수금상세현황요청]: 30%종목주문가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, @"30%종목주문가능금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", @"[예수금상세현황요청]: 40%종목주문가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, @"40%종목주문가능금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", @"[예수금상세현황요청]: 100%종목주문가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, @"100%종목주문가능금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 현금미수금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "현금미수금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 현금미수연체료: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "현금미수연체료").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 현금미수금합계: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "현금미수금합계").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 신용이자미납: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용이자미납").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 신용이자미납연체로: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용이자미납연체로").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 신용이자미납합계: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용이자미납합계").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 기타대여금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "기타대여금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 기타대여금연체로: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "기타대여금연체로").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 기타대여금합계: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "기타대여금합계").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 미상환융자금: : " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "미상환융자금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 융자금합계: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "융자금합계").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 대주금합계: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "대주금합계").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 신용담보비율: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "신용담보비율").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 중도이용료: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "중도이용료").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 최소주문가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "최소주문가능금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 대출총평가금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "대출총평가금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 예탁담보대출잔고: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "예탁담보대출잔고").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 매도담보대출잔고: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "매도담보대출잔고").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: d+1추정예수금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "d+1추정예수금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: d+1매도매수정산금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "d+1매도매수정산금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: d+1매수정산금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "d+1매수정산금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: d+1미수변제소요금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "d+1미수변제소요금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: d+1매도정산금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "d+1매도정산금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: d+1출금가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "d+1출금가능금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: d+2추정예수금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "d+2추정예수금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: d+2매도매수정산금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "d+2매도매수정산금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: d+2미수변제소요금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "d+2미수변제소요금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: d+2매도정산금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "d+2매도정산금").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: d+2출금가능금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "d+2출금가능금액").Trim());
                //StockLog.Logger.LOG.WriteLog("Console", "[예수금상세현황요청]: 출력건수: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "출력건수").Trim());
            }

            if (e.sRQName == "계좌평가현황요청")
            {

                int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);

                StockLog.Logger.LOG.WriteLog("Test", "[계좌평가현황요청]: 계좌명: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "계좌명").Trim());
                StockLog.Logger.LOG.WriteLog("Test", "[계좌평가현황요청]: 지점명: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "지점명").Trim());
                StockLog.Logger.LOG.WriteLog("Test", "[계좌평가현황요청]: 예수금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, 0, "예수금").Trim());


                for (int i = 0; i < nCnt; i++)
                {
                    StockLog.Logger.LOG.WriteLog("Test", "[계좌평가현황요청]: 종목명: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목명").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[계좌평가현황요청]: 종목코드: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목코드").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[계좌평가현황요청]: 보유수량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "보유수량").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[계좌평가현황요청]: 현재가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "현재가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[계좌평가현황요청]: 평가금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "평가금액").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[계좌평가현황요청]: 손익금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "손익금액").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[계좌평가현황요청]: 손익율: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "손익율").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[계좌평가현황요청]: 매입금액: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "매입금액").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[계좌평가현황요청]: 결제잔고: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "결제잔고").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[계좌평가현황요청]: 금일매수수량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "금일매수수량").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[계좌평가현황요청]: 금일매도수량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "금일매도수량").Trim());
                }
            }

            if (e.sRQName == "일별데이터조회")
            {
                StockLog.Logger.LOG.WriteLog("APIOperation", "[" + e.sRQName + "]" + dayInforCurrentCode + ": Response Request Data, Error Code: " + e.sErrorCode);

                if (dayInforCurrentCode != "")
                {
                    List<StockData.DataStruct.DailyData> dayList = GetDailyData(e, dayInforCurrentCode);

                    SetDailyStockData(new List<object> { dayList, e.sPrevNext });
                }
            }

            if (e.sRQName == "주식분봉차트조회요청")
            {
                StockLog.Logger.LOG.WriteLog("APIOperation", "[" + e.sRQName + "]" + minInforCurrentCode + ": Response Request Data, Error Code: " + e.sErrorCode);

                if (minInforCurrentCode != "")
                {
                    List<StockData.DataStruct.MinuteData> minList = GetMinuteData(e, minInforCurrentCode);

                    SetMinuteStockData(new List<object> { minList, e.sPrevNext });
                }
                //if (!ThreadPool.QueueUserWorkItem(new WaitCallback(SetMinuteStockData), new List<object> { minList, e.sPrevNext }))
                //{
                //    StockLog.Logger.LOG.WriteLog("Error", "Fail ThreadPool");
                //    SetMinuteStockData(new List<object> { minList, e.sPrevNext });
                //}

            }
            //주식틱차트조회요청
            if (e.sRQName == "주식틱차트조회요청")
            {
                StockLog.Logger.LOG.WriteLog("APIOperation", "[" + e.sRQName + "]" + tickInforCurrentCode + ": Response Request Data, Error Code: " + e.sErrorCode);

                if (tickInforCurrentCode != "")
                {
                    List<StockData.DataStruct.TickData> tickList = GetTickData(e, tickInforCurrentCode);
                    SetTickStockData(new List<object> { tickList, e.sPrevNext });
                }
            }

            if (e.sRQName == "주가호가요청")
            {
                StockLog.Logger.LOG.WriteLog("APIOperation", "[" + e.sRQName + "]" + askbidInforCurrentCode + ": Response Request Data, Error Code: " + e.sErrorCode);

                if (askbidInforCurrentCode != "")
                {
                    List<StockData.DataStruct.AskBidPrice> askbidList = GetAskBidData(e, askbidInforCurrentCode);
                    SetAskBidStockData(new List<object> { askbidList, e.sPrevNext });
                }
            }

            if (e.sRQName.StartsWith("업종일봉조회요청"))
            {
                int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);

                StockLog.Logger.LOG.WriteLog("Test", "Test1: " + e.sTrCode + ", " + e.sRQName);
                List<StockData.DataStruct.IndexDataDay> indexDataList = new List<StockData.DataStruct.IndexDataDay>();
                for (int i = 0; i < nCnt; i++)
                {
                    StockData.DataStruct.IndexDataDay indexData = new StockData.DataStruct.IndexDataDay();

                    /*
    현재가
    거래량
    일자
    시가
    고가
    저가
    거래대금
    대업종구분
    소업종구분
    종목정보
    전일종가


    거래량은 천단위, 거래대금은 백만단위 입니다. - 키움증권 API
    */


                    DateTime dt = DateTime.Now;
                    //indexData.wk_dt = dt.ToString("yyyyMMdd");
                    indexData.wk_dt = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "일자").Trim();
                    indexData.index_code = e.sRQName.Substring(e.sRQName.Length-3);


                    if (double.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "현재가").Trim().Replace("+", "").Replace("-", ""), out double index_value))
                    {
                        indexData.index_value = index_value;
                    }

                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래량").Trim(), out int volume))
                    {
                        indexData.volume = volume;
                    }

                    if (double.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "시가").Trim(), out double open))
                    {
                        indexData.open = open;
                    }

                    if (double.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "고가").Trim().Replace("+", ""), out double high))
                    {
                        indexData.high = high;
                    }

                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "저가").Trim(), out int low))
                    {
                        indexData.low = low;
                    }

                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래대금").Trim(), out int trade_cost))
                    {
                        indexData.trade_cost = trade_cost;
                    }

                    indexDataList.Add(indexData);


                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 현재가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "현재가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 거래량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래량").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 일자: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "일자").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 시가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "시가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 고가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "고가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 저가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "저가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 거래대금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래대금").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 대업종구분: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "대업종구분").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 소업종구분: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "소업종구분").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 종목정보: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목정보").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 전일종가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "전일종가").Trim());
                    break;

                }

                if (nCnt > 0)
                {

                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        MySqlConnection connection = null;
                        string sql = "";
                        try
                        {

                            connection = mDBFactory.Connect();

                            if (connection == null)
                            {
                                StockLog.Logger.LOG.WriteLog("Error", "[UpdateSecRealTimeData]: Database Connection Fail");
                                return;
                            }

                            sql = StockData.Singleton.Store.GetInsertMultiQueryIndexDataDay("tb_stock_price_index_day", indexDataList);

                            mDBFactory.Execute(connection, sql, 60);
                            connection.Close();
                        }
                        catch (Exception exception)
                        {
                            StockLog.Logger.LOG.WriteLog("Exception", "Exception SQL: " + sql);
                            StockLog.Logger.LOG.WriteLog("Exception", exception.ToString());
                            if (connection != null)
                            {
                                connection.Close();
                            }
                            return;
                        }
                    });
                }
            }

            if (e.sRQName.StartsWith("전업종지수요청"))
            {
                int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);
                
                //GetInsertMultiQueryIndexData
                List<StockData.DataStruct.IndexData> indexDataList = new List<StockData.DataStruct.IndexData>();
                for (int i = 0; i < nCnt; i++)
                {
                    StockData.DataStruct.IndexData indexData = new StockData.DataStruct.IndexData();

                    DateTime dt = DateTime.Now;
                    indexData.wk_dt = dt.ToString("yyyyMMdd");
                    //indexData.wk_tm = dt.ToString("HHmmss");
                    indexData.wk_tm = dt.ToString("HH0000");
                    indexData.index_code = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목코드").Trim();
                    indexData.index_name = axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목명").Trim();


                    if (double.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "현재가").Trim().Replace("+", "").Replace("-", ""), out double index_value))
                    {
                        indexData.index_value = index_value;
                    }

                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "대비기호").Trim(), out int cost_symbol))
                    {
                        indexData.cost_symbol = cost_symbol;
                    }

                    if (double.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "전일대비").Trim(), out double net_change))
                    {
                        indexData.net_change = net_change;
                    }

                    if (double.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "등락률").Trim().Replace("+", ""), out double pct_change))
                    {
                        indexData.pct_change = pct_change;
                    }

                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래량").Trim(), out int trade_volume))
                    {
                        indexData.trade_volume = trade_volume;
                    }

                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래대금").Trim(), out int trade_cost))
                    {
                        indexData.trade_cost = trade_cost;
                    }

                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "상한").Trim(), out int higher_limit))
                    {
                        indexData.higher_limit = higher_limit;
                    }

                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "상승").Trim(), out int higher))
                    {
                        indexData.higher = higher;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "보합").Trim(), out int flat))
                    {
                        indexData.flat = flat;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "하락").Trim(), out int lower))
                    {
                        indexData.lower = lower;
                    }
                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "하한").Trim(), out int lower_limit))
                    {
                        indexData.lower_limit = lower_limit;
                    }

                    if (int.TryParse(axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "상장종목수").Trim(), out int listed_item))
                    {
                        indexData.listed_item = listed_item;
                    }

                    indexDataList.Add(indexData);

                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 종목코드: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목코드").Trim());
                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 종목명: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목명").Trim());
                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 현재가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "현재가").Trim());
                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 대비기호: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "대비기호").Trim());
                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 전일대비: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "전일대비").Trim());
                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 등락률: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "등락률").Trim());
                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 거래량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래량").Trim());
                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 비중: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "비중").Trim());
                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 거래대금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래대금").Trim());
                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 상한: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "상한").Trim());
                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 상승: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "상승").Trim());
                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 보합: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "보합").Trim());
                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 하락: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "하락").Trim());
                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 하한: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "하한").Trim());
                    //StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 상장종목수: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "상장종목수").Trim());
                }

                if (nCnt > 0)
                {

                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        MySqlConnection connection = null;
                        string sql = "";
                        try
                        {

                            connection = mDBFactory.Connect();

                            if (connection == null)
                            {
                                StockLog.Logger.LOG.WriteLog("Error", "[UpdateSecRealTimeData]: Database Connection Fail");
                                return;
                            }

                            sql = StockData.Singleton.Store.GetInsertMultiQueryIndexData("tb_stock_index_price", indexDataList);

                            mDBFactory.Execute(connection, sql);

                        }
                        catch (Exception exception)
                        {
                            StockLog.Logger.LOG.WriteLog("Exception", "Exception SQL: " + sql);
                            StockLog.Logger.LOG.WriteLog("Exception", exception.ToString());
                            if (connection != null)
                            {
                                connection.Close();
                            }
                            return;
                        }
                    });
                }
                
            }

            if (e.sRQName == "업종현재가요청")
            {
                int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);

                for (int i = 0; i < nCnt; i++)
                {
                    StockLog.Logger.LOG.WriteLog("Test", "[업종현재가요청]: 시간: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "시간").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종현재가요청]: 현재가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "현재가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종현재가요청]: 전일대비기호: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "전일대비기호").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종현재가요청]: 전일대비: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "전입대비").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종현재가요청]: 등락률: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "등락률").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종현재가요청]: 거래량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래량").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종현재가요청]: 누적거래량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "누적거래량").Trim());
                }
            }

            if (e.sRQName == "업종일봉조회요청")
            {
                int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);

                for (int i = 0; i < nCnt; i++)
                {
                    StockLog.Logger.LOG.WriteLog("Test", "[업종일봉조회요청]: 현재가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "현재가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종일봉조회요청]: 거래량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래량").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종일봉조회요청]: 일자: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "일자").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종일봉조회요청]: 시가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "시가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종일봉조회요청]: 고가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "고가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종일봉조회요청]: 저가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "저가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종일봉조회요청]: 거래대금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래대금").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종일봉조회요청]: 대업종구분: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "대업종구분").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종일봉조회요청]: 소업종구분: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "소업종구분").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종일봉조회요청]: 종목정보: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목정보").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[업종일봉조회요청]: 전일종가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "전일종가").Trim());
                }
            }
            //주식일봉차트조회요청
            if (e.sRQName == "주식일봉차트조회요청")
            {
                int nCnt = axKHOpenAPI1.GetRepeatCnt(e.sTrCode, e.sRQName);

                for (int i = 0; i < nCnt; i++)
                {
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName +"]: 종목코드: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목코드").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName +"]: 현재가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "현재가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName +"]: 거래량: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래량").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName +"]: 거래대금: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "거래대금").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName +"]: 일자: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "일자").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName +"]: 시가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "시가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName +"]: 고가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "고가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName +"]: 저가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "저가").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName +"]: 수정주가구분: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "수정주가구분").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName +"]: 수정비율: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "수정비율").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName +"]: 대업종구분: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "대업종구분").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName +"]: 소업종구분: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "소업종구분").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName +"]: 종목정보: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "종목정보").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName +"]: 수정주가이벤트: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "수정주가이벤트").Trim());
                    StockLog.Logger.LOG.WriteLog("Test", "[" + e.sRQName + "]: 전일종가: " + axKHOpenAPI1.GetCommData(e.sTrCode, e.sRQName, i, "전일종가").Trim());
                }
            }

        }

        private void AxKHOpenAPI1_OnReceiveMsg(object sender, _DKHOpenAPIEvents_OnReceiveMsgEvent e)
        {
            StockLog.Logger.LOG.WriteLog("Console", "AxKHOpenAPI1_OnReceiveMsg: " + e.sRQName);
            StockLog.Logger.LOG.WriteLog("Console", "AxKHOpenAPI1_OnReceiveMsg: " + e.sMsg);
        }

        private void AxKHOpenAPI1_OnReceiveRealData(object sender, _DKHOpenAPIEvents_OnReceiveRealDataEvent e)
        {
            DateTime dt = DateTime.Now;
            string curDT = dt.ToString("HHmmss");
            int rt_indx_tmp = -1;

            if (e.sRealType == "주식체결" || e.sRealType == "주식호가잔량")
            {
                lock (rtIndexLock)
                {
                    if (rt_lastTM != curDT)
                    {
                        rt_lastTM = curDT;
                        rt_index = 1;
                    }
                    rt_index = rt_index + 1;
                    rt_indx_tmp = rt_index;
                }
            }

            if (e.sRealType == "주식체결")
            {

                if (!StockData.Singleton.Store.StockRealTimeData.TryGetValue(e.sRealKey, out StockData.DataStruct.RealTimeData_v2 realTimeData))
                {
                    realTimeData = new StockData.DataStruct.RealTimeData_v2();
                    realTimeData.ITM_C = e.sRealKey;
                    StockData.Singleton.Store.StockRealTimeData[e.sRealKey] = realTimeData;
                    StockLog.Logger.LOG.WriteLog("Console", "[GetRealData] Create New Code:" + e.sRealKey);
                }
                //if (StockData.Singleton.Store.KosdaqList.Contains(e.sRealKey) || StockData.Singleton.Store.ETFList.Contains(e.sRealKey))
                //{ return; }

                string dataBuf = axKHOpenAPI1.GetCommRealData(e.sRealType, 10).Trim(pmfilter);
                if (dataBuf != null && dataBuf != "")
                {
                    realTimeData.PRICE = dataBuf;
                }

                dataBuf = axKHOpenAPI1.GetCommRealData(e.sRealType, 20);
                if (dataBuf != null && dataBuf != "")
                {
                    realTimeData.WK_TM_REAL = dataBuf;
                }

                dataBuf = axKHOpenAPI1.GetCommRealData(e.sRealType, 15).Trim('+');
                dataBuf = dataBuf.Replace("--", "-");
                if (dataBuf != null && dataBuf != "")
                {
                    realTimeData.VOLUME = dataBuf;
                }

                dataBuf = axKHOpenAPI1.GetCommRealData(e.sRealType, 13);
                if (dataBuf != null && dataBuf != "")
                {
                    realTimeData.ACC_VOLUME = dataBuf;
                }

                dataBuf = axKHOpenAPI1.GetCommRealData(e.sRealType, 14);
                if (dataBuf != null && dataBuf != "")
                {
                    realTimeData.ACC_AM = dataBuf;
                }

                dataBuf = axKHOpenAPI1.GetCommRealData(e.sRealType, 228);
                if (dataBuf != null && dataBuf != "")
                {
                    realTimeData.VOLUME_POWER = dataBuf;
                }

                realTimeData.WK_DT = dt.ToString("yyyy-MM-dd");
                realTimeData.WK_TM = dt.ToString("HHmmss");
                realTimeData.WK_TM_MILLI = dt.ToString("fff");

                realTimeData.SEQ = rt_indx_tmp;

                lock (tickLock)
                {
                    tickValueList.Add(realTimeData.GetInsertQuery());
                }
                //}
            }
            else if (e.sRealType == "주식호가잔량")
            {

                if (StockData.Singleton.Store.Account_Privileges.ask_bid_update_rt == "1" || StockData.Singleton.Store.Account_Privileges.ask_bid_update_rt_sub == "1")
                {
                    if (!StockData.Singleton.Store.AskBidData.TryGetValue(e.sRealKey, out StockData.DataStruct.AskBidPrice askBidData))
                    {
                        askBidData = new StockData.DataStruct.AskBidPrice();
                        askBidData.stock_code = e.sRealKey;
                        StockData.Singleton.Store.AskBidData[e.sRealKey] = askBidData;
                        StockLog.Logger.LOG.WriteLog("Console", "[GetAskBid] Create New Code:" + e.sRealKey);
                    }
                    //if (StockData.Singleton.Store.KosdaqList.Contains(e.sRealKey) || StockData.Singleton.Store.ETFList.Contains(e.sRealKey))
                    //{ return; }
                    //StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] 호가시간: " + axKHOpenAPI1.GetCommRealData(e.sRealType, 21));
                    //askBidData.stock_code = e.sRealKey;
                    //askBidData.top_ask = "0";// axKHOpenAPI1.GetCommRealData(e.sRealType, 27);
                    //askBidData.top_bid = "0";//axKHOpenAPI1.GetCommRealData(e.sRealType, 28);

                    askBidData.wk_tm_real = axKHOpenAPI1.GetCommRealData(e.sRealType, 21);

                    askBidData.ask1 = axKHOpenAPI1.GetCommRealData(e.sRealType, 41);
                    askBidData.ask1_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 61);
                    askBidData.ask1_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 81);
                    askBidData.bid1 = axKHOpenAPI1.GetCommRealData(e.sRealType, 51);
                    askBidData.bid1_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 71);
                    askBidData.bid1_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 91);

                    askBidData.ask2 = axKHOpenAPI1.GetCommRealData(e.sRealType, 42);
                    askBidData.ask2_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 62);
                    askBidData.ask2_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 82);
                    askBidData.bid2 = axKHOpenAPI1.GetCommRealData(e.sRealType, 52);
                    askBidData.bid2_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 72);
                    askBidData.bid2_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 92);

                    askBidData.ask3 = axKHOpenAPI1.GetCommRealData(e.sRealType, 43);
                    askBidData.ask3_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 63);
                    askBidData.ask3_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 83);
                    askBidData.bid3 = axKHOpenAPI1.GetCommRealData(e.sRealType, 53);
                    askBidData.bid3_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 73);
                    askBidData.bid3_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 93);

                    askBidData.ask4 = axKHOpenAPI1.GetCommRealData(e.sRealType, 44);
                    askBidData.ask4_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 64);
                    askBidData.ask4_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 84);
                    askBidData.bid4 = axKHOpenAPI1.GetCommRealData(e.sRealType, 54);
                    askBidData.bid4_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 74);
                    askBidData.bid4_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 94);

                    askBidData.ask5 = axKHOpenAPI1.GetCommRealData(e.sRealType, 45);
                    askBidData.ask5_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 65);
                    askBidData.ask5_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 85);
                    askBidData.bid5 = axKHOpenAPI1.GetCommRealData(e.sRealType, 55);
                    askBidData.bid5_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 75);
                    askBidData.bid5_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 95);

                    askBidData.ask6 = axKHOpenAPI1.GetCommRealData(e.sRealType, 46);
                    askBidData.ask6_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 66);
                    askBidData.ask6_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 86);
                    askBidData.bid6 = axKHOpenAPI1.GetCommRealData(e.sRealType, 56);
                    askBidData.bid6_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 76);
                    askBidData.bid6_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 96);

                    askBidData.ask7 = axKHOpenAPI1.GetCommRealData(e.sRealType, 47);
                    askBidData.ask7_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 67);
                    askBidData.ask7_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 87);
                    askBidData.bid7 = axKHOpenAPI1.GetCommRealData(e.sRealType, 57);
                    askBidData.bid7_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 77);
                    askBidData.bid7_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 97);

                    askBidData.ask8 = axKHOpenAPI1.GetCommRealData(e.sRealType, 48);
                    askBidData.ask8_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 68);
                    askBidData.ask8_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 88);
                    askBidData.bid8 = axKHOpenAPI1.GetCommRealData(e.sRealType, 58);
                    askBidData.bid8_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 78);
                    askBidData.bid8_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 98);

                    askBidData.ask9 = axKHOpenAPI1.GetCommRealData(e.sRealType, 49);
                    askBidData.ask9_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 69);
                    askBidData.ask9_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 89);
                    askBidData.bid9 = axKHOpenAPI1.GetCommRealData(e.sRealType, 59);
                    askBidData.bid9_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 79);
                    askBidData.bid9_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 99);

                    askBidData.ask10 = axKHOpenAPI1.GetCommRealData(e.sRealType, 50);
                    askBidData.ask10_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 70);
                    askBidData.ask10_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 90);
                    askBidData.bid10 = axKHOpenAPI1.GetCommRealData(e.sRealType, 60);
                    askBidData.bid10_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 80);
                    askBidData.bid10_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 100);

                    askBidData.total_ask_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 121);
                    askBidData.total_ask_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 122);
                    askBidData.total_bid_qty = axKHOpenAPI1.GetCommRealData(e.sRealType, 125);
                    askBidData.total_bid_qty_ratio = axKHOpenAPI1.GetCommRealData(e.sRealType, 126);

                    askBidData.wk_dt = dt.ToString("yyyy-MM-dd");
                    askBidData.wk_tm = dt.ToString("HHmmss");
                    askBidData.wk_tm_milli = dt.ToString("fff");

                    askBidData.seq = rt_indx_tmp;

                    lock (askbidLock)
                    {
                        askbidValueList.Add(askBidData.GetInsertQuery());
                    }

                }



                //axKHOpenAPI1.GetCommRealData(e.sRealType, 228)
            }
            else if (e.sRealType == "VI발동/해제")
            {
                MySqlConnection connection = null;
                try
                {

                    connection = mDBFactory.Connect();

                    mDBFactory.Execute(connection, "insert into tb_stock_vi_info (" +
                        "stock_code, wk_dt, wk_tm, wk_dtm, stock_name, vi_gubun, stock_group, price, release_time) values (" +
                        "'" + axKHOpenAPI1.GetCommRealData(e.sRealType, 9001) + "'" +
                        "'" + dt.ToString("yyyyMMdd") + "'" +
                        "'" + dt.ToString("HHmmss") + "'" +
                        "'" + dt.ToString("yyyy-MM-dd HH:mm:ss") + "'" +
                        "'" + axKHOpenAPI1.GetCommRealData(e.sRealType, 302) + "'" +
                        "'" + axKHOpenAPI1.GetCommRealData(e.sRealType, 9068) + "'" +
                        "'" + axKHOpenAPI1.GetCommRealData(e.sRealType, 9008) + "'" +
                        "'" + axKHOpenAPI1.GetCommRealData(e.sRealType, 1221) + "'" +
                        "'" + axKHOpenAPI1.GetCommRealData(e.sRealType, 1224) + "'" +
                        ")");


                    connection.Close();
                }
                catch (Exception exception)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", exception.ToString());
                    if (connection != null)
                    {
                        connection.Close();
                    }
                }
            }
            //else if (e.sRealType == "잔고")
            //{
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 9201));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 9001));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 917));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 916));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 302));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 10));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 930));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 931));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 932));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 933));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 945));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 946));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 950));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 951));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 27));
            //    StockLog.Logger.LOG.WriteLog("Test", "[AxKHOpenAPI1_OnReceiveRealData] : " + axKHOpenAPI1.GetCommRealData(e.sRealType, 28));
            //}
            else
            {
                if (!realTypeList.Contains(e.sRealType))
                {
                    StockLog.Logger.LOG.WriteLog("Test", "RealType: " + e.sRealType);
                    realTypeList.Add(e.sRealType);
                }
            }
        }

        private void AxKHOpenAPI1_OnReceiveChejanData(object sender, _DKHOpenAPIEvents_OnReceiveChejanDataEvent e)
        {
            MySqlConnection connection = null;

            try
            {
                StockLog.Logger.LOG.WriteLog("APIOperation", "[AxKHOpenAPI1_OnReceiveChejanData] :" + e.sGubun + ", " + e.sFIdList + ", " + e.nItemCnt);

                string[] strList = e.sFIdList.Split(';');

                if (strList == null)
                {
                    StockLog.Logger.LOG.WriteLog("APIOperation", "[AxKHOpenAPI1_OnReceiveChejanData] : strList == null");
                    return;
                }
                if (e.sGubun == "0")
                {
                    StockData.DataStruct.OrderInformation orderinfo = new StockData.DataStruct.OrderInformation();

                    orderinfo.wk_dtm = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    orderinfo.wk_dt = DateTime.Now.ToString("yyyyMMdd");
                    orderinfo.wk_tm = DateTime.Now.ToString("HHmmss");

                    for (int i = 0; i < strList.Length; i++)
                    {
                        if (int.TryParse(strList[i], out int fid))
                        {
                            StockLog.Logger.LOG.WriteLog("APIOperation", "[AxKHOpenAPI1_OnReceiveChejanData] + " + fid + ", " + GetFIDName(strList[i]) + ", Msg :" + GetFIDValue(fid, orderinfo));
                        }
                        else
                        {
                            StockLog.Logger.LOG.WriteLog("APIOperation", "[AxKHOpenAPI1_OnReceiveChejanData] else faile parse : + " + fid + ", " + GetFIDName(strList[i]));
                        }
                    }


                    if (orderinfo.sell_gubun.Contains("보통") && orderinfo.order_state == "접수" && orderinfo.order_qty != "0")
                    {

                        //지정가 매수 접수 후 5초이내 체결되지 않을 시 취소
                        if (orderinfo.order_gubun.Trim(pmfilter) == "매수")
                        {
                            lock (limitOrderLock)
                            {
                                limitOrderBidList[orderinfo.order_num] = orderinfo;
                            }
                        }


                        //지정가 매도 접수 후 2초이내 체결되지 않을 시 시장가
                        if (orderinfo.order_gubun.Trim(pmfilter) == "매도")
                        {
                            lock (limitOrderLock)
                            {
                                limitOrderAskList[orderinfo.order_num] = orderinfo;
                            }
                        }
                    }


                    if (orderinfo.sell_gubun == "보통" && orderinfo.order_state == "체결")
                    {
                        //지정가 매수 접수 후 5초이내 체결되었을 시 처리
                        if (orderinfo.order_gubun.Trim(pmfilter) == "매수")
                        {
                            lock (limitOrderLock)
                            {
                                //만약 부분 체결된 것을 취소나 정정할 경우 여기에 조건문으로 오더수랑 체결수 같은 애들만 제거하고 제거하는 부분에서
                                //dic_recentOrder 데이터를 가져와서 부분을 수정하면 될 것 같습니다.
                                limitOrderBidList.Remove(orderinfo.order_num);
                            }
                        }


                        //지정가 매도 접수 후 2초이내 체결되었을 시 처리
                        if (orderinfo.order_gubun.Trim(pmfilter) == "매도")
                        {
                            lock (limitOrderLock)
                            {
                                limitOrderAskList.Remove(orderinfo.order_num);
                            }
                        }
                    }


                    //현재 보유 종목을 체크하기 위한 딕셔너리
                    if (orderinfo.order_state == "체결")
                    {
                        if (orderinfo.order_gubun.Trim(pmfilter) == "매수")
                        {
                            lock (recentOrderLock)
                            {
                                dic_recentOrder[orderinfo.stock_code.Replace("A", "")] = orderinfo;
                            }
                        }

                        if (orderinfo.order_gubun.Trim(pmfilter) == "매도")
                        {

                            if (orderinfo.order_qty == orderinfo.trading_qty)
                            {
                                lock (recentOrderLock)
                                {
                                    dic_recentOrder.Remove(orderinfo.stock_code.Replace("A", ""));
                                }
                            }

                        }
                    }


                    connection = mDBFactory.Connect();

                    if (connection == null)
                    {
                        StockLog.Logger.LOG.WriteLog("Error", "DB Connection Fail: [AxKHOpenAPI1_OnReceiveChejanData]");
                        return;
                    }


                    mDBFactory.Execute(connection, StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.OrderInformation>("tb_stock_order_info", orderinfo));

                    if (orderinfo.order_state == "체결")
                    {
                        if (orderinfo.order_gubun.Trim(pmfilter) == "매도")
                        {
                            mDBFactory.Execute(connection, "update tb_account_bsc set acc_krw_bac=acc_krw_bac+" + orderinfo.current_price.Trim(pmfilter) + " where acc_name='" + StockData.Singleton.Store.Account.ID + "'");

                            if (orderinfo.order_qty == orderinfo.trading_qty)
                            {
                                //StockLog.Logger.LOG.WriteLog("Test", "ask history");
                                mDBFactory.Execute(connection, "delete from tb_stock_hold_v2 where account='" + orderinfo.account + "' and stock_code='" + orderinfo.stock_code + "'");
                            }
                        }
                        else if (orderinfo.order_gubun.Trim(pmfilter) == "매수")
                        {
                            //StockLog.Logger.LOG.WriteLog("Test", "bid history");
                            //
                            mDBFactory.Execute(connection, "update tb_account_bsc set acc_krw_bac=acc_krw_bac-" + orderinfo.current_price.Trim(pmfilter) + " where acc_name='" + StockData.Singleton.Store.Account.ID + "'");
                            mDBFactory.Execute(connection, StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.OrderInformation>("tb_stock_hold_v2", orderinfo));
                        }
                    }

                    //tb_stock_hold

                    //DataTable dataTable = mDBFactory.ExecuteDataTable(connection, "select * FROM tb_stock_hold WHERE WK_DT=(select wk_dt from tb_stock_hold where client_id='" + StockData.Singleton.Store.Account.ID + "') and client_id='" + StockData.Singleton.Store.Account.ID + "'");

                    //if (dataTable.Rows.Count > 0)
                    //{
                    //    for (int i = 0; i < dataTable.Rows.Count; i++)
                    //    {
                    //        StockData.DataStruct.HoldStock cs = new StockData.DataStruct.HoldStock();
                    //        td.CLIENT_ID = StockData.Singleton.Store.Account.ID;
                    //        StockData.Singleton.Store.GetDataTable<StockData.DataStruct.HoldStock>(dataTable.Rows[i], td);

                    //        if (orderinfo.stock_code.TrimStart('A') == td.ITM_C)
                    //        {
                    //            if (orderinfo.order_state == "체결" && orderinfo.order_gubun.Trim(pmfilter) == "매도")
                    //            {
                    //                if (int.TryParse(orderinfo.order_qty, out int order_qty) && int.TryParse(td.NUM, out int num))
                    //                {
                    //                    td.NUM = (num - order_qty).ToString();

                    //                    if (td.NUM == "0")
                    //                    {
                    //                        td.STS_DSC = "2";
                    //                    }

                    //                    td.WK_DTM = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    //                    mDBFactory.Execute(connection, StockData.Singleton.Store.MakeInsertUpdateQuery<StockData.DataStruct.TradeData>("tb_stock_trade", td));
                    //                }
                    //                else
                    //                {
                    //                    StockLog.Logger.LOG.WriteLog("Error", "[AxKHOpenAPI1_OnReceiveChejanData] qty parsing error");
                    //                }
                    //            }
                    //        }
                    //    }
                    //}

                    connection.Close();
                }
                else
                {
                    for (int i = 0; i < strList.Length; i++)
                    {
                        if (int.TryParse(strList[i], out int fid))
                        {
                            StockLog.Logger.LOG.WriteLog("APIOperation", "[AxKHOpenAPI1_OnReceiveChejanData] + " + fid + ", " + GetFIDName(strList[i]) + ", Msg :" + axKHOpenAPI1.GetChejanData(fid));
                        }
                        else
                        {
                            StockLog.Logger.LOG.WriteLog("APIOperation", "[AxKHOpenAPI1_OnReceiveChejanData] else faile parse : + " + fid + ", " + GetFIDName(strList[i]));
                        }
                    }
                }
            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());

                if (connection != null)
                {
                    connection.Close();
                }
            }
        }

        private void TimeChecker(Action act, string name)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            act();
            sw.Stop();
            StockLog.Logger.LOG.WriteLog("Stopwatch", "[" + name + "] Process Time: " + sw.ElapsedMilliseconds);

        }

        private bool APIExecute(Func<bool> func, string rqName)
        {
            apiWatch.Restart();

            if (rqName != null && rqName != "")
            {
                lock (reqLock)
                {
                    RQCheckBox[rqName] = DateTime.Now;
                }
            }
            apiTimeOut.Set();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                apiTimeOut.Reset();
                if (!apiTimeOut.WaitOne(5000))
                {
                    StockLog.Logger.LOG.WriteLog("System", "Kiwoom API Function Timeout: " + rqName);
                    StockData.Singleton.Store.QueryLogSend("System", "API Timeout. " + rqName);

                    RestartEvent?.Invoke();
                }
            });

            while (apiTimeOut.WaitOne(1))
            {
                //wait check thread run...
            }

            bool result = func();
            apiTimeOut.Set();

            apiWatch.Stop();
            StockLog.Logger.LOG.WriteLog("Stopwatch", "[APIExecute] Process Time: " + apiWatch.ElapsedMilliseconds);
            return result;

        }
    }
}







