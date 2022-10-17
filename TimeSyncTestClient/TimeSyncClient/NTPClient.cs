using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// SNTPClient is a C# class designed to connect to time servers on the Internet and
/// fetch the current date and time. Optionally, it may update the time of the local system.
/// The implementation of the protocol is based on the RFC 2030.
/// 
/// Public class members:
/// 
/// Initialize - Sets up data structure and prepares for connection.
/// 
/// Connect - Connects to the time server and populates the data structure.
///    It can also update the system time.
/// 
/// IsResponseValid - Returns true if received data is valid and if comes from
/// a NTP-compliant time server.
/// 
/// ToString - Returns a string representation of the object.
/// 
/// -----------------------------------------------------------------------------
/// Structure of the standard NTP header (as described in RFC 2030) 标准NTP标头的结构（如RFC 2030中所述）
///                       1                   2                   3
///   0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |LI | VN  |Mode |    Stratum    |     Poll      |   Precision   |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                          Root Delay                           |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                       Root Dispersion                         |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                     Reference Identifier                      |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                                                               |
///  |                   Reference Timestamp (64)                    |
///  |                                                               |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                                                               |
///  |                   Originate Timestamp (64)                    |
///  |                                                               |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                                                               |
///  |                    Receive Timestamp (64)                     |
///  |                                                               |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                                                               |
///  |                    Transmit Timestamp (64)                    |
///  |                                                               |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                 Key Identifier (optional) (32)                |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
///  |                                                               |
///  |                                                               |
///  |                 Message Digest (optional) (128)               |
///  |                                                               |
///  |                                                               |
///  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
/// 
/// -----------------------------------------------------------------------------
/// 
/// SNTP Timestamp Format (as described in RFC 2030) SNTP时间戳格式（如RFC 2030中所述）
///                         1                   2                   3
///     0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
/// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
/// |                           Seconds                             |
/// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
/// |                  Seconds Fraction (0-padded)                  |
/// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
/// 
/// </summary>
namespace TimeSyncClient
{
    public class NTPClient
    {
        /// <summary>
        /// SNTP Data Structure Length
        /// SNTP数据结构长度
        /// </summary>
        private const byte SNTPDataLength = 48;

        /// <summary>
        /// SNTP Data Structure (as described in RFC 2030)
        /// SNTP数据结构（如RFC 2030中所述）
        /// </summary>
        byte[] SNTPData = new byte[SNTPDataLength];

        //Offset constants for timestamps in the data structure 
        //数据结构中时间戳的偏移量常量
        /// <summary>
        /// 参考编号
        /// </summary>
        private const byte offReferenceID = 12;
        /// <summary>
        /// 参考时间戳
        /// </summary>
        private const byte offReferenceTimestamp = 16;
        /// <summary>
        /// 原始时间戳
        /// </summary>
        private const byte offOriginateTimestamp = 24;
        /// <summary>
        /// 接收时间戳
        /// </summary>
        private const byte offReceiveTimestamp = 32;
        /// <summary>
        /// 发送时间戳
        /// </summary>
        private const byte offTransmitTimestamp = 40;

        /// <summary>
        /// Leap Indicator Warns of an impending leap second to be inserted/deleted in the last  minute of the current day. 
        /// 飞跃指示器警告在当日的最后一分钟即将插入/删除的leap秒
        /// 值为“11”时表示告警状态，时钟未被同步。为其他值时NTP本身不做处理
        /// </summary>
        public _LeapIndicator LeapIndicator
        {
            get
            {
                // Isolate the two most significant bits 隔离两个最高有效位
                byte val = (byte)(SNTPData[0] >> 6);
                switch (val)
                {
                    case 0: return _LeapIndicator.NoWarning;
                    case 1: return _LeapIndicator.LastMinute61;
                    case 2: return _LeapIndicator.LastMinute59;
                    case 3: goto default;
                    default:
                        return _LeapIndicator.Alarm;
                }
            }
        }

        /// <summary>
        /// Version Number Version number of the protocol (3 or 4) NTP的版本号
        /// 版本号协议的版本号（3或4）NTP的版本号
        /// </summary>
        public byte VersionNumber
        {
            get
            {
                // Isolate bits 3 - 5
                byte val = (byte)((SNTPData[0] & 0x38) >> 3);
                return val;
            }
        }

