using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None)]
    public class sessionOpen : Robot
    {
        [Parameter("Risk Percentage", Group = "Risk Type", DefaultValue = true)]
        public bool RiskPercentage { get; set; }

        [Parameter("Percentage Risk Amount", Group = "Risk Type", DefaultValue = true)]
        public double PercentageRiskValue { get; set; }

        [Parameter("Lot Size", Group = "Risk Type", DefaultValue = 1)]
        public int LotSize { get; set; }

        [Parameter("Maximum Volume Broker Side", Group = "Risk Type", DefaultValue = 250)]
        public int MaxBrokerSize { get; set; }

        [Parameter("Start Trading Time", Group = "Entry", DefaultValue = "14:30:00")]
        public string StartTime { get; set; }

        [Parameter("End Trading Time", Group = "Entry", DefaultValue = "14:35:00")]
        public string EndTime { get; set; }

        double EntryGap;
        double StopLoss;
        double TakeProfit;
        double RR = 2;
        string Label = "200";
        DateTime CurrentTime;
        DateTime _start;
        DateTime _finish;
        bool TradesPlaced = false;
        bool TradingToDo = true;
        Bars chartTime;
        int noTrades = 0;

        protected override void OnStart()
        {
            // Establish timings for trading
            updateTimes();
        }

        protected override void OnTick()
        {
            CurrentTime = DateTime.Now;
            // If a new day, update the times for trading
            if (_start.Day != CurrentTime.Day)
            {
                updateTimes();
            }
            // If within trading times and trading has been permitted
            if (within() && TradingToDo == true)
            {
                // Calculate which parameters to use according to seasonality
                updateParameters();
                double BuyPrice = returnHighest() + (EntryGap * Symbol.PipSize);
                double SellPrice = returnLowest() + -(EntryGap * Symbol.PipSize);
                int lots;
                // Calculate lot size depending on if using risk % or fixed lots
                if (RiskPercentage)
                {
                    lots = calculateRisk(StopLoss);
                }
                else
                {
                    lots = LotSize;
                }
                // Place the trades
                // Some brokers place a limit, so to work around this, it will place the same trade multiple times
                // Trades will expire at the end of trading time
                while (lots > MaxBrokerSize)
                {
                    PlaceStopOrder(TradeType.Buy, Symbol.Name, MaxBrokerSize, BuyPrice, Label, StopLoss, TakeProfit, (Server.Time.Add(_finish.Subtract(_start))));
                    PlaceStopOrder(TradeType.Sell, Symbol.Name, MaxBrokerSize, SellPrice, Label, StopLoss, TakeProfit, (Server.Time.Add(_finish.Subtract(_start))));
                    noTrades += 2;
                    lots -= MaxBrokerSize;
                }
                PlaceStopOrder(TradeType.Buy, Symbol.Name, lots, BuyPrice, Label, StopLoss, TakeProfit, (Server.Time.Add(_finish.Subtract(_start))));
                PlaceStopOrder(TradeType.Sell, Symbol.Name, lots, SellPrice, Label, StopLoss, TakeProfit, (Server.Time.Add(_finish.Subtract(_start))));
                noTrades += 2;
                // Change triggers to represent trades have been placed
                TradesPlaced = true;
                TradingToDo = false;
            }
            if (TradesPlaced)
            {
                // Check if any of the trades have activated
                int count = 0;
                foreach (var order in PendingOrders)
                {
                    if (order.Label == Label)
                    {
                        count++;
                    }
                }
                if (count != noTrades)
                // If they have activated, remove the ones that didn't
                // E.g. if buy stops activated, remove sell stops
                {
                    cancelAll();
                }
            }
        }

        // Calculate the timings for when to start and stop trading
        private void updateTimes()
        {
            string[] timings = StartTime.Split(":");
            _start = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, int.Parse(timings[0]), int.Parse(timings[1]), int.Parse(timings[2]));
            timings = EndTime.Split(":");
            _finish = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, int.Parse(timings[0]), int.Parse(timings[1]), int.Parse(timings[2]));
        }

        // Check if the current time is within trading hours
        private bool within()
        {
            if (!(_start < CurrentTime && _finish > CurrentTime))
            {
                TradingToDo = true;
                TradesPlaced = false;
                noTrades = 0;
            }
            return (_start < CurrentTime && _finish > CurrentTime);
        }

        // Calculate parameters according to seasonality
        private void updateParameters()
        {
            if ((CurrentTime.Month <= 6 && CurrentTime.Month != 2) || CurrentTime.Month == 12)
            {
                EntryGap = 390;
                StopLoss = 170;
                TakeProfit = StopLoss * RR;
                chartTime = MarketData.GetBars(TimeFrame.Minute4);
            }
            else
            {
                EntryGap = 430;
                StopLoss = 180;
                TakeProfit = StopLoss * RR;
                chartTime = MarketData.GetBars(TimeFrame.Minute3);
            }
        }

        // Calculate the lot size required if risking percentage
        private int calculateRisk(double stopPips)
        {
            double totalBalance = Account.Balance;
            double riskGBP = (totalBalance * PercentageRiskValue) / 100;
            double totalPips = stopPips;
            double exactVolume = (riskGBP / (Symbol.PipValue * totalPips));
            int returnVolume = (int)exactVolume;
            if (returnVolume == 0)
            {
                return 1;
            }
            else
            {
                return returnVolume;
            }
        }

        // Return high of last candle
        private double returnHighest()
        {
            return (chartTime.HighPrices.Last(1));
        }

        // Return low of last candle
        private double returnLowest()
        {
            return (chartTime.LowPrices.Last(1));
        }

        // Cancel all of the orders 
        private void cancelAll()
        {
            foreach (var order in PendingOrders)
            {
                if (order.Label == Label)
                {
                    order.Cancel();
                }
            }
            TradesPlaced = false;
            noTrades = 0;
        }
    }
}