using System;
using System.Collections.Generic;
using System.Text;

namespace StockData
{
    public partial class DataStruct
    {
        public class IndexData
        {
            public string wk_dt;
            public string wk_tm;
            public string index_code;
            public string index_name;
            public double index_value;
            public int cost_symbol;
            public double net_change;
            public double pct_change;
            public int trade_volume;
            public int trade_cost;
            public int higher_limit;
            public int higher;
            public int flat;
            public int lower;
            public int lower_limit;
            public int listed_item;
        }

        public class IndexDataDay
        {
            public string wk_dt;
            public string index_code;
            public double index_value;
            public int volume;
            public double open;
            public double high;
            public double low;
            public int trade_cost;
        }


        public class RealTimeData
        {
            public string ITM_C;            // 주식 코드
            public string WK_DT;        // 현재 날짜
            public string WK_TM;        // 현재 시간
            public double RT_AM;        // 10: 현재가
            public double BF_PRE_AM;    // 11: 전일 대비
            public double RT_RATE;      // 12: 등락률
            public double SEL_AM;       // 27: 매도호가
            public double BUY_AM;       // 28: 매수호가
            public int AC_VOL;          // 13: 누적거래량
            public double AC_TR_AM;     // 14: 누적거래대금
            public double NOW_AM;       // 16: 시가
            public double HIGH_AM;      // 17: 고가
            public double LOW_AM;       // 18: 저가
            public double BF_PRE_VOL;   // 26: 전일거래량
            public double BF_PRE_RT_AM; // 29: 거래대금증감
            public double CHG_AM;       // 31; 거래 회전율
            public double FEE_AM;       // 32: 거래 비용
            public double ITM_VOL;      // 311: 시가 총액
            public string UP_LIM_TM;    // 상한가 발생시간
            public string DOWN_LIM_TM;  // 하한가 발생시간
        }

        public class RealTimeData_v2
        {
            public string ITM_C = "";            // 주식 코드
            public string WK_DT = "";        // 현재 날짜
            public string WK_TM = "";        // 현재 시간
            public int SEQ = 1;
            public string WK_TM_MILLI = "";
            public string WK_TM_REAL = "";        // 체결 시간
            public string VOLUME = "";
            public string PRICE = "";
            public string ACC_VOLUME = "";
            public string ACC_AM = "";
            public string VOLUME_POWER = "";
            StringBuilder sb = new StringBuilder();

            public string GetInsertQuery()
            {
                sb.Clear();

                sb.Append("('");
                sb.Append(WK_DT);
                sb.Append("','");
                sb.Append(WK_TM);
                sb.Append("','");
                sb.Append(ITM_C);
                sb.Append("','");
                sb.Append(SEQ);
                sb.Append("','");
                sb.Append(WK_TM_MILLI);
                sb.Append("','");
                sb.Append(WK_TM_REAL);
                sb.Append("','");

                sb.Append(VOLUME);
                sb.Append("','");
                sb.Append(PRICE);
                sb.Append("','");
                sb.Append(ACC_VOLUME);
                sb.Append("','");
                sb.Append(ACC_AM);
                sb.Append("','");
                sb.Append(VOLUME_POWER);
                sb.Append("')");

                return sb.ToString();
            }

        }

        public class DailyData
        {
            public string ITM_C;
            public string WK_DT;
            public int VOLUME;
            public int PRICE;
            public int START_AM;
            public int HIGH_AM;
            public int LOW_AM;
            //public int FIX_PRICE_YN;
            //public int FIX_RATE;
            //public string FIX_EVENT;
            //public string WK_DTM;
        }

        public class MinuteData
        {
            public string WK_DT;
            public string WK_TM;
            public string ITM_C;
            public int VOLUME;
            public int PRICE;
            public int START_AM;
            public int HIGH_AM;
            public int LOW_AM;
        }

        public class TickData
        {
            public string WK_DT;
            public string WK_TM;
            public string ITM_C;
            public double SEQ;
            public int VOLUME;
            public int PRICE;
        }

        public class DayData
        {
            public string code;
            public string date;
            public int start_price;
            public int high_price;
            public int row_price;
            public int end_price;
            public int diff_price;
            public double diff_rate;
            public int trading_volume;
            public int trading_price;
            public double credit_price;
            public int local_trading;
            public int foreigner_trading;
            public int agency_trading;
            public int foreign_trading;
            public int program_trading;
            public double foreign_rate;
            public double trading_power;
            public double foreign_owner;
            public double foreign_ratio;
            public int foreigner_net_purchase;
            public int agency_net_purchase;
            public int local_net_purchase;
            public double credit_balance_rate;
        }