        /// <summary>
        /// Mode 长度为3比特，表示NTP的工作模式。不同的值所表示的含义分别是：
        /// 0 未定义、
        /// 1 表示主动对等体模式、
        /// 2 表示被动对等体模式、
        /// 3 表示客户模式、
        /// 4 表示服务器模式、
        /// 5 表示广播模式或组播模式、
        /// 6 表示此报文为NTP控制报文、
        /// 7 预留给内部使用
        /// </summary>
        public _Mode Mode
        {
            get
            {
                // Isolate bits 0 - 3
                byte val = (byte)(SNTPData[0] & 0x7);
                switch (val)
                {
                    case 0:
                        return _Mode.Unknown;
                    case 6:
                        return _Mode.Unknown;
                    case 7:
                        return _Mode.Unknown;
                    default:
                        return _Mode.Unknown;
                    case 1:
                        return _Mode.SymmetricActive;
                    case 2:
                        return _Mode.SymmetricPassive;
                    case 3:
                        return _Mode.Client;
                    case 4:
                        return _Mode.Server;
                    case 5:
                        return _Mode.Broadcast;
                }
            }
        }

        /// <summary>
        /// Stratum 系统时钟的层数，取值范围为1～16，它定义了时钟的准确度。
        /// 层数为1的时钟准确度最高，准确度从1到16依次递减，层数为16的时钟处于未同步状态，不能作为参考时钟
        /// </summary>
        public _Stratum Stratum
        {
            get
            {
                byte val = (byte)SNTPData[1];
                if (val == 0) return _Stratum.Unspecified;
                else
                    if (val == 1) return _Stratum.PrimaryReference;
                    else
                        if (val <= 15) return _Stratum.SecondaryReference;
                        else
                            return _Stratum.Reserved;
            }
        }

        /// <summary>
        /// Poll Interval (in seconds) Maximum interval between successive messages 
        /// 轮询时间，即两个连续NTP报文之间的时间间隔
        /// </summary>
        public uint PollInterval
        {
            get
            {
                // Thanks to Jim Hollenhorst <hollenho@attbi.com>
                return (uint)(Math.Pow(2, (sbyte)SNTPData[2]));
            }
        }

        /// <summary>
        /// Precision (in seconds) Precision of the clock 
        /// 系统时钟的精度
        /// </summary>
        public double Precision
        {
            get
            {
                // Thanks to Jim Hollenhorst <hollenho@attbi.com>
                return (Math.Pow(2, (sbyte)SNTPData[3]));
            }
        }

        /// <summary>
        /// Root Delay (in milliseconds) Round trip time to the primary reference source  
        /// 根延迟（毫秒）到主要参考源的往返时间
        /// NTP服务器到主参考时钟的延迟
        /// </summary>
        public double RootDelay
        {
            get
            {
                int temp = 0;
                temp = 256 * (256 * (256 * SNTPData[4] + SNTPData[5]) + SNTPData[6]) + SNTPData[7];
                return 1000 * (((double)temp) / 0x10000);
            }
        }

        /// <summary>
        /// Root Dispersion (in milliseconds) Nominal error relative to the primary reference source 
        /// 根色散（以毫秒为单位）相对于主要参考源的标称误差
        /// 系统时钟相对于主参考时钟的最大误差
        /// </summary>
        public double RootDispersion
        {
            get
            {
                int temp = 0;
                temp = 256 * (256 * (256 * SNTPData[8] + SNTPData[9]) + SNTPData[10]) + SNTPData[11];
                return 1000 * (((double)temp) / 0x10000);
            }
        }

