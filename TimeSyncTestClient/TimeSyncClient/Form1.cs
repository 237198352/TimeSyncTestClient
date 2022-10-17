using System;
using System.Drawing;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace TimeSyncClient
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        #region 变量
        /// <summary>
        /// 请求次数
        /// </summary>
        public int requestCount = 0;
        /// <summary>
        /// 间隔时间(单位：毫秒)
        /// </summary>
        public int interval = 0;
        /// <summary>
        /// 请求时间的线程
        /// </summary>
        Thread timeThread;
        /// <summary>
        /// NTP 工具类
        /// </summary>
        NTPClient _ntpClientClass = new NTPClient();
        #endregion

        private void Form1_Load(object sender, EventArgs e)
        {
            this.tabPage3.Hide();

            #region test
            //IPAddress ntpserver = IPAddress.Parse("192.168.6.20"); //NTP Server
            //_ntpClientClass.ServerAddress = ntpserver;
            //_ntpClientClass.Connect(false); //参数为false时只从服务器获取信息，为true时同时自动更新本机时间
            //Console.WriteLine("T1:" + _ntpClientClass.OriginateTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            //Console.WriteLine("T2:" + _ntpClientClass.ReceiveTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            //Console.WriteLine("T3:" + _ntpClientClass.TransmitTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            //Console.WriteLine("T4:" + _ntpClientClass.DestinationTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            //Console.WriteLine("偏移量：" + _ntpClientClass.RoundTripDelay.ToString());
            //Console.WriteLine(_ntpClientClass.GetTime().ToString("yyyy-MM-dd HH:mm:ss.fff"));
            ////_ntpClientClass.Connect(false); //参数为false时只从服务器获取信息，为true时同时自动更新本机时间
            ////Console.WriteLine(_ntpClientClass.GetTime().ToString("yyyy-MM-dd HH:mm:ss.fff"));
            //Console.ReadLine();
            DateTimeOffset test1 = DateTimeOffset.Now;//6/8/2016 11:36:11 AM +08:00
            DateTimeOffset test2 = DateTimeOffset.UtcNow;//6/8/2016 3:36:35 AM +00:0

            #endregion
        }

        #region 实时监控

        /// <summary>
        /// 是否计算本地始终偏移量 ：本地时间+偏移量
        /// </summary>
        bool isCalLocalClockOffset = false;

        /// <summary>
        /// 初始化文本框
        /// </summary>
        /// <returns></returns>
        public bool checkTextValue()
        {
            if (this.txtrequestcount.Text.Trim() == "")
            {
                MessageBox.Show("请求次数不能为空！", "提示");
                this.txtrequestcount.Focus();
                return false;
            }
            if (this.txtinterval.Text.Trim() == "")
            {
                MessageBox.Show("间隔时间不能为空！", "提示");
                this.txtinterval.Focus();
                return false;
            }
            requestCount = Convert.ToInt32(this.txtrequestcount.Text.Trim());
            interval = Convert.ToInt32(this.txtinterval.Text.Trim());
            return true;
        }
        /// <summary>
        /// 循环获取NTP时间
        /// </summary>
        public void timeThreadMethod()
        {
            for (int i = 1; i <= requestCount; i++)
            {
                //Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                if (this.IsHandleCreated)
                {
                    this.Invoke(new InvokeHandler(delegate()
                    {
                        //
                        getNTPTimeFillTextBox();
                        txtrequestcountresult.Text = i.ToString();

                    }));
                }
                //Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                Console.WriteLine("***");
                Thread.Sleep(interval);
            }
            //线程运行完毕，调用停止按钮事件
            btnStop_Click(null, null);
        }
        /// <summary>
        /// 使用代理让主线程去处理控件数据
        /// </summary>
        private delegate void InvokeHandler();
        /// <summary>
        /// NTP请求时间 并 分析获取到的服务器时间并实时展示文本框信息
        /// </summary>
        public void getNTPTimeFillTextBox()
        {
            #region 标准时间源
            try
            {
                if (IsIP(this.txt1ntpip.Text.Trim()) && this.txt1ntpip.Tag == null)
                {
                    IPAddress ntpserver = IPAddress.Parse(this.txt1ntpip.Text.Trim()); //NTP Server
                    _ntpClientClass.ServerAddress = ntpserver;
                    _ntpClientClass.Connect(false);                   
                    this.txt1t1.Text = _ntpClientClass.OriginateTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt1t2.Text = _ntpClientClass.ReceiveTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt1t3.Text = _ntpClientClass.TransmitTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt1t4.Text = _ntpClientClass.DestinationTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt1LocalClockOffset.Text = _ntpClientClass.LocalClockOffset.ToString();
                    this.txt1RoundTripDelay.Text = _ntpClientClass.RoundTripDelay.ToString();
                    this.txt1ntptime.Text = _ntpClientClass.GetTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
                    if (isCalLocalClockOffset)
                        this.txt1LocalTime.Text = DateTime.Now.AddMilliseconds(_ntpClientClass.LocalClockOffset).ToString("yyyy-MM-dd HH:mm:ss.fff");
                    else
                        this.txt1LocalTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.Text = "时间同步测试 - " + this.txt1ntptime.Text;
                }
            }
            catch (Exception ex)
            {
                this.txt1ntpip.ForeColor = Color.Red;
                this.txt1ntpip.Tag = "error";
            }
            #endregion
            #region NTP Server 1
            try
            {
                if (IsIP(this.txt2ntpip.Text.Trim()) && this.txt2ntpip.Tag == null)
                {
                    IPAddress ntpserver = IPAddress.Parse(this.txt2ntpip.Text.Trim()); //NTP Server
                    _ntpClientClass.ServerAddress = ntpserver;
                    _ntpClientClass.Connect(false);
                    this.txt2t1.Text = _ntpClientClass.OriginateTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt2t2.Text = _ntpClientClass.ReceiveTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt2t3.Text = _ntpClientClass.TransmitTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt2t4.Text = _ntpClientClass.DestinationTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt2LocalClockOffset.Text = _ntpClientClass.LocalClockOffset.ToString();
                    this.txt2RoundTripDelay.Text = _ntpClientClass.RoundTripDelay.ToString();
                    this.txt2ntptime.Text = _ntpClientClass.GetTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
                    if (isCalLocalClockOffset)
                        this.txt2LocalTime.Text = DateTime.Now.AddMilliseconds(_ntpClientClass.LocalClockOffset).ToString("yyyy-MM-dd HH:mm:ss.fff");
                    else
                        this.txt2LocalTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
      
                }
            }
            catch (Exception ex)
            {
                this.txt2ntpip.ForeColor = Color.Red;    
            }
            #endregion
            #region NTP Server 2
            try
            {
                if (IsIP(this.txt3ntpip.Text.Trim()) && this.txt3ntpip.Tag == null)
                {
                    IPAddress ntpserver = IPAddress.Parse(this.txt3ntpip.Text.Trim()); //NTP Server
                    _ntpClientClass.ServerAddress = ntpserver;
                    _ntpClientClass.Connect(false);
                    this.txt3t1.Text = _ntpClientClass.OriginateTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt3t2.Text = _ntpClientClass.ReceiveTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt3t3.Text = _ntpClientClass.TransmitTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt3t4.Text = _ntpClientClass.DestinationTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt3LocalClockOffset.Text = _ntpClientClass.LocalClockOffset.ToString();
                    this.txt3RoundTripDelay.Text = _ntpClientClass.RoundTripDelay.ToString();
                    this.txt3ntptime.Text = _ntpClientClass.GetTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
                    if (isCalLocalClockOffset)
                        this.txt3LocalTime.Text = DateTime.Now.AddMilliseconds(_ntpClientClass.LocalClockOffset).ToString("yyyy-MM-dd HH:mm:ss.fff");
                    else
                        this.txt3LocalTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                }
            }
            catch (Exception ex)
            {
                this.txt3ntpip.ForeColor = Color.Red;
                this.txt3ntpip.Tag = "error";
            }
            #endregion
            #region NTP Server 3
            try
            {
                if (IsIP(this.txt4ntpip.Text.Trim()) && this.txt4ntpip.Tag == null)
                {
                    IPAddress ntpserver = IPAddress.Parse(this.txt4ntpip.Text.Trim()); //NTP Server
                    _ntpClientClass.ServerAddress = ntpserver;
                    _ntpClientClass.Connect(false);
                    this.txt4t1.Text = _ntpClientClass.OriginateTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt4t2.Text = _ntpClientClass.ReceiveTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt4t3.Text = _ntpClientClass.TransmitTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt4t4.Text = _ntpClientClass.DestinationTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    this.txt4LocalClockOffset.Text = _ntpClientClass.LocalClockOffset.ToString();
                    this.txt4RoundTripDelay.Text = _ntpClientClass.RoundTripDelay.ToString();
                    this.txt4ntptime.Text = _ntpClientClass.GetTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
                    if (isCalLocalClockOffset)
                        this.txt4LocalTime.Text = DateTime.Now.AddMilliseconds(_ntpClientClass.LocalClockOffset).ToString("yyyy-MM-dd HH:mm:ss.fff");
                    else
                        this.txt4LocalTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                }
            }
            catch (Exception ex)
            {
                this.txt4ntpip.ForeColor = Color.Red;
                this.txt4ntpip.Tag = "error";
            }
            #endregion
        }
        #endregion

        #region 系统事件
        private void btnStart_Click(object sender, EventArgs e)
        {
            if (!checkTextValue())
            {
                return;
            }
            timeThread = new Thread(timeThreadMethod);
            timeThread.IsBackground = true;
            timeThread.Start();
            this.btnStart.Enabled = false;
            this.btnStop.Enabled = true;
        }
        private void btnStop_Click(object sender, EventArgs e)
        {
            if (this.IsHandleCreated)
            {
                this.Invoke(new InvokeHandler(delegate()
                {
                    this.btnStart.Enabled = true;
                    this.btnStop.Enabled = false;
                }));
            }
            if (timeThread != null)
            {
                //终止线程
                timeThread.Abort();
            }
        }

        private void checkBoxLocalTimeOffset_CheckedChanged(object sender, EventArgs e)
        {
            isCalLocalClockOffset = this.checkBoxLocalTimeOffset.Checked;
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (timeThread != null)
            {
                //终止线程
                timeThread.Abort();
            }
            if (pressureTestThread1 != null)
            {
                //终止线程
                pressureTestThread1.Abort();
            }

        }
        #endregion

        #region 检测工具
        /// <summary>
        /// 是否为ip
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        public static bool IsIP(string ip)
        {
            return Regex.IsMatch(ip,@"^((2[0-4]\d|25[0-5]|[01]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[01]?\d\d?)$");
        }
        #endregion






        #region 压力测试
        private int threadCount = 5;
        private int threadDelay = 100;
        private int thread1Count = 0;//成功次数
        private int thread1errCount = 0;//失败次数
        Thread pressureTestThread1;
        private void btnTestStart_Click(object sender, EventArgs e)
        {
            threadDelay = Convert.ToInt32(txtDelay.Text.Trim());
            //创建无参的线程
            pressureTestThread1 = new Thread(new ThreadStart(Thread1));
            pressureTestThread1.IsBackground = true;
            //调用Start方法执行线程
            pressureTestThread1.Start();

            this.btnTestStart.Enabled = false;
            this.btnTestStop.Enabled = true;
            this.txtTStartTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            thread1Count = 0;
        }

        /// <summary>
        /// 创建无参的方法
        /// </summary>
        void Thread1()
        {
            while (true)
            {
                if (this.IsHandleCreated)
                {
                    this.Invoke(new InvokeHandler(delegate()
                       {
                           IPAddress ntpserver = IPAddress.Parse(this.txtTNtpIP.Text.Trim()); //NTP Server
                           //IPAddress ntpserver = IPAddress.Parse("192.168.0.2"); //NTP Server
                           _ntpClientClass.ServerAddress = ntpserver;
                           _ntpClientClass.Connect(false);
                           if (_ntpClientClass.ErrorInfo != "")
                           {
                               this.txtTestErrorInfo.Text = _ntpClientClass.ErrorInfo;
                               thread1errCount++;
                               this.txtTestFailureCount.Text = thread1errCount.ToString();
                           }
                           else
                           {
                               this.txtTReferenceid.Text = _ntpClientClass.ReferenceID;
                               this.txtTPollInterval.Text = _ntpClientClass.PollInterval.ToString();
                               this.txtTt1.Text = _ntpClientClass.OriginateTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                               this.txtTt2.Text = _ntpClientClass.ReceiveTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                               this.txtTt3.Text = _ntpClientClass.TransmitTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                               this.txtTt4.Text = _ntpClientClass.DestinationTimestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                               this.txtTLocalClockOffset.Text = _ntpClientClass.LocalClockOffset.ToString();
                               this.txtTRoundTripDelay.Text = _ntpClientClass.RoundTripDelay.ToString();
                               this.txtTRootDispersion.Text = _ntpClientClass.RootDispersion.ToString();
                               this.txtTRootDelay.Text = _ntpClientClass.RootDelay.ToString();
                               this.txtTPrecision.Text = _ntpClientClass.Precision.ToString();
                               this.txtTntptime.Text = _ntpClientClass.NtpAcquiredTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
                               this.txtLocalTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                               this.txtTVersionNumber.Text = _ntpClientClass.VersionNumber.ToString();
                               this.txtTestCount.Text = thread1Count.ToString();
                               thread1Count++;
                           }
                       }));
                }
                Thread.Sleep(threadDelay);
            }
        }
        private void btnTestStop_Click(object sender, EventArgs e)
        {
            this.btnTestStart.Enabled = true;
            this.btnTestStop.Enabled = false;
            if (pressureTestThread1 != null)
            {
                //终止线程
                pressureTestThread1.Abort();
            }
        }

        private void btnTestCalculation_Click(object sender, EventArgs e)
        {
            try
            {
                DateTime StartTime_ = DateTime.Parse(txtTStartTime.Text);
                DateTime AcquiredTime_ = DateTime.Parse(txtTntptime.Text);
                TimeSpan SecTime = AcquiredTime_ - StartTime_;
                txtTextAverageCount.Text = (int.Parse(txtTestCount.Text) / SecTime.TotalSeconds).ToString();
            }
            catch (Exception)
            {
                txtTStartTime.Text = "数据错误";
            }
        }


        #endregion





    }
}