        public class TradeData
        {
            public string CLIENT_ID;
            public string WK_DT;
            public string WK_TM;
            public string ITM_C;
            public string GUBUN;
            public string SEQ;
            public string DATE;
            public string PRICE;
            public string NUM;
            public string STS_DSC;
            public string MEMO;
            public string WK_DTM;
            public string BID_DSC;
        }

        public class StockConfig
        {
            public string DBType = "";
            public string DBUser = "";
            public string DBPassword = "";
            public string DBName = "";
        }

        public class DBAccessInfo
        {
            public string Url = "";
            public string Port = "";
            public string User = "";
            public string Password = "";
            public string DBName = "";
        }

        public class AccountCredit
        {
            public string ACC_NAME;
            public string WK_DT;
            public string WK_TM;
            public string ACC_KRW;
            public string ACC_KRW_BAC;
            public string ACC_MARGIN;
        }

        public class AccountData
        {
            public string[] Account;
            public string MainAccount;
            public string ID = "";
            public string Name = "";
            public string ConnectType = "";
        }

        public class HoldStock
        {
            public string CLIENT_ID = "";
            public string WK_DT;
            public string WK_TM;
            public string ITM_C;
            public string ITM_NAME;
            public int NOW_PRICE;
            public int BUY_PRICE;
            public int BUY_AM;
            public int QTY;
            public int SELL_PROFIT;
            public int COMMISSION;
            public int SELL_TEX;
            public int CREDIT;
            public string LOAN_DT;
            public int PAYMENT_BALANCE;
            public int LIQUID_QTY;
            public int CREDIT_PRICE;
            public int CREDIT_INTEREST;
            public string EXPIRE_DT;
        }

        public class TimerInformation
        {
            public string name;
            public DateTime date;
        }

        public class WorkInformation
        {
            public List<string> DayHistoryEnd = new List<string>();
            public List<string> MinHistoryEnd = new List<string>();
            public List<string> TickHistoryEnd = new List<string>();
            public List<string> AskBidHistoryEnd = new List<string>();

            public bool BasicStarted = false;
            public bool DayStarted = false;
            public bool MinStarted = false;
            public bool TickStarted = false;
            public bool AskBidStarted = false;

            public string BasicCurrentCode = "";
            public string DayCurrentCode = "";
            public string MinCurrentCode = "";
            public string TickCurrentCode = "";
            public string AskBidCurrentCode = "";

        }

        public class OrderCheckUnit
        {
            public string stock_code;
            public DateTime wk_dtm;
            public string order_gubun;
            public string order_qty;

        }

        public class OrderInformation
        {
            public string account;
            public string wk_dtm;
            public string wk_dt;
            public string wk_tm;
            public string order_num;
            public string stock_code;
            public string order_state;
            public string order_name;
            public string order_qty;
            public string order_price;
            public string untraded_qty;
            public string trading_amount;
            public string original_order_num;
            public string order_gubun;
            public string trading_gubun;
            public string sell_gubun;
            public string medosugubun;
            public string order_trade_time;
            public string trading_num;
            public string trading_price;
            public string trading_qty;
            public string current_price;
            public string ask;
            public string bid;
            public string unit_trade_price;
            public string unit_trade_qty;
            public string reason_rejection;
            public string screen_number;
            //public string credit_gubun;
            //public string loan_day;
            //public string holding_qty;
            //public string purchase_unit_price;
            //public string total_purchase_price;
            //public string order_avail_qty;
            //public string net_buying_qty;
            //public string sell_buy_gubun;
            //public string today_gun_sell_sonil;
            //public string jesus_money;
            //public string standard_price;
            //public string margin_ratio;
            //public string credit_price;
            //public string credit_interest;
            //public string mangil;
            //public string today_get_margin_uga;
            //public string today_get_margin_ratio_uga;
            //public string today_get_margin_credit;
            //public string today_get_margin_ratio_credit;
            //public string pasangpumtradeunit;
            //public string upper_limit;
            //public string lower_limit;
        }

        public class HeartBit
        {
            public string clientid;
            public string process_name;
            public string heartbit_time;
        }

        public class Privileges
        {
            public string acc_name;
            public string acc_main;

            public string get_kospi;
            public string get_kospi2;
            public string get_kosdaq;
            public string get_kosdaq2;
            public string get_etf;

            public string basic_update;
            public string day_update;
            public string min_update;
            public string tick_update;
            public string ask_bid_update;
            public string index_update;
            public string index_day_update;

            public string basic_update_time;
            public string day_update_time;
            public string min_update_time;
            public string tick_update_time;
            public string ask_bid_update_time;
            public string index_update_time;
            public string index_day_update_time;

            public string min_update_rt;
            public string sec_update_rt;
            public string tick_update_rt;
            public string ask_bid_update_rt;

            public string min_update_rt_sub;
            public string sec_update_rt_sub;
            public string tick_update_rt_sub;
            public string ask_bid_update_rt_sub;

