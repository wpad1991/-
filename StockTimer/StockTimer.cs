using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace StockTimer
{
    public enum ScheduleState
    {
        None = 0,
        Ready = 1,
        Run = 2,
        End = 3,
        Expired = 4
    }

    public enum Cycle
    {
        None = 0,
        Daily = 1,
        Weekly = 2,
        Monthly = 4,
        Yearly = 8
    }

    public enum WeekCycle
    {
        WeekNone = 0,
        WeekMonday = 1,
        WeekTuesday = 2,
        WeekWednesday = 4,
        WeekThursday = 8,
        WeekFriday = 16,
        WeekSaturday = 32,
        WeekSunday = 64,
        WeekWeekday = 128,
        WeekWeekend = 256
    }

    public class StockSchedInfo
    {
        public string Name = "None";
        ScheduleState state = ScheduleState.None;
        Cycle cycle;
        WeekCycle weekCycle;
        int month;
        int day;
        public DateTime StartTime;
        public DateTime EndTime;
        public DateTime ExecutedTime;
        bool isStarted = false;
        Func<string[], bool> f;
        string[] args;
        string lastInitTime = "";

        public ScheduleState State
        {
            get => state; set => state = value;
        }

        public StockSchedInfo(string name, Cycle cycle, WeekCycle weekCycle, int day, string startTime, string endTime, Func<string[], bool> f, string[] args)
        {
            this.Name = name;
            this.state = ScheduleState.Ready;
            this.cycle = cycle;
            this.weekCycle = weekCycle;
            this.day = day;
            this.StartTime = DateTime.ParseExact(startTime, "HHmmss", null);
            this.EndTime = DateTime.ParseExact(endTime, "HHmmss", null); ;
            this.f = f;
            this.args = args;

            if (this.StartTime == this.EndTime && compareDayTime(EndTime, DateTime.Now) < 0)
            {
                ExecutedTime = DateTime.Now;
            }
            else if (this.StartTime == this.EndTime)
            {
                ExecutedTime = DateTime.Now.AddDays(-1);
            }
            
        }

        public void Run()
        {
            DateTime dt = DateTime.Now;

            if (cycle == Cycle.None)
            {
                excuteFunc(dt);
                if (state == ScheduleState.End)
                {
                    state = ScheduleState.Expired;
                }
            }
            else if (cycle == Cycle.Daily)
            {
                excuteFunc(dt);
            }
            else if (cycle == Cycle.Weekly)
            {
                if (checkDayOfWeek(dt))
                {
                    excuteFunc(dt);
                }
                else
                {
                    state = ScheduleState.None;
                }
            }
            else if (cycle == Cycle.Monthly)
            {
                if (day == dt.Day)
                {
                    excuteFunc(dt);
                }
                else
                {
                    state = ScheduleState.None;
                }
            }
            else if (cycle == Cycle.Yearly)
            {
                if (month == dt.Month)
                {
                    if (day == dt.Day)
                    {
                        excuteFunc(dt);
                    }
                    else
                    {
                        state = ScheduleState.None;
                    }
                }
            }
        }

        bool excuteFunc(DateTime dt)
        {
            if (StartTime == EndTime && compareDayTime(EndTime, dt) <= 0 && ExecutedTime.ToString("yyyyMMdd") != dt.ToString("yyyyMMdd"))
            {
                StockLog.Logger.LOG.WriteLog("Timer", "Excute Once Timer -> " + Name);
                ExecutedTime = dt;
                return f(args);
            }
            else if (compareDayTime(EndTime, dt) < 0)
            {
                if (state != ScheduleState.End)
                {
                    state = ScheduleState.End;
                    StockLog.Logger.LOG.WriteLog("Timer", "Change Timer State -> " + state + ": " + Name);
                }
            }
            else if (compareDayTime(StartTime, dt) <= 0 && compareDayTime(EndTime, dt) >= 0)
            {
                if (state != ScheduleState.Run)
                {
                    state = ScheduleState.Run;
                    StockLog.Logger.LOG.WriteLog("Timer", "Change Timer State -> " + state + ": " + Name);
                }
                return f(args);
            }
            //else if (state == ScheduleState.Ready && CompareDayTime(StartTime, dt) <= 0)
            //{
            //    if (state != ScheduleState.Run)
            //    {
            //        state = ScheduleState.Run;
            //        StockLog.Logger.LOG.WriteLog("Timer", "Change Timer State -> " + state + ": " + Name);
            //    }
            //    return f(args);
            //}

            return true;
        }

        public int compareDayTime(DateTime dt1, DateTime dt2)
        {

            double total1 = dt1.Hour * 60 * 60 + dt1.Minute * 60 + dt1.Second + dt1.Millisecond / 1000;
            double total2 = dt2.Hour * 60 * 60 + dt2.Minute * 60 + dt2.Second + dt2.Millisecond / 1000;

            if (total1 > total2)
            {
                return 1;
            }
            else if (total1 < total2)
            {
                return -1;
            }

            return 0;
        }

        bool checkDayOfWeek(DateTime dt)
        {

            switch (dt.DayOfWeek)
            {
                case DayOfWeek.Monday:
                    if (weekCycle == (weekCycle & WeekCycle.WeekMonday)) return true;
                    if (weekCycle == (weekCycle & WeekCycle.WeekWeekday)) return true;
                    break;
                case DayOfWeek.Tuesday:
                    if (weekCycle == (weekCycle & WeekCycle.WeekThursday)) return true;
                    if (weekCycle == (weekCycle & WeekCycle.WeekWeekday)) return true;
                    break;
                case DayOfWeek.Wednesday:
                    if (weekCycle == (weekCycle & WeekCycle.WeekWednesday)) return true;
                    if (weekCycle == (weekCycle & WeekCycle.WeekWeekday)) return true;
                    break;
                case DayOfWeek.Thursday:
                    if (weekCycle == (weekCycle & WeekCycle.WeekThursday)) return true;
                    if (weekCycle == (weekCycle & WeekCycle.WeekWeekday)) return true;
                    break;
                case DayOfWeek.Friday:
                    if (weekCycle == (weekCycle & WeekCycle.WeekFriday)) return true;
                    if (weekCycle == (weekCycle & WeekCycle.WeekWeekday)) return true;
                    break;
                case DayOfWeek.Saturday:
                    if (weekCycle == (weekCycle & WeekCycle.WeekSaturday)) return true;
                    if (weekCycle == (weekCycle & WeekCycle.WeekWeekend)) return true;
                    break;
                case DayOfWeek.Sunday:
                    if (weekCycle == (weekCycle & WeekCycle.WeekSunday)) return true;
                    if (weekCycle == (weekCycle & WeekCycle.WeekWeekend)) return true;
                    break;
            }
            return false;
        }
    }


    public class Timer
    {

        public List<StockSchedInfo> stockSchedInfos = new List<StockSchedInfo>();
        ManualResetEvent stopEvent = new ManualResetEvent(false);
        string name = "none";
        public int Interval = 1000;
        public bool isStarted = false;
        object obj = new object();
        public Timer(string name)
        {
            this.name = name;
        }

        public void SetScheduler(StockSchedInfo ssi)
        {
            stockSchedInfos.Add(ssi);
        }

        public void Start()
        {
            StockLog.Logger.LOG.WriteLog("Timer", "Timer Start Try: " + name);
            if (!isStarted)
            {
                isStarted = true;
                StockLog.Logger.LOG.WriteLog("Timer", "Timer Start: " + name);
                stopEvent.Reset();
                ThreadPool.QueueUserWorkItem(_ => loop());
            }
        }

        public void Stop()
        {
            isStarted = false;
            stopEvent.Set();
        }

        public List<string> GetTimerNames()
        {
            List<string> ret = new List<string>();

            lock (obj)
            {
                foreach (StockSchedInfo ssi in stockSchedInfos)
                {
                    ret.Add(ssi.Name);
                }
            }
            return ret;
        }

        public StockSchedInfo GetTimer(string name)
        {
            lock (obj)
            {
                foreach (StockSchedInfo ssi in stockSchedInfos)
                {
                    if (name == ssi.Name)
                    {
                        return ssi;
                    }
                }
            }
            return null;
        }

        void loop()
        {
            while (!stopEvent.WaitOne((int)Interval))
            {
                try
                {
                    if (stockSchedInfos.Count > 0)
                    {
                        foreach (StockSchedInfo ssi in stockSchedInfos)
                        {
                            ssi.Run();
                        }
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
                catch (Exception except)
                {
                    StockLog.Logger.LOG.WriteLog("Exception", except.ToString());
                }
            }
            isStarted = false;
        }
    }
}