        /// <summary>
        /// Reference Identifier Reference identifier (either a 4 character string or an IP address)
        /// 参考标识符参考标识符（4个字符串或IP地址）
        /// </summary>
        public string ReferenceID
        {
            get
            {
                string val = "";
                switch (Stratum)
                {
                    case _Stratum.Unspecified:
                        goto case _Stratum.PrimaryReference;
                    case _Stratum.PrimaryReference:
                        val += (char)SNTPData[offReferenceID + 0];
                        val += (char)SNTPData[offReferenceID + 1];
                        val += (char)SNTPData[offReferenceID + 2];
                        val += (char)SNTPData[offReferenceID + 3];
                        break;
                    case _Stratum.SecondaryReference:
                        switch (VersionNumber)
                        {
                            case 3:    // Version 3, Reference ID is an IPv4 address
                                string Address = SNTPData[offReferenceID + 0].ToString() + "." +
                                                 SNTPData[offReferenceID + 1].ToString() + "." +
                                                 SNTPData[offReferenceID + 2].ToString() + "." +
                                                 SNTPData[offReferenceID + 3].ToString();
                                try
                                {
                                    IPHostEntry Host = Dns.GetHostEntry(Address);
                                    val = Host.HostName + " (" + Address + ")";
                                }
                                catch (Exception)
                                {
                                    val = "N/A";
                                }
                                break;
                            case 4: // Version 4, Reference ID is the timestamp of last update
                                DateTime time = ComputeDate(GetMilliSeconds(offReferenceID));
                                // Take care of the time zone
                                TimeSpan offspan = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);
                                val = (time + offspan).ToString();
                                break;
                            default:
                                val = "N/A";
                                break;
                        }
                        break;
                }

                return val;
            }
        }