            public string load_rt_data;
            public string load_rt_time;

            public string update_check_day;
            public string update_check_day2;

            public string day_update_override;
            public string min_update_override;

            public string dev_url;
            public string dev_port;

            public string request_timeout;
            public string trading_check;
            public string info_check;
            public string test;

            public int rt_interval;
        }

        public class System_Operation
        {
            public string client;
            public string restart;
            public string exit;
        }

        public class StockBasic
        {
            public string stock_code;
            public string stock_name;
            public string dt;
            public string stock_group;
            public string settlement_month;
            public string face_price;
            public string capital;
            public string shares_outstanding;
            public string credit_ratio;
            public string year_high_price;
            public string year_low_price;
            public string market_cap;
            public string market_weight;
            public string foreign_burnout_rate;
            public string substitute_price;
            public string per;
            public string eps;
            public string roe;
            public string pbr;
            public string ev;
            public string bps;
            public string sales;
            public string operating_income;
            public string net_income;
            public string open_price;
            public string high_price;
            public string low_price;
            public string upper_limit_price;
            public string lower_limit_price;
            public string base_price;
            public string current_price;
            public string pre_day_symbol;
            public string pre_day_change;
            public string range_ratio;
            public string volume;
            public string pre_day_volume;
            public string face_price_unit;
            public string circulate_share;
            public string circulate_ratio;
        }

        public class DBLogUnit
        {
            public string hostip;
            public string wk_dtm;
            public string group_name;
            public string message;
        }

        public class LogicRun
        {
            public int run_id;
            public string client_id;
            public DateTime starttime;
            public DateTime endtime;
            public string code_id;
            public string state;
            public string input_data;
            public string output_data;
            public string print;
        }

        public class LogicCode
        {
            public string code_id;
            public string code;
        }

        public class DataUpdateTime
        {
            public string id;
            public string wk_dt;
            public string second_time;
            public string minute_time;
        }

        public class AskBidPrice
        {

            public string wk_dt;
            public string wk_tm;
            public string stock_code;
            public int seq;
            public string wk_tm_milli;
            public string wk_tm_real;

            public string top_ask;
            public string top_ask_qty;
            public string top_bid;
            public string top_bid_qty;

            public string ask1;
            public string ask1_qty;
            public string ask1_qty_ratio;
            public string bid1;
            public string bid1_qty;
            public string bid1_qty_ratio;

            public string ask2;
            public string ask2_qty;
            public string ask2_qty_ratio;
            public string bid2;
            public string bid2_qty;
            public string bid2_qty_ratio;

            public string ask3;
            public string ask3_qty;
            public string ask3_qty_ratio;
            public string bid3;
            public string bid3_qty;
            public string bid3_qty_ratio;

            public string ask4;
            public string ask4_qty;
            public string ask4_qty_ratio;
            public string bid4;
            public string bid4_qty;
            public string bid4_qty_ratio;

            public string ask5;
            public string ask5_qty;
            public string ask5_qty_ratio;
            public string bid5;
            public string bid5_qty;
            public string bid5_qty_ratio;

            public string ask6;
            public string ask6_qty;
            public string ask6_qty_ratio;
            public string bid6;
            public string bid6_qty;
            public string bid6_qty_ratio;

            public string ask7;
            public string ask7_qty;
            public string ask7_qty_ratio;
            public string bid7;
            public string bid7_qty;
            public string bid7_qty_ratio;

            public string ask8;
            public string ask8_qty;
            public string ask8_qty_ratio;
            public string bid8;
            public string bid8_qty;
            public string bid8_qty_ratio;

            public string ask9;
            public string ask9_qty;
            public string ask9_qty_ratio;
            public string bid9;
            public string bid9_qty;
            public string bid9_qty_ratio;

            public string ask10;
            public string ask10_qty;
            public string ask10_qty_ratio;
            public string bid10;
            public string bid10_qty;
            public string bid10_qty_ratio;

            public string total_ask_qty;
            public string total_ask_qty_ratio;
            public string total_bid_qty;
            public string total_bid_qty_ratio;

            StringBuilder sb = new StringBuilder();

