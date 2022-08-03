using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

using Modbus.Data;
using Modbus.Device;
using Modbus.Message;


namespace modbusPlcSimulator
{
    public class Node : IDisposable
    {
        #region Field

        private Thread _thread;

        private ModbusSlave modbusSlave;
        private TcpListener listener;


        public bool isRunning = false;

        private readonly DataStore dataStore;

        /// <summary>
        /// 设备id,默认为1
        /// </summary>
        private readonly byte slaveId = 1;

        /// <summary>
        /// 保存配置文件
        /// </summary>
        private DataTable _dt = new DataTable();

        /// <summary>
        /// 保存ioName 到在datable中的索引
        /// </summary>
        private readonly Dictionary<string, int> ioName2indexMap = new Dictionary<string, int>();

        #endregion

        #region Property

        public string Status { get; set; } //状态

        public int Id { get; set; }

        public int Port { get; set; }

        public string Name { get; set; }

        public string DeviceName { get; set; }

        public DataStore DataStore
        {
            get
            {
                return this.dataStore;
            }
        }

        #endregion

        #region Constructor

        public Node(int id, int port, string deviceName)
        {
            this.Id = id;
            this.Port = port;
            this.DeviceName = deviceName;


            this.Name = id.ToString() + "#设备";

            this.dataStore = DataStoreFactory.CreateDefaultDataStore();
            this.Status = "服务未启动";
            string errStr = "";

            if (!this.InitNodeData(ref errStr))
            {
                string outStr = "[" + this.Name + "]" + "CSV数据解析失败! \n请检查" + deviceName + ".CSV \n";
                outStr += errStr;
                MessageBox.Show(outStr, "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);  //用风机类型初始化数据
            }

        }

        #endregion

        #region Public Method

        public void Start()
        {
            if (this.isRunning)
            {
                return;
            }

            if (IsPortUsed(this.Port))
            {
                string errorStr = "TCP端口：[" + this.Port.ToString() + "]" + "被占用";
                MessageBox.Show(errorStr, "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                this.listener = new TcpListener(IPAddress.Any, this.Port);

                this.modbusSlave = ModbusTcpSlave.CreateTcp(this.slaveId, this.listener);
                this.modbusSlave.DataStore = this.dataStore;
                this.modbusSlave.ModbusSlaveRequestReceived += this.RequestReceiveHandler;

                this._thread = new Thread(this.modbusSlave.Listen) { Name = this.Port.ToString() };
                this._thread.Start();
                this.Status = "服务运行中";
                this.isRunning = true;
            }
            catch (Exception)
            {
                string errorStr = "[" + this.Name + "]" + "风机启动失败";
                MessageBox.Show(errorStr, "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void Stop()
        {
            if (this.isRunning == false)
            {
                return;
            }

            this.Status = "服务停止";
            this.isRunning = false;
            try
            {

                this.modbusSlave.Dispose();
                //_listener.Stop();
                this._thread.Abort();
            }
            catch
            {

            }
        }


        public void SetValue16(int groupindex, int offset, ushort value)
        {
            ModbusDataCollection<ushort> data = this.getRegisterGroup(groupindex);
            data[offset] = value;
        }

        public void SetValue32(int groupindex, int offset, int value)
        {
            byte[] valueBuf = BitConverter.GetBytes(value);
            ushort lowOrderValue = BitConverter.ToUInt16(valueBuf, 0);
            ushort highOrderValue = BitConverter.ToUInt16(valueBuf, 2);

            ModbusDataCollection<ushort> data = this.getRegisterGroup(groupindex);
            data[offset] = lowOrderValue;
            data[offset + 1] = highOrderValue;
        }

        public void SetValue32(int groupindex, int offset, float value)
        {
            ushort lowOrderValue = BitConverter.ToUInt16(BitConverter.GetBytes(value), 0);
            ushort highOrderValue = BitConverter.ToUInt16(BitConverter.GetBytes(value), 2);
            ModbusDataCollection<ushort> data = this.getRegisterGroup(groupindex);
            data[offset] = lowOrderValue;
            data[offset + 1] = highOrderValue;
        }

        public void SetValue16(int groupindex, int offset, bool value)
        {
            byte[] valueBuf = BitConverter.GetBytes(value);//用1代替true
            ushort lowOrderValue = BitConverter.ToUInt16(valueBuf, 0);

            ModbusDataCollection<ushort> data = this.getRegisterGroup(groupindex);
            data[offset] = lowOrderValue;

        }

        public int FloatToInt(float f)//四舍五入
        {
            int i = 0;
            if (f > 0) //正数
            {
                i = (int)((f * 10 + 5) / 10);
            }
            else if (f < 0) //负数
            {
                i = (int)((f * 10 - 5) / 10);
            }
            else
            {
                i = 0;
            }

            return i;
        }

        public void SetValueByName(string ioName, string valueStr)
        {
            if (ioName.Length == 0 || valueStr.Length == 0)
            {
                return;
            }

            int ioNameIndex = 0;
            if (!this.Fetch(ioName, ref ioNameIndex))
            {
                return;
            }
            try
            {
                int groupindex = int.Parse(this._dt.Rows[ioNameIndex]["groupIndex"].ToString());              //功能码
                int offset = int.Parse(this._dt.Rows[ioNameIndex]["offs"].ToString());

                string dataTypeStr = this._dt.Rows[ioNameIndex]["dataType"].ToString();
                float coe = float.Parse(this._dt.Rows[ioNameIndex]["coe"].ToString());
                int coe_reverse = this.FloatToInt(1.0000f / coe);

                bool ret = this.SetValueUniverse(groupindex, offset, dataTypeStr, coe_reverse, valueStr); //coe在这不起作用
            }
            catch (Exception)
            {
                return;
            }

        }

        /// <summary>
        /// 判断指定端口号是否被占用
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        internal static bool IsPortUsed(int port)
        {
            bool result = false;
            try
            {
                System.Net.NetworkInformation.IPGlobalProperties iproperties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
                System.Net.IPEndPoint[] ipEndPoints = iproperties.GetActiveTcpListeners();
                //System.Net.NetworkInformation.TcpConnectionInformation[] conns = iproperties.GetActiveTcpConnections();

                //foreach (var con in conns)
                foreach (IPEndPoint con in ipEndPoints)
                {
                    // if (con.LocalEndPoint.Port == port)
                    if (con.Port == port)
                    {
                        result = true;
                        break;
                    }
                }
            }
            catch (Exception)
            {
            }
            return result;
        }


        #endregion

        #region IDisposable Implemenation

        public void Dispose()
        {
            this.Stop();
        }

        #endregion

        #region Event Handler

        /// <summary>
        ///  收到请求
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RequestReceiveHandler(object sender, ModbusSlaveRequestEventArgs e)//
        {
            IModbusMessage message = e.Message;
            string writeLogStr = this.Name + ": " + message;

            if (message.FunctionCode == 6)//6是写单个寄存器
            {
                Program._logForm.addWriteLog(writeLogStr);
                return;
            }
            else if (message.FunctionCode == 16)//16是写多个模拟量寄存器
            {
                Program._logForm.addWriteLog(writeLogStr);
                return;
            }
            string logStr = this.Name + " Receive Request：" + message;
            Program._logForm.addLog(logStr);
        }

        #endregion

        #region Private Method

        /// <summary>
        /// 根据3或4返回适合的寄存器
        /// </summary>
        /// <param name="groupindex"></param>
        /// <returns></returns>
        private ModbusDataCollection<ushort> getRegisterGroup(int groupindex)//
        {
            switch (groupindex)
            {
                //case 1:
                //    return this.dataStore.CoilDiscretes; //不由moddbus修改
                //    break;
                //case 2:
                //    return this.dataStore.InputDiscretes; //不由moddbus修改
                //    break;
                case 3:
                    return this.dataStore.HoldingRegisters; //可由moddbus修改
                case 4:
                    return this.dataStore.InputRegisters;   //不可通过modbus修改
                default:
                    return this.dataStore.InputRegisters;
            }
        }

        private bool SetValueUniverse(int groupindex, int offset, string dataTypeStr,
                                      int coe_reverse, string valueStr)
        {
            float value_f;
            string offsetAddOne = MAppConfig.getValueByName("offsetAddOne");
            if (offsetAddOne != "0")
            {
                offset += 1;//此处的内存对应的是modbus协议中的地址，比offset要多1。
            }
            else
            {
                offset += 0;//只在配置为0时才不加1
            }

            try
            {
                if (valueStr.Contains('.'))//有些点虽为INT型，但最终的值是float。风速为INT，modbus值为988这样。
                {
                    value_f = float.Parse(valueStr);
                }
                else
                {
                    value_f = int.Parse(valueStr);
                }



                switch (dataTypeStr.ToUpper())
                {
                    case "INT16":
                    case "WORD":
                    case "INT"://目前主控把INT当作16位
                        this.SetValue16(groupindex, offset, (ushort)(value_f * coe_reverse));
                        break;
                    case "INT32":
                    case "DINT":
                    case "DWORD":
                        this.SetValue32(groupindex, offset, (int)value_f * coe_reverse);
                        break;
                    case "REAL":
                    case "FLOAT":
                        value_f = float.Parse(valueStr);
                        this.SetValue32(groupindex, offset, value_f * coe_reverse);
                        break;
                    case "BIT"://先不管
                        return true;
                    default:
                        this.SetValue16(groupindex, offset, (ushort)(value_f * coe_reverse));
                        break;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 查找ioName 在_dt中的index
        /// </summary>
        /// <param name="ioName"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private bool Fetch(string ioName, ref int index)//
        {
            if (this.ioName2indexMap.ContainsKey(ioName))
            {
                index = this.ioName2indexMap[ioName];
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool InitNodeData(ref string errorStr)
        {
            this._dt = null;

            try
            {
                if (this.DeviceName.Length == 0)
                { return false; }

                string configDirStr = MAppConfig.getValueByName("defaultCfgDir");
                string csvFileName = System.IO.Path.Combine(configDirStr, this.DeviceName + ".csv");// Type是MY1500， 采用MY1500.csv作为模型名
                bool ret = CSVReader.readCSV(csvFileName, out this._dt);
                if (!ret)
                {
                    return false;
                }

                for (int i = 0; i < this._dt.Rows.Count; i++) //写入各行数据
                {
                    {
                        string ioName = this._dt.Rows[i]["path"].ToString();
                        if (ioName.Length == 0)
                        {
                            errorStr = csvFileName + "[path] 列出现空值";
                            return false;
                        }

                        this.ioName2indexMap[ioName] = i;
                    }

                    string groupindexStr = this._dt.Rows[i]["groupIndex"].ToString();
                    if (groupindexStr.Length == 0)
                    {
                        errorStr = csvFileName + "[groupIndex] 列出现空值";
                        return false;
                    }
                    int groupindex = int.Parse(groupindexStr);     //功能码

                    string offsetStr = this._dt.Rows[i]["offs"].ToString();
                    if (offsetStr.Length == 0)
                    {
                        errorStr = csvFileName + "[offs] 列出现空值";
                        return false;
                    }
                    if (offsetStr.Contains(':'))
                    {
                        offsetStr = offsetStr.Substring(0, offsetStr.IndexOf(":"));
                    }
                    int offset = int.Parse(offsetStr);

                    string dataTypeStr = this._dt.Rows[i]["dataType"].ToString();
                    if (dataTypeStr.Length == 0)
                    {
                        errorStr = csvFileName + "[dataType] 列出现空值";
                        return false;
                    }

                    float coe = float.Parse(this._dt.Rows[i]["coe"].ToString());
                    int coe_reverse = this.FloatToInt(1.00000000f / coe);//1.0除以0.1得到0.9
                    string valueStr = "0";
                    if (this._dt.Columns.Contains("value"))
                    {
                        valueStr = this._dt.Rows[i]["value"].ToString();//如果有value这一列就赋值，否则默认是0
                    }

                    bool ret1 = this.SetValueUniverse(groupindex, offset, dataTypeStr, coe_reverse, valueStr);
                    if (ret1 != true)
                    {
                        return false;
                    }
                }//for

                return true;
            }
            catch (Exception e)
            {
                errorStr = e.Message;
                return false;
            }
        }//initData

        #endregion
    }
}
