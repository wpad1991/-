using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace Prost
{
    public delegate void ProcessRestartDelegate();
    public delegate void ProcessExitDelegate();

    public partial class ProstForm : Form
    {
        Prost.Controls.Pages.MainWindow mainWindow = new Controls.Pages.MainWindow();

        public event ProcessExitDelegate ExitEvent = null;
        public event ProcessRestartDelegate RestartEvent = null;

        public ProstForm()
        {
            InitializeComponent();

            StockLog.Logger.LOG.WriteLog("System", "Start Program");

            this.Load += Prost_Load;

            StockData.Singleton.Store.LoadSetting();

            mDBFactory = new DBManager.MariaDBFactory(StockData.Singleton.Store.AccessInfo.Url, StockData.Singleton.Store.AccessInfo.Port, StockData.Singleton.Store.AccessInfo.User, StockData.Singleton.Store.AccessInfo.Password, StockData.Singleton.Store.AccessInfo.DBName);

            StockLog.Logger.LOG.logFilter.Add("Console");
            StockLog.Logger.LOG.logFilter.Add("Exception");
            StockLog.Logger.LOG.logFilter.Add("Error");
            StockLog.Logger.LOG.logFilter.Add("TradeLog");
            StockLog.Logger.LOG.logFilter.Add("APIOperation");
            StockLog.Logger.LOG.logFilter.Add("System");
            StockLog.Logger.LOG.logFilter.Add("Stopwatch");
            StockLog.Logger.LOG.logFilter.Add("Timer");
            StockLog.Logger.LOG.logFilter.Add("Test");
            StockLog.Logger.LOG.logFilter.Add("PythonLog");

            loginToolStripMenuItem.Click += LoginToolStripMenuItem_Click;
            showToolStripMenuItem.Click += ShowToolStripMenuItem_Click;
            exitToolStripMenuItem.Click += ExitToolStripMenuItem_Click;

            axKHOpenAPI1.OnReceiveMsg += AxKHOpenAPI1_OnReceiveMsg;
            axKHOpenAPI1.OnEventConnect += onEventConnect;
            axKHOpenAPI1.OnReceiveTrData += onReceiveTrData;
            axKHOpenAPI1.OnReceiveChejanData += AxKHOpenAPI1_OnReceiveChejanData;
            axKHOpenAPI1.OnReceiveRealData += AxKHOpenAPI1_OnReceiveRealData;
            axKHOpenAPI1.CommConnect();

            mainWindow.administratorPage.buttonClicked += AdministratorPage_buttonClicked;
            mainWindow.backTestPage.buttonClicked += AdministratorPage_buttonClicked;
            mainWindow.Show();

            StartHeartBit();

            try
            {
                string externalIpString = new System.Net.WebClient().DownloadString("http://icanhazip.com").Replace("\\r\\n", "").Replace("\\n", "").Trim();
                var externalIp = System.Net.IPAddress.Parse(externalIpString);

                StockData.Singleton.Store.ExternalIPAddress = externalIp.ToString();

                StockData.Singleton.Store.QueryLogSend("System", "Start Program.");

            }
            catch (Exception except)
            {
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
            }

            int timeout = 60000;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (!loginRestartEvent.WaitOne(timeout))
                {
                    StockLog.Logger.LOG.WriteLog("System", "Kiwoom Login Connection Timeout");
                    RestartEvent?.Invoke();
                }
            });
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExitEvent?.Invoke();
        }

        private void AdministratorPage_buttonClicked(string group, string command)
        {
            StockLog.Logger.LOG.WriteLog("ButtonClickEvent", command);
            Console.WriteLine(command);

            switch (command)
            {
                case "GetDailyData":
                    BatchDailyStockData(null);
                    break;
                case "teest1":
                    test1();
                    break;
                case "GetPostgres":
                    break;
                case "Test":
                    if (group == "BackTest")
                    {
                        test_python();
                    }
                    else
                    {
                        TestFunc();
                    }
                    break;
                case "AccountCheck":
                    AccountCheck();
                    break;
                case "BuyTest":
                    BuyTest();
                    break;
                case "SellTest":
                    SellTest();
                    break;
                case "BuyListCheck":
                    BuyListCheck();
                    break;
                case "AccountJudge":
                    AccountJudge();
                    break;
                case "MultiRequest":
                    MultiRequest();
                    break;
                case "AccountCheck_Detail":
                    AccountCheck_Detail();
                    break;
                case "CreateTest1":
                    CreateTest1();
                    break;
                case "InformationChecker":
                    InformationChecker(null);
                    break;
                case "BuySellCheck":
                    BuySellCheck(null);
                    break;
                case "GetDailyList":
                    List<string> test = GetBasicStockList();
                    StockLog.Logger.LOG.WriteLog("Console", "GetDailyList: " + test.Count);
                    break;
                case "MinbongTest":
                    MinbongTest();
                    break;
                case "MinbongLoop":
                    BatchMinuteStockData(null);
                    break;
                case "LoopStop":
                    loopStop = true;
                    break;
                case "Restart":
                    RestartEvent?.Invoke();
                    break;
                case "Exit":
                    ExitEvent?.Invoke();
                    break;
                case "AskBidBatch":
                    BatchAskBidStockData(null);
                    break;
                case "TickLoop":
                    BatchTickStockData(null);
                    break;
                case "RequestBasic":
                    RequestStockBasicInformation("000020");
                    break;
                case "Logout":
                    axKHOpenAPI1.CommTerminate();
                    break;
                case "BasicLoop":
                    BatchStockBasic(null);
                    break;
                case "chegul_test":
                    chegul_test();
                    break;
                case "mechegul_test":
                    mechegul_test();
                    break;
                case "InsertSampleLogic":
                    InsertSampleLogic();
                    break;
                case "Bug1Test":
                    Bug1Test();
                    break;
                case "LoadRtData":
                    ThreadPool.QueueUserWorkItem(_ => {
                        LoadRtData(null);
                    });
                    break;
                case "opt20001":
                    opt20001();
                    break;
                case "opt20003":
                    opt20003();
                    break;
                case "opt20005":
                    opt20005();
                    break;
                case "opt20006":
                    opt20006(null);
                    break;
                case "opt20081":
                    opt20081();
                    break;
                case "StartHeartBit":
                    StartHeartBit();
                    break;
            }
        }

        private void opt20001()
        {
            axKHOpenAPI1.SetInputValue("시장구분", "0");
            axKHOpenAPI1.SetInputValue("업종코드", "001");
            axKHOpenAPI1.CommRqData("업종현재가요청", "OPT20001", 0, StockData.Singleton.Store.GetScreenNumber());
        }

        private void opt20003()
        {

            //업종코드 = 001:종합(KOSPI), 002:대형주, 003:중형주, 004:소형주 101:종합(KOSDAQ), 201:KOSPI200, 302:KOSTAR, 701: KRX100 나머지 ※ 업종코드 참고
            List<string> industryCode = new List<string>();
            industryCode.Add("001");
            industryCode.Add("002");
            industryCode.Add("003");
            industryCode.Add("004");
            industryCode.Add("101");
            industryCode.Add("201");
            //industryCode.Add("302");
            industryCode.Add("701");
            
            for (int i=0;i< industryCode.Count;i++) {

                StockLog.Logger.LOG.WriteLog("Test", "Request oPT20003: " + industryCode[i]);
                axKHOpenAPI1.SetInputValue("업종코드", industryCode[i]);
                axKHOpenAPI1.CommRqData("전업종지수요청" + industryCode[i], "OPT20003", 0, StockData.Singleton.Store.GetScreenNumber());
            }
        }

        private void opt20005()
        {

            /*
[ opt20005 : 업종분봉조회요청 ]

 [ 주의 ] 
 데이터 건수를 지정할 수 없고, 데이터 유무에따라 한번에 최대 900개가 조회됩니다.

 1. Open API 조회 함수 입력값을 설정합니다.
	업종코드 = 001:종합(KOSPI), 002:대형주, 003:중형주, 004:소형주 101:종합(KOSDAQ), 201:KOSPI200, 302:KOSTAR, 701: KRX100 나머지 ※ 업종코드 참고
	SetInputValue("업종코드"	,  "입력값 1");

	틱범위 = 1:1틱, 3:3틱, 5:5틱, 10:10틱, 30:30틱
	SetInputValue("틱범위"	,  "입력값 2");


 2. Open API 조회 함수를 호출해서 전문을 서버로 전송합니다.
	CommRqData( "RQName"	,  "opt20005"	,  "0"	,  "화면번호"); 

             */

            List<string> indexList = new List<string>();
            indexList.Add("001");
            indexList.Add("002");
            indexList.Add("003");
            indexList.Add("004");
            indexList.Add("101");
            indexList.Add("201");
            //indexList.Add("302");
            indexList.Add("701");

            for (int i = 0; i < indexList.Count; i++)
            {
                
                axKHOpenAPI1.SetInputValue("업종코드", indexList[i]);
                axKHOpenAPI1.SetInputValue("틱범위", "1:1틱");
                axKHOpenAPI1.CommRqData("업종분봉조회요청" + indexList[i], "opt20005", 0, StockData.Singleton.Store.GetScreenNumber());
                Thread.Sleep(500);
            }
        }

        private bool opt20006(string[] args)
        {

            /*
           

 [ opt20006 : 업종일봉조회요청 ]

 [ 주의 ] 
 데이터 건수를 지정할 수 없고, 데이터 유무에따라 한번에 최대 600개가 조회됩니다.

 1. Open API 조회 함수 입력값을 설정합니다.
	업종코드 = 001:종합(KOSPI), 002:대형주, 003:중형주, 004:소형주 101:종합(KOSDAQ), 201:KOSPI200, 302:KOSTAR, 701: KRX100 나머지 ※ 업종코드 참고
	SetInputValue("업종코드"	,  "입력값 1");

	기준일자 = YYYYMMDD (20160101 연도4자리, 월 2자리, 일 2자리 형식)
	SetInputValue("기준일자"	,  "입력값 2");


 2. Open API 조회 함수를 호출해서 전문을 서버로 전송합니다.
	CommRqData( "RQName"	,  "opt20006"	,  "0"	,  "화면번호"); 

*/
            ThreadPool.QueueUserWorkItem(_ =>
            {
                List<string> indexList = new List<string>();
                indexList.Add("001");
                indexList.Add("002");
                indexList.Add("003");
                indexList.Add("004");
                indexList.Add("101");
                indexList.Add("201");
                indexList.Add("302");
                indexList.Add("701");

                for (int i = 0; i < indexList.Count; i++)
                {
                    StockLog.Logger.LOG.WriteLog("Test", "Request oPT20006: " + indexList[i]);
                    axKHOpenAPI1.SetInputValue("업종코드", indexList[i]);
                    axKHOpenAPI1.SetInputValue("기준일자", DateTime.Now.ToString("yyyyMMdd"));
                    axKHOpenAPI1.CommRqData("업종일봉조회요청" + indexList[i], "OPT20006", 0, StockData.Singleton.Store.GetScreenNumber());
                    Thread.Sleep(1000);
                }
            });
            return true;
        }

        private void opt20081()
        {
            axKHOpenAPI1.SetInputValue("종목코드", "005930");
            axKHOpenAPI1.SetInputValue("기준일자", "20230313");
            axKHOpenAPI1.CommRqData("주식일봉차트조회요청", "OPT10081", 0, StockData.Singleton.Store.GetScreenNumber());
        }

        private void Bug1Test()
        {

            limitOrderAskList["test"] = new StockData.DataStruct.OrderInformation();

            requestChecker.CheckTime(DateTime.Now);
            requestChecker.CheckTime(DateTime.Now);
            requestChecker.CheckTime(DateTime.Now);
            requestChecker.CheckTime(DateTime.Now);
            requestChecker.CheckTime(DateTime.Now);

            RequestCheck(null);


        }

        private void test1()
        {

            /*
            MySqlConnector conntion = null;
            connection = mDBFactory.Connect();

            mDBFactory.Execute(connection, @"
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
            mDBFactory.Execute(connection, "truncate table pertest.tb_stock_price_tick_sub");

            connection.Clone();
            */
        }

        private void AccountJudge()
        {
            axKHOpenAPI1.SetInputValue("계좌번호", StockData.Singleton.Store.Account.MainAccount);
            axKHOpenAPI1.SetInputValue("비밀번호", "");
            axKHOpenAPI1.SetInputValue("상장폐지조회구분", "0");
            axKHOpenAPI1.SetInputValue("비밀번호입력매체구분", "00");
            axKHOpenAPI1.CommRqData("계좌평가현황요청", "OPW00004", 0, StockData.Singleton.Store.GetScreenNumber());
        }

        private void InsertSampleLogic()
        {
            MySqlConnector.MySqlConnection connection = null;
            try
            {
                connection = mDBFactory.Connect();

                StockData.DataStruct.LogicRun lr = new StockData.DataStruct.LogicRun();

                lr.client_id = StockData.Singleton.Store.Account.ID;
                lr.starttime = DateTime.Now;
                lr.endtime = DateTime.Now;
                lr.code_id = "0";
                lr.state = "idle";
                lr.input_data = "";
                lr.output_data = "";
                lr.print = "";

                string sql = "insert into tb_logic_run (client_id, starttime, endtime, code_id, state, input_data, output_data, print) values('"
                    + lr.client_id + "','"
                    + lr.starttime.ToString("yyyy-MM-dd HH:mm:ss") + "','"
                    + lr.endtime.ToString("yyyy-MM-dd HH:mm:ss") + "','"
                    + lr.code_id + "','"
                    + lr.state + "','"
                    + lr.input_data + "','"
                    + lr.output_data + "','"
                    + lr.print + "')";

                mDBFactory.Execute(connection, sql);

                connection.Close();

            }
            catch (Exception except)
            {
                if (connection != null)
                {
                    connection.Close();
                }
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
            }
        }

        private void test_python()
        {

        }


        private void chegul_test()
        {
            axKHOpenAPI1.SetInputValue("계좌번호", StockData.Singleton.Store.Account.MainAccount);
            axKHOpenAPI1.SetInputValue("전체종목구분", "0");
            axKHOpenAPI1.SetInputValue("매매구분", "0");
            axKHOpenAPI1.SetInputValue("종목코드", String.Join(";", StockData.Singleton.Store.HoldStockCode) + ";");
            axKHOpenAPI1.CommRqData("체결요청", "opt10075", 0, StockData.Singleton.Store.GetScreenNumber());
        }

        private void mechegul_test()
        {
            axKHOpenAPI1.SetInputValue("계좌번호", StockData.Singleton.Store.Account.MainAccount);
            axKHOpenAPI1.SetInputValue("전체종목구분", "0");
            axKHOpenAPI1.SetInputValue("매매구분", "0");
            axKHOpenAPI1.SetInputValue("종목코드", String.Join(";", StockData.Singleton.Store.HoldStockCode) + ";");
            axKHOpenAPI1.CommRqData("미체결요청", "opt10076", 0, StockData.Singleton.Store.GetScreenNumber());
        }

        private void TestFunc()
        {
            //axKHOpenAPI1.SetInputValue("종목코드", "005930");
            //axKHOpenAPI1.SetInputValue("틱범위", "1:1분");
            //axKHOpenAPI1.SetInputValue("수정주가구분", "0");
            //axKHOpenAPI1.CommRqData("주식분봉차트조회요청", "opt10080", 0, StockData.Singleton.Store.GetScreenNumber());
            MySqlConnector.MySqlConnection connection = null;
            try
            {
                connection = mDBFactory.Connect();

                DateTime dateNow = DateTime.Now;

                mDBFactory.Execute(connection,
                            "insert into tb_update_time (id, wk_dt, minute_time) values ('main', '" + dateNow.ToString("yyyyMMdd") + "', '" + dateNow.ToString("HHmmssfff") + "') " +
                            "on duplicate key update id='main', wk_dt='" + dateNow.ToString("yyyyMMdd") + "', minute_time='" + dateNow.ToString("HHmmssfff") + "'");

                mDBFactory.Execute(connection,
                            "insert into tb_update_time (id, wk_dt, second_time) values ('main', '" + dateNow.ToString("yyyyMMdd") + "', '" + dateNow.ToString("HHmmssfff") + "') " +
                            "on duplicate key update id='main', wk_dt='" + dateNow.ToString("yyyyMMdd") + "', second_time='" + dateNow.ToString("HHmmssfff") + "'");

                //DataTable dataTable = mDBFactory.ExecuteDataTable(connection, "select * FROM tb_stock_trade WHERE WK_DT=" + DateTime.Now.AddDays(-1).ToString("yyyyMMdd") + " AND STS_DSC=1 AND GUBUN=2 AND CLIENT_ID='" + StockData.Singleton.Store.Account.ID + "'");

                //if (dataTable.Rows.Count > 0)
                //{
                //    List<StockData.DataStruct.TradeData> dataList = new List<StockData.DataStruct.TradeData>();

                //    for (int i = 0; i < dataTable.Rows.Count; i++)
                //    {
                //        StockData.DataStruct.TradeData td = new StockData.DataStruct.TradeData();
                //        td.CLIENT_ID = StockData.Singleton.Store.Account.ID;
                //        StockData.Singleton.Store.GetDataTable<StockData.DataStruct.TradeData>(dataTable.Rows[i], td);

                //        StockLog.Logger.LOG.WriteLog("Console", "Test: " + td.ITM_C);

                //        dataList.Add(td);
                //    }
                //}

                connection.Close();
            }
            catch (Exception except)
            {
                if (connection != null)
                {
                    connection.Close();
                }
                StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
            }
        }

        private void MinbongTest()
        {
            axKHOpenAPI1.SetInputValue("종목코드", "005930");
            axKHOpenAPI1.SetInputValue("틱범위", "1:1분");
            //수정주가구분 = 0 or 1, 수신데이터 1:유상증자, 2:무상증자, 4:배당락, 8:액면분할, 16:액면병합, 32:기업합병, 64:감자, 256:권리락
            axKHOpenAPI1.SetInputValue("수정주가구분", "0");
            axKHOpenAPI1.CommRqData("주식분봉차트조회요청", "opt10080", 0, StockData.Singleton.Store.GetScreenNumber());
        }

        private void CreateTest1()
        {
            string sql = StockData.Singleton.Store.CreateQueryAllString<StockData.DataStruct.OrderInformation>("tb_stock_order_info", new StockData.DataStruct.OrderInformation());
            StockLog.Logger.LOG.WriteLog("Console", sql);

            DBManager.MariaClient mdb = new DBManager.MariaClient(StockData.Singleton.Store.AccessInfo.Url, StockData.Singleton.Store.AccessInfo.Port, StockData.Singleton.Store.AccessInfo.User, StockData.Singleton.Store.AccessInfo.Password, StockData.Singleton.Store.AccessInfo.DBName);
            bool dbSuccess = mdb.Connect();

            if (!dbSuccess)
            {
                StockLog.Logger.LOG.WriteLog("Error", "DB Connection Fail: [getDailyStockData]");
                return;
            }

            mdb.Execute(sql);

            mdb.Close();
        }

        private void MultiRequest()
        {

            //   [CommKwRqData() 함수]

            //   CommKwRqData(
            //BSTR sArrCode,    // 조회하려는 종목코드 리스트
            //BOOL bNext,   // 연속조회 여부 0:기본값, 1:연속조회(지원안함)
            //int nCodeCount,   // 종목코드 갯수
            //int nTypeFlag,    // 0:주식 종목, 3:선물옵션 종목
            //BSTR sRQName,   // 사용자 구분명
            //BSTR sScreenNo    // 화면번호
            //)


            // 한번에 100종목까지 조회할 수 있는 복수종목 조회함수 입니다.
            // 함수인자로 사용하는 종목코드 리스트는 조회하려는 종목코드 사이에 구분자';'를 추가해서 만들면 됩니다.
            // 수신되는 데이터는 TR목록에서 복수종목정보요청(OPTKWFID) Output을 참고하시면 됩니다.
            // ※ OPTKWFID TR은 CommKwRqData()함수 전용으로, CommRqData 로는 사용할 수 없습니다.
            // ※ OPTKWFID TR은 영웅문4 HTS의 관심종목과는 무관합니다.
            List<string> kospiList = StockData.Singleton.Store.KospiList;
            List<string> kosdaqList = StockData.Singleton.Store.KosdaqList;

            List<string> bufList = new List<string>();

            int error = 0;

            for (int i = 0; i < kospiList.Count; i++)
            {
                bufList.Add(kospiList[i]);

                if (bufList.Count >= 100)
                {
                    error = axKHOpenAPI1.CommKwRqData(String.Join(";", bufList), 0, bufList.Count, 0, "복수종목정보요청", StockData.Singleton.Store.GetScreenNumber());
                    KiwoomErrorCatch(error);
                    bufList = new List<string>();
                    return;
                }
            }
            if (bufList.Count > 0)
            {
                error = axKHOpenAPI1.CommKwRqData(String.Join(";", bufList), 0, bufList.Count, 0, "복수종목정보요청", StockData.Singleton.Store.GetScreenNumber());
                KiwoomErrorCatch(error);
                bufList = new List<string>();
            }


            for (int i = 0; i < kosdaqList.Count; i++)
            {
                bufList.Add(kosdaqList[i]);

                if (bufList.Count >= 100)
                {
                    error = axKHOpenAPI1.CommKwRqData(String.Join(";", bufList), 0, bufList.Count, 0, "복수종목정보요청", StockData.Singleton.Store.GetScreenNumber());
                    KiwoomErrorCatch(error);
                    bufList = new List<string>();
                }
            }
            if (bufList.Count > 0)
            {
                error = axKHOpenAPI1.CommKwRqData(String.Join(";", bufList), 0, bufList.Count, 0, "복수종목정보요청", StockData.Singleton.Store.GetScreenNumber());
                KiwoomErrorCatch(error);
                bufList = new List<string>();
            }

            //axKHOpenAPI1.CommKwRqData("", 0, 0, 0, "복수종목정보요청", StockData.Singleton.Store.GetScreenNumber());
        }

        private void BuyListCheck()
        {
            axKHOpenAPI1.SetInputValue("계좌번호", StockData.Singleton.Store.Account.MainAccount);
            int error = axKHOpenAPI1.CommRqData("계좌수익률요청", "opt10085", 0, StockData.Singleton.Store.GetScreenNumber());
            KiwoomErrorCatch(error);
        }

        private void AccountCheck()
        {
            axKHOpenAPI1.SetInputValue("계좌번호", StockData.Singleton.Store.Account.MainAccount);
            axKHOpenAPI1.SetInputValue("비밀번호", "0000");
            axKHOpenAPI1.SetInputValue("비밀번호입력매체구분", "00");
            //axKHOpenAPI1.SetInputValue("조회구분", "2");
            int error = axKHOpenAPI1.CommRqData("체결잔고요청", "opw00005", 0, StockData.Singleton.Store.GetScreenNumber());
            KiwoomErrorCatch(error);
        }

        private void AccountCheck_Detail()
        {
            axKHOpenAPI1.SetInputValue("계좌번호", StockData.Singleton.Store.Account.MainAccount);
            axKHOpenAPI1.SetInputValue("비밀번호", "0000");
            axKHOpenAPI1.SetInputValue("비밀번호입력매체구분", "00");
            axKHOpenAPI1.SetInputValue("조회구분", "2");
            int error = axKHOpenAPI1.CommRqData("예수금상세현황요청", "opw00001", 0, StockData.Singleton.Store.GetScreenNumber());
            KiwoomErrorCatch(error);
        }

        private void BuyTest()
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
            int error = axKHOpenAPI1.SendOrder("종목신규매수", StockData.Singleton.Store.GetScreenNumber(), StockData.Singleton.Store.Account.MainAccount, 1, "005930", 1, 0, "03", "");
            if (error == 0 || error == 1)
            {
                StockLog.Logger.LOG.WriteLog("Console", "Buy Success!");
            }
            KiwoomErrorCatch(error);

            error = axKHOpenAPI1.SendOrder("종목신규매수", StockData.Singleton.Store.GetScreenNumber(), StockData.Singleton.Store.Account.MainAccount, 1, "005380", 1, 0, "03", "");
            if (error == 0 || error == 1)
            {
                StockLog.Logger.LOG.WriteLog("Console", "Buy Success!");
            }
            KiwoomErrorCatch(error);
        }

        private void SellTest()
        {
            int error = axKHOpenAPI1.SendOrder("종목신규매도", StockData.Singleton.Store.GetScreenNumber(), StockData.Singleton.Store.Account.MainAccount, 2, "005930", 1, 0, "03", "");
            if (error == 0 || error == 1)
            {
                StockLog.Logger.LOG.WriteLog("Console", "Buy Success!");
            }
            KiwoomErrorCatch(error);

            error = axKHOpenAPI1.SendOrder("종목신규매도", StockData.Singleton.Store.GetScreenNumber(), StockData.Singleton.Store.Account.MainAccount, 2, "005380", 1, 0, "03", "");
            if (error == 0 || error == 1)
            {
                StockLog.Logger.LOG.WriteLog("Console", "Buy Success!");
            }
            KiwoomErrorCatch(error);
        }

        private void Test()
        {

        }

        private void ShowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //mainWindow.Show();
        }

        private void LoginToolStripMenuItem_Click(object sender, EventArgs e)
        {
            axKHOpenAPI1.CommConnect();
            axKHOpenAPI1.OnReceiveMsg += AxKHOpenAPI1_OnReceiveMsg;
            axKHOpenAPI1.OnEventConnect += onEventConnect;
            axKHOpenAPI1.OnReceiveTrData += onReceiveTrData;
            axKHOpenAPI1.OnReceiveRealData += AxKHOpenAPI1_OnReceiveRealData;
        }

        private void Prost_Load(object sender, EventArgs e)
        {
            this.ShowInTaskbar = false;
            this.Visible = false;
            notifyIcon_Prost.ContextMenuStrip = contextMenu_Prost;
        }
    }
}
