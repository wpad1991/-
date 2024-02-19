using StockTimer;
using System;
using System.Collections.Generic;
using System.Text;

namespace StockData
{
    public class Singleton
    {
        static DataStore dataStore;

        static Singleton() {
            dataStore = new DataStore();
        }

        public static DataStore Store { get => dataStore; set => dataStore = value; }
    }
}