            public string GetInsertQuery()
            {
                sb.Clear();

                sb.Append("('");
                sb.Append(stock_code);
                sb.Append("','");
                sb.Append(wk_dt);
                sb.Append("','");
                sb.Append(wk_tm);
                sb.Append("','");
                sb.Append(seq);
                sb.Append("','");
                sb.Append(wk_tm_milli);
                sb.Append("','");
                sb.Append(wk_tm_real);

                sb.Append("','");
                sb.Append(ask1);
                sb.Append("','");
                sb.Append(ask1_qty);
                sb.Append("','");
                sb.Append(ask1_qty_ratio);
                sb.Append("','");
                sb.Append(bid1);
                sb.Append("','");
                sb.Append(bid1_qty);
                sb.Append("','");
                sb.Append(bid1_qty_ratio);

                sb.Append("','");
                sb.Append(ask2);
                sb.Append("','");
                sb.Append(ask2_qty);
                sb.Append("','");
                sb.Append(ask2_qty_ratio);
                sb.Append("','");
                sb.Append(bid2);
                sb.Append("','");
                sb.Append(bid2_qty);
                sb.Append("','");
                sb.Append(bid2_qty_ratio);

                sb.Append("','");
                sb.Append(ask3);
                sb.Append("','");
                sb.Append(ask3_qty);
                sb.Append("','");
                sb.Append(ask3_qty_ratio);
                sb.Append("','");
                sb.Append(bid3);
                sb.Append("','");
                sb.Append(bid3_qty);
                sb.Append("','");
                sb.Append(bid3_qty_ratio);

                sb.Append("','");
                sb.Append(ask4);
                sb.Append("','");
                sb.Append(ask4_qty);
                sb.Append("','");
                sb.Append(ask4_qty_ratio);
                sb.Append("','");
                sb.Append(bid4);
                sb.Append("','");
                sb.Append(bid4_qty);
                sb.Append("','");
                sb.Append(bid4_qty_ratio);

                sb.Append("','");
                sb.Append(ask5);
                sb.Append("','");
                sb.Append(ask5_qty);
                sb.Append("','");
                sb.Append(ask5_qty_ratio);
                sb.Append("','");
                sb.Append(bid5);
                sb.Append("','");
                sb.Append(bid5_qty);
                sb.Append("','");
                sb.Append(bid5_qty_ratio);

                sb.Append("','");
                sb.Append(ask6);
                sb.Append("','");
                sb.Append(ask6_qty);
                sb.Append("','");
                sb.Append(ask6_qty_ratio);
                sb.Append("','");
                sb.Append(bid6);
                sb.Append("','");
                sb.Append(bid6_qty);
                sb.Append("','");
                sb.Append(bid6_qty_ratio);

                sb.Append("','");
                sb.Append(ask7);
                sb.Append("','");
                sb.Append(ask7_qty);
                sb.Append("','");
                sb.Append(ask7_qty_ratio);
                sb.Append("','");
                sb.Append(bid7);
                sb.Append("','");
                sb.Append(bid7_qty);
                sb.Append("','");
                sb.Append(bid7_qty_ratio);

                sb.Append("','");
                sb.Append(ask8);
                sb.Append("','");
                sb.Append(ask8_qty);
                sb.Append("','");
                sb.Append(ask8_qty_ratio);
                sb.Append("','");
                sb.Append(bid8);
                sb.Append("','");
                sb.Append(bid8_qty);
                sb.Append("','");
                sb.Append(bid8_qty_ratio);

                sb.Append("','");
                sb.Append(ask9);
                sb.Append("','");
                sb.Append(ask9_qty);
                sb.Append("','");
                sb.Append(ask9_qty_ratio);
                sb.Append("','");
                sb.Append(bid9);
                sb.Append("','");
                sb.Append(bid9_qty);
                sb.Append("','");
                sb.Append(bid9_qty_ratio);

                sb.Append("','");
                sb.Append(ask10);
                sb.Append("','");
                sb.Append(ask10_qty);
                sb.Append("','");
                sb.Append(ask10_qty_ratio);
                sb.Append("','");
                sb.Append(bid10);
                sb.Append("','");
                sb.Append(bid10_qty);
                sb.Append("','");
                sb.Append(bid10_qty_ratio);

                sb.Append("','");
                sb.Append(total_ask_qty);
                sb.Append("','");
                sb.Append(total_ask_qty_ratio);
                sb.Append("','");
                sb.Append(total_bid_qty);
                sb.Append("','");
                sb.Append(total_bid_qty_ratio);
                sb.Append("')");

                return sb.ToString();
            }

        }
        public class RequestLimitList
        {
            DateTime[] timelist = new DateTime[5];
            int index = 0;

            public RequestLimitList()
            {
                for (int i = 0; i < timelist.Length; i++)
                {
                    timelist[i] = DateTime.Now.AddSeconds(-1);
                }
            }

            public double CheckTime(DateTime t) 
            {
                StockLog.Logger.LOG.WriteLog("Test", "Check Time: " + new System.Diagnostics.StackFrame().ToString());
                double diff = (t - timelist[index]).TotalMilliseconds;
                if (diff >= 1000)
                {
                    timelist[index] = t;

                    index++;
                    if (index >= 5)
                    {
                        index = 0;
                    }

                    return 0;
                }

                return diff;

            }


        }

    }
}
