﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BasicallyMe.RobinhoodNet;

namespace RobinhoodDesktop.HomePage
{
    public partial class HomePageForm : Form
    {
        public HomePageForm()
        {
            InitializeComponent();
            this.FormClosing += HomePageForm_FormClosing;
            
            // Load the configuration
            this.Config = UserConfig.Load(UserConfig.CONFIG_FILE);

            // Create the interface to the stock data
            Robinhood = new RobinhoodInterface();
            DataAccessor.SetAccessor(new DataTableCache(Robinhood));
            Broker.SetBroker(Robinhood);

            UIList = new Panel();
            UIList.HorizontalScroll.Maximum = 0;
            UIList.AutoScroll = true;
            UIList.Resize += (sender, e) =>
            {
                foreach(Control c in UIList.Controls)
                {
                    c.Size = new Size(UIList.Width - 10, c.Height);
                }
            };
            UIList.ControlAdded += UIList_Pack;
            UIList.ControlRemoved += UIList_Pack;
            UIList.Location = new Point(340, 20);
            foreach(var configObj in Config.StockCharts)
            {
                CreateStockChart(StockUI.LoadConfig(configObj));
            }
            //Plot.SetChartData(GenerateExampleData());
            this.Controls.Add(UIList);

            

            // Create the search box
            SearchHome = new SearchList();
            SearchHome.Size = new Size(270, 50);
            SearchHome.Location = new Point(50, 20);
            SearchHome.AutoSize = true;
            SearchHome.AddToWatchlist += (string symbol) => { StockListHome.Add("Watchlist", symbol); };
            SearchHome.AddStockUi += CreateStockChart;
            Controls.Add(SearchHome);

            // Add test stock symbols to the list
#if true
            StockListHome = new StockList();
            StockListHome.Location = new Point(SearchHome.Location.X, SearchHome.Location.Y + 100);
            StockListHome.AutoScroll = true;
            StockListHome.Size = new Size(300, 300);
            StockListHome.AddStockUi += CreateStockChart;
            Controls.Add(StockListHome);

            StockListHome.Add("Positions", "AMD");
            foreach(string symbol in Config.LocalWatchlist)
            {
                StockListHome.Add("Watchlist", symbol);
            }

#endif
            // Create the menu
            Menu = new MenuBar();
            Menu.ToggleButton.Location = new Point(20, 20);
            Menu.LogIn.RememberLogIn.Checked = Config.RememberLogin;
            Controls.Add(Menu.ToggleButton);

            // Set up the resize handler
            this.ResizeEnd += HomePageForm_ResizeEnd;
            HomePageForm_ResizeEnd(this, EventArgs.Empty);

            // Sign in if authentification is available
            if(Config.RememberLogin && !string.IsNullOrEmpty(Config.AuthenticationToken))
            {
                Broker.SignIn(Config.AuthenticationToken);
            }
        }

        #region Variables
        public SearchList SearchHome;
        public StockList StockListHome;
        public RobinhoodInterface Robinhood;
        public UserConfig Config;
        public List<StockUI> StockUIs = new List<StockUI>();
        public Panel UIList;
        public MenuBar Menu;
        #endregion

        private static System.Data.DataTable GenerateExampleData()
        {
            System.Data.DataTable dt = new System.Data.DataTable();
            dt.Columns.Add("Time", typeof(DateTime));
            dt.Columns.Add("Price", typeof(float));

            try
            {
                var rh = new RobinhoodClient();
                var history = rh.DownloadHistory("AMD", "5minute", "week").Result;

                foreach(var p in history.HistoricalInfo)
                {
                    dt.Rows.Add(p.BeginsAt.ToLocalTime(), (float)p.OpenPrice);
                }
            }
            catch(Exception ex)
            {
                Environment.Exit(1);
            }

            return dt;
        }

        private void CreateStockChart(string symbol)
        {
            StockUI ui = new StockUI(symbol);
            CreateStockChart(ui);
        }
        private void CreateStockChart(StockUI ui)
        {
            StockUIs.Add(ui);
            StockChartPanel p = new StockChartPanel(ui.Canvas);
            p.Size = new Size(UIList.Width - 10, 250);
            p.Resize += UIList_Pack;
            p.CloseButton.MouseUp += (sender, e) => { UIList.Controls.Remove(p); };
            ui.Chart.Updated += () => {
                float currentPrice = (float)ui.Chart.Source.Rows[ui.Chart.Source.Rows.Count - 1][StockChart.PRICE_DATA_TAG];
                float changePercent = (currentPrice / (float)ui.Chart.DailyData.Rows[ui.Chart.DailyData.Rows.Count - 1][StockChart.PRICE_DATA_TAG]) - 1.0f;
                p.UpdateSummaryText(string.Format("{0} {1:c} ({2:P2})", ui.Symbol, currentPrice, changePercent));
            };
            UIList.Controls.Add(p);
        }

        private void UIList_Pack(object sender, System.EventArgs e)
        {
            int y = 0;
            foreach(Control c in UIList.Controls)
            {
                c.Location = new Point(c.Location.X, y);
                y += c.Height + 5;
            }
        }

        private void HomePageForm_ResizeEnd(object sender, System.EventArgs e)
        {
            UIList.Size = new Size((this.Width - (StockListHome.Location.X + StockListHome.Width)) - 40, (this.Height - UIList.Location.Y) - 40);
            StockListHome.Size = new Size(StockListHome.Width, ((this.Height - StockListHome.Location.Y) - 40));
        }

        private void HomePageForm_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Robinhood.Close();

            Config.LocalWatchlist.Clear();
            foreach(var stock in StockListHome.Stocks["Watchlist"])
            {
                Config.LocalWatchlist.Add(stock.Symbol);
            }
            Config.StockCharts.Clear();
            foreach(var chart in StockUIs)
            {
                Config.StockCharts.Add(chart.SaveConfig());
            }

            Config.RememberLogin = Menu.LogIn.RememberLogIn.Checked;
            if(Config.RememberLogin && Broker.IsSignedIn())
            {
                Config.AuthenticationToken = Broker.GetAuthenticationToken();
            }

            // Save the current user configuration
            Config.Save(UserConfig.CONFIG_FILE);
        }
    }
}
