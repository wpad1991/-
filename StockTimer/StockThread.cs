using System;
using System.Threading;

namespace StockTimer
{
    public class StockThread
    {
        public delegate void ThreadExceptionHandler(Exception exception);

        public event ThreadExceptionHandler ThreadExceptionEvent = null;

        public string Name = "";
        bool isExecute = false;
        ManualResetEvent stopEvent = new ManualResetEvent(false);
        public int Interval = 500;
        Action func = null;

        public StockThread(Action func)
        {
            if (func == null)
            {
                throw new ArgumentNullException();
            }

            this.func = func;
        }

        public void Start()
        {
            if (!isExecute)
            {
                isExecute = true;
                stopEvent.Reset();
                ThreadPool.QueueUserWorkItem(_ => Execute());
            }
        }

        public void Stop()
        {
            stopEvent.Set();
        }

        private void Execute()
        {
            
            while (!stopEvent.WaitOne((int)Interval))
            {
                try
                {
                    func();
                }
                catch (Exception except)
                {
                    ThreadExceptionEvent?.Invoke(except);
                }
            }
            isExecute = false;
        }
    }
}