        /// <summary>
        /// Reference Timestamp The time at which the clock was last set or corrected 
        /// NTP系统时钟最后一次被设定或更新的时间
        /// </summary>
        public DateTime ReferenceTimestamp
        {
            get
            {
                DateTime time = ComputeDate(GetMilliSeconds(offReferenceTimestamp));
                // Take care of the time zone
                TimeSpan offspan = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);
                return time + offspan;
            }
        }

        /// <summary>
        /// Originate Timestamp (T1)  The time at which the request departed the client for the server. 
        /// 发送时间戳（T1）请求数据包离开客户端的客户端的时间。
        /// </summary>
        public DateTime OriginateTimestamp
        {
            get
            {
                return ComputeDate(GetMilliSeconds(offOriginateTimestamp));
            }
        }

        /// <summary>
        /// Receive Timestamp (T2) The time at which the request arrived at the server. 
        /// 接收时间戳（T2）请求数据包到达服务器的服务器时间。
        /// </summary>
        public DateTime ReceiveTimestamp
        {
            get
            {
                DateTime time = ComputeDate(GetMilliSeconds(offReceiveTimestamp));
                // Take care of the time zone
                TimeSpan offspan = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);
                return time + offspan;
            }
        }

        /// <summary>
        /// Transmit Timestamp (T3) The time at which the reply departed the server for client. 
        /// 发送时间戳（T3）响应数据包离开服务器的服务器时间。
        /// </summary>
        public DateTime TransmitTimestamp
        {
            get
            {
                DateTime time = ComputeDate(GetMilliSeconds(offTransmitTimestamp));
                // Take care of the time zone
                TimeSpan offspan = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);
                return time + offspan;
            }
            set
            {
                SetDate(offTransmitTimestamp, value);
            }
        }

        /// <summary>
        /// Destination Timestamp (T4) The time at which the reply arrived at the client. 
        /// 接收时间戳（T4）响应数据包到达客户端的客户端时间。
        /// </summary>
        public DateTime DestinationTimestamp;

        /// <summary>
        /// Round trip delay (in milliseconds) The time between the departure of request and arrival of reply 
        /// 往返延迟（以毫秒为单位）从请求离开到答复到达之间网络延迟时间
        /// </summary>
        public double RoundTripDelay
        {
            get
            {
                // Thanks to DNH <dnharris@csrlink.net>
                //公式:(T4-T1)-(T3-T2)
                //TimeSpan span = (DestinationTimestamp - OriginateTimestamp) - (ReceiveTimestamp - TransmitTimestamp);
                TimeSpan span = (DestinationTimestamp - OriginateTimestamp) - (TransmitTimestamp - ReceiveTimestamp);
                //Console.WriteLine(span.TotalMilliseconds.ToString() + " - " + span.TotalMilliseconds.ToString());
                return span.TotalMilliseconds;
            }
        }
        /// <summary>
        /// Local clock offset (in milliseconds)  The offset of the local clock relative to the primary reference source.
        /// 本地时钟偏移量（以毫秒为单位）主要参考源相对于本地时钟的偏移量。
        /// 本机相对于NTP服务器（主时钟）的时间差
        /// </summary>
        public double LocalClockOffset
        {
            get
            {
                // Thanks to DNH <dnharris@csrlink.net>
                //公式：(T2-T1)+(T3-T4)/2
                TimeSpan span = (ReceiveTimestamp - OriginateTimestamp) + (TransmitTimestamp - DestinationTimestamp);
                return span.TotalMilliseconds / 2;
            }
        }
        /// <summary>
        /// NTP获取的时间
        /// </summary>
        public DateTime NtpAcquiredTime
        {
            get { return DateTime.Now.AddMilliseconds(LocalClockOffset); }
        }

        /// <summary>
        /// Compute date, given the number of milliseconds since January 1, 1900
        /// 计算日期，以自1900年1月1日以来的毫秒数为单位
        /// </summary>
        /// <param name="milliseconds"></param>
        /// <returns></returns>
        private DateTime ComputeDate(ulong milliseconds)
        {
            TimeSpan span = TimeSpan.FromMilliseconds((double)milliseconds);
            DateTime time = new DateTime(1900, 1, 1);
            time += span;
            return time;
        }

        /// <summary>
        /// Compute the number of milliseconds, given the offset of a 8-byte array
        /// 给定8字节数组的偏移量，计算毫秒数
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private ulong GetMilliSeconds(byte offset)
        {
            ulong intpart = 0, fractpart = 0;

            for (int i = 0; i <= 3; i++)
            {
                intpart = 256 * intpart + SNTPData[offset + i];
            }
            for (int i = 4; i <= 7; i++)
            {
                fractpart = 256 * fractpart + SNTPData[offset + i];
            }
            ulong milliseconds = intpart * 1000 + (fractpart * 1000) / 0x100000000L;
            return milliseconds;
        }

        /// <summary>
        /// Compute the 8-byte array, given the date
        /// 给定日期，计算8字节数组
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="date"></param>
        private void SetDate(byte offset, DateTime date)
        {
            ulong intpart = 0, fractpart = 0;
            DateTime StartOfCentury = new DateTime(1900, 1, 1, 0, 0, 0);    // January 1, 1900 12:00 AM

            ulong milliseconds = (ulong)(date - StartOfCentury).TotalMilliseconds;
            intpart = milliseconds / 1000;
            fractpart = ((milliseconds % 1000) * 0x100000000L) / 1000;

            ulong temp = intpart;
            for (int i = 3; i >= 0; i--)
            {
                SNTPData[offset + i] = (byte)(temp % 256);
                temp = temp / 256;
            }

            temp = fractpart;
            for (int i = 7; i >= 4; i--)
            {
                SNTPData[offset + i] = (byte)(temp % 256);
                temp = temp / 256;
            }
        }

        /// <summary>
        /// Initialize the NTPClient data
        /// 初始化NTPClient数据
        /// </summary>
        private void Initialize()
        {
            // Set version number to 4 and Mode to 3 (client) 将版本号设置为4，将模式设置为3（客户端）
            SNTPData[0] = 0x1B;
            // Initialize all other fields with 0 用0初始化所有其他字段
            for (int i = 1; i < 48; i++)
            {
                SNTPData[i] = 0;
            }
            // Initialize the transmit timestamp 初始化发送时间戳，这里使用UTC时间，
            //TransmitTimestamp = DateTime.UtcNow;
            TransmitTimestamp = DateTime.Now;
        }

        /// <summary>
        /// The IPAddress of the time server we're connecting to 我们要连接的时间服务器的IP地址
        /// </summary>
        private IPAddress serverAddress = null;

        public IPAddress ServerAddress
        {
            get { return serverAddress; }
            set { serverAddress = value; }
        }

        private string errorInfo = "";

        public string ErrorInfo
        {
            get { return errorInfo; }
            set { errorInfo = value; }
        }

        /// <summary>
        /// Constractor with HostName
        /// </summary>
        /// <param name="host"></param>
        public NTPClient(string host)
        {
            //string host = "ntp1.aliyun.com";
            //string host = "0.asia.pool.ntp.org";
            //string host = "1.asia.pool.ntp.org";
            //string host = "www.ntp.org/";

            // Resolve server address
            IPHostEntry hostadd = Dns.GetHostEntry(host);
            foreach (IPAddress address in hostadd.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork) //只支持IPV4协议的IP地址
                {
                    serverAddress = address;
                    break;
                }
            }
            if (serverAddress == null)
                throw new Exception("Can't get any ipaddress infomation");
        }

        /// <summary>
        /// Constractor with IPAddress 
        /// </summary>
        /// <param name="address"></param>
        public NTPClient(IPAddress address)
        {
            if (address == null)
                throw new Exception("Can't get any ipaddress infomation");
            serverAddress = address;
            ErrorInfo = "";
        }
        public NTPClient()
        {
            ErrorInfo = "";
        }


        /// <summary>
        /// Connect to the time server and update system time
        /// 连接到时间服务器获取时间
        /// </summary>
        /// <param name="updateSystemTime">更新系统时间</param>
        public void Connect(bool updateSystemTime, int timeout = 3000)
        {
            ErrorInfo = "";
            IPEndPoint EPhost = new IPEndPoint(serverAddress, 123);

            //Connect the time server 连接时间服务器
            using (UdpClient TimeSocket = new UdpClient())
            {
                try
                {
                    TimeSocket.Connect(EPhost);
                    // Initialize data structure 初始化数据结构
                    Initialize();

                    TimeSocket.Send(SNTPData, SNTPData.Length);
                    TimeSocket.Client.ReceiveTimeout = timeout;
                    SNTPData = TimeSocket.Receive(ref EPhost);
                    //SNTPData ntpdate日志
                    //ToolClass.applogs(SNTPData[40].ToString() + "-" + SNTPData[41].ToString() + "-" + SNTPData[42].ToString() + "-" + SNTPData[43].ToString());

                    if (!IsResponseValid)
                    {
                        ErrorInfo = "Invalid response from " + serverAddress.ToString();
                        throw new Exception("Invalid response from " + serverAddress.ToString());
                    }
                }
                catch (Exception ex)
                {
                    ErrorInfo = ex.Message;
                    return;
                    throw ex;
                }
            }
            DestinationTimestamp = DateTime.Now;
            //TransmitTimestamp = TransmitTimestamp.AddHours(8);

            if (updateSystemTime)
                SetTime();
            //取时完成后转化为北京时间(北京时间=UTC时间+8小时)
            //this.OriginateTimestamp.AddHours(8);
        }


        /// <summary>
        /// Check if the response from server is valid 检查服务器的响应是否有效
        /// </summary>
        /// <returns></returns>
        public bool IsResponseValid
        {
            get
            {
                return !(SNTPData.Length < SNTPDataLength || Mode != _Mode.Server);
            }
        }

        /// <summary>
        /// Converts the object to string 将对象转换为字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(512);
            sb.Append("Leap Indicator: ");
            switch (LeapIndicator)
            {
                case _LeapIndicator.NoWarning:
                    sb.Append("No warning");
                    break;
                case _LeapIndicator.LastMinute61:
                    sb.Append("Last minute has 61 seconds");
                    break;
                case _LeapIndicator.LastMinute59:
                    sb.Append("Last minute has 59 seconds");
                    break;
                case _LeapIndicator.Alarm:
                    sb.Append("Alarm Condition (clock not synchronized)");
                    break;
            }
            sb.AppendFormat("\r\nVersion number: {0}\r\n", VersionNumber);
            sb.Append("Mode: ");
            switch (Mode)
            {
                case _Mode.Unknown:
                    sb.Append("Unknown");
                    break;
                case _Mode.SymmetricActive:
                    sb.Append("Symmetric Active");
                    break;
                case _Mode.SymmetricPassive:
                    sb.Append("Symmetric Pasive");
                    break;
                case _Mode.Client:
                    sb.Append("Client");
                    break;
                case _Mode.Server:
                    sb.Append("Server");
                    break;
                case _Mode.Broadcast:
                    sb.Append("Broadcast");
                    break;
            }
            sb.Append("\r\nStratum: ");
            switch (Stratum)
            {
                case _Stratum.Unspecified:
                case _Stratum.Reserved:
                    sb.Append("Unspecified");
                    break;
                case _Stratum.PrimaryReference:
                    sb.Append("Primary Reference");
                    break;
                case _Stratum.SecondaryReference:
                    sb.Append("Secondary Reference");
                    break;
            }
            sb.AppendFormat("\r\nLocal Time T3: {0:yyyy-MM-dd HH:mm:ss:fff}", TransmitTimestamp);
            sb.AppendFormat("\r\nDestination Time T4: {0:yyyy-MM-dd HH:mm:ss:fff}", DestinationTimestamp);
            sb.AppendFormat("\r\nPrecision: {0} s", Precision);
            sb.AppendFormat("\r\nPoll Interval:{0} s", PollInterval);
            sb.AppendFormat("\r\nReference ID: {0}", ReferenceID.ToString().Replace("\0", string.Empty));
            sb.AppendFormat("\r\nRoot Delay: {0} ms", RootDelay);
            sb.AppendFormat("\r\nRoot Dispersion: {0} ms", RootDispersion);
            sb.AppendFormat("\r\nRound Trip Delay: {0} ms", RoundTripDelay);
            sb.AppendFormat("\r\nLocal Clock Offset: {0} ms", LocalClockOffset);
            sb.AppendFormat("\r\nReferenceTimestamp: {0:yyyy-MM-dd HH:mm:ss:fff}", ReferenceTimestamp);
            sb.Append("\r\n");
            return sb.ToString();
        }

        /// <summary>
        /// SYSTEMTIME structure used by SetSystemTime SetSystemTime使用的SYSTEMTIME结构
        /// </summary>
        [StructLayoutAttribute(LayoutKind.Sequential)]
        private struct SYSTEMTIME
        {
            public short year;
            public short month;
            public short dayOfWeek;
            public short day;
            public short hour;
            public short minute;
            public short second;
            public short milliseconds;
        }

        [DllImport("kernel32.dll")]
        static extern bool SetLocalTime(ref SYSTEMTIME time);


        /// <summary>
        /// Set system time according to transmit timestamp 把本地时间设置为获取到的时钟时间
        /// </summary>
        public void SetTime()
        {
            SYSTEMTIME st;

            DateTime trts = DateTime.Now.AddMilliseconds(LocalClockOffset);

            st.year = (short)trts.Year;
            st.month = (short)trts.Month;
            st.dayOfWeek = (short)trts.DayOfWeek;
            st.day = (short)trts.Day;
            st.hour = (short)trts.Hour;
            st.minute = (short)trts.Minute;
            st.second = (short)trts.Second;
            st.milliseconds = (short)trts.Millisecond;
            SetLocalTime(ref st);
        }
        /// <summary>
        /// NTP获取的时间
        /// </summary>
        /// <returns></returns>
        public DateTime GetTime()
        {
            return DateTime.Now.AddMilliseconds(LocalClockOffset);
        }
    }


    /// <summary>
    /// Leap indicator field values 飞跃指标字段值
    /// </summary>
    public enum _LeapIndicator
    {
        NoWarning,        // 0 - No warning 没有警告
        LastMinute61,    // 1 - Last minute has 61 seconds 最后一分钟有61秒
        LastMinute59,    // 2 - Last minute has 59 seconds 最后一分钟有59秒
        Alarm            // 3 - Alarm condition (clock not synchronized) 闹钟条件（时钟未同步）
    }

    /// <summary>
    /// Mode field values 模式字段值
    /// </summary>
    public enum _Mode
    {
        SymmetricActive,    // 1 - Symmetric active
        SymmetricPassive,    // 2 - Symmetric pasive
        Client,                // 3 - Client
        Server,                // 4 - Server
        Broadcast,            // 5 - Broadcast
        Unknown                // 0, 6, 7 - Reserved
    }

    /// <summary>
    /// Stratum field values 层场值
    /// </summary>
    public enum _Stratum
    {
        Unspecified,            // 0 - unspecified or unavailable 未指定或不可用
        PrimaryReference,        // 1 - primary reference (e.g. radio-clock) 主要参考资料（例如无线电时钟）
        SecondaryReference,        // 2-15 - secondary reference (via NTP or SNTP) 辅助参考（通过NTP或SNTP）
        Reserved                // 16-255 - reserved 保留的
    }

}
