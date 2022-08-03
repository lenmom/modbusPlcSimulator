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
        private Thread _thread;
        private ModbusSlave _modbusSlave;
        private TcpListener _listener;
        private IPAddress _ip;
        public string _status { get; set; } //状态
        public bool _isRunning = false;
        public int _id { get; set; }
        public int _port { get; set; }
        public string _name { get; set; }
        // public TurbineType _type{ get; set; }
        public string _typeStr { get; set; }
        private readonly DataStore _dataStore;
        private readonly byte _slaveId = 1; //设备id,默认为1

        private DataTable _dt = new DataTable();//保存配置文件
        private readonly Dictionary<string, int> _ioName2index = new Dictionary<string, int>();//保存ioName 到在datable中的索引
        public DataStore getDataStore()
        {
            return this._dataStore;
        }

        public Node(int id, int port, string typeStr)
        {
            this._id = id;
            this._port = port;
            this._typeStr = typeStr;
            this._name = id.ToString() + "#设备";
            this._dataStore = DataStoreFactory.CreateDefaultDataStore();
            this._status = "服务未启动";
            string errStr = "";
            if (!this.initNodeData(ref errStr))
            {
                string outStr = "[" + this._name + "]" + "CSV数据解析失败! \n请检查" + typeStr + ".CSV \n";
                outStr += errStr;
                MessageBox.Show(outStr, "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);  //用风机类型初始化数据
            }

        }

        public void start()
        {
            if (this._isRunning)
            {
                return;
            }

            if (IsPortUsed(this._port))
            {
                string errorStr = "TCP端口：[" + this._port.ToString() + "]" + "被占用";
                MessageBox.Show(errorStr, "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                this._ip = new IPAddress(new byte[] { 0, 0, 0, 0 }); //new IPAddress(new byte[] { 127, 0, 0, 1 });
                this._listener = new TcpListener(this._ip, this._port);

                this._modbusSlave = ModbusTcpSlave.CreateTcp(this._slaveId, this._listener);
                this._modbusSlave.DataStore = this._dataStore;
                this._modbusSlave.ModbusSlaveRequestReceived += this.requestReceiveHandler;

                this._thread = new Thread(this._modbusSlave.Listen) { Name = this._port.ToString() };
                this._thread.Start();
                this._status = "服务运行中";
                this._isRunning = true;
            }
            catch (Exception)
            {
                string errorStr = "[" + this._name + "]" + "风机启动失败";
                MessageBox.Show(errorStr, "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void requestReceiveHandler(object sender, ModbusSlaveRequestEventArgs e)//收到请求
        {
            IModbusMessage message = e.Message;
            string writeLogStr = this._name + ": " + message;

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
            string logStr = this._name + " Receive Request：" + message;
            Program._logForm.addLog(logStr);
        }


        public void stop()
        {
            if (this._isRunning == false)
            {
                return;
            }
            this._status = "服务停止";
            this._isRunning = false;
            try
            {

                this._modbusSlave.Dispose();
                //_listener.Stop();
                this._thread.Abort();
            }
            catch
            {

            }
        }

        public void Dispose()
        {
            this.stop();
        }

        private ModbusDataCollection<ushort> getRegisterGroup(int groupindex)//根据3或4返回适合的寄存器
        {
            switch (groupindex)
            {
                case 3:
                    return this._dataStore.HoldingRegisters; //可由moddbus修改
                case 4:
                    return this._dataStore.InputRegisters;   //不可通过modbus修改
                default:
                    return this._dataStore.InputRegisters;
            }
        }
        public void setValue16(int groupindex, int offset, ushort value)
        {
            ModbusDataCollection<ushort> data = this.getRegisterGroup(groupindex);
            data[offset] = value;
        }
        public void setValue32(int groupindex, int offset, int value)
        {
            byte[] valueBuf = BitConverter.GetBytes(value);
            ushort lowOrderValue = BitConverter.ToUInt16(valueBuf, 0);
            ushort highOrderValue = BitConverter.ToUInt16(valueBuf, 2);

            ModbusDataCollection<ushort> data = this.getRegisterGroup(groupindex);
            data[offset] = lowOrderValue;
            data[offset + 1] = highOrderValue;
        }
        public void setValue32(int groupindex, int offset, float value)
        {
            ushort lowOrderValue = BitConverter.ToUInt16(BitConverter.GetBytes(value), 0);
            ushort highOrderValue = BitConverter.ToUInt16(BitConverter.GetBytes(value), 2);
            ModbusDataCollection<ushort> data = this.getRegisterGroup(groupindex);
            data[offset] = lowOrderValue;
            data[offset + 1] = highOrderValue;
        }
        public void setValue16(int groupindex, int offset, bool value)
        {
            byte[] valueBuf = BitConverter.GetBytes(value);//用1代替true
            ushort lowOrderValue = BitConverter.ToUInt16(valueBuf, 0);

            ModbusDataCollection<ushort> data = this.getRegisterGroup(groupindex);
            data[offset] = lowOrderValue;

        }

        public bool initNodeData(ref string errorStr)
        {
            this._dt = null;

            try
            {
                if (this._typeStr.Length == 0)
                { return false; }

                string configDirStr = MAppConfig.getValueByName("defaultCfgDir");
                string csvFileName = configDirStr + "/" + this._typeStr + ".csv";// _typeStr是MY1500， 采用MY1500.csv作为模型名
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

                        this._ioName2index[ioName] = i;
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
                    int coe_reverse = this.floatToInt(1.00000000f / coe);//1.0除以0.1得到0.9
                    string valueStr = "0";
                    if (this._dt.Columns.Contains("value"))
                    {
                        valueStr = this._dt.Rows[i]["value"].ToString();//如果有value这一列就赋值，否则默认是0
                    }

                    bool ret1 = this.setValueUniverse(groupindex, offset, dataTypeStr, coe_reverse, valueStr);
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

        public int floatToInt(float f)//四舍五入
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

        private bool setValueUniverse(int groupindex, int offset, string dataTypeStr, int coe_reverse, string valueStr)
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
                        this.setValue16(groupindex, offset, (ushort)(value_f * coe_reverse));
                        break;
                    case "INT32":
                    case "DINT":
                    case "DWORD":
                        this.setValue32(groupindex, offset, (int)value_f * coe_reverse);
                        break;
                    case "REAL":
                    case "FLOAT":
                        value_f = float.Parse(valueStr);
                        this.setValue32(groupindex, offset, value_f * coe_reverse);
                        break;
                    case "BIT"://先不管
                        return true;
                    default:
                        this.setValue16(groupindex, offset, (ushort)(value_f * coe_reverse));
                        break;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }
        public void setValueByName(string ioName, string valueStr)
        {
            if (ioName.Length == 0 || valueStr.Length == 0)
            {
                return;
            }

            int ioNameIndex = 0;
            if (!this.fetch(ioName, ref ioNameIndex))
            {
                return;
            }
            try
            {
                int groupindex = int.Parse(this._dt.Rows[ioNameIndex]["groupIndex"].ToString());              //功能码
                int offset = int.Parse(this._dt.Rows[ioNameIndex]["offs"].ToString());

                string dataTypeStr = this._dt.Rows[ioNameIndex]["dataType"].ToString();
                float coe = float.Parse(this._dt.Rows[ioNameIndex]["coe"].ToString());
                int coe_reverse = this.floatToInt(1.0000f / coe);

                bool ret = this.setValueUniverse(groupindex, offset, dataTypeStr, coe_reverse, valueStr); //coe在这不起作用
            }
            catch (Exception)
            {
                return;
            }

        }

        private bool fetch(string ioName, ref int index)//查找ioName 在_dt中的index
        {
            if (this._ioName2index.ContainsKey(ioName))
            {
                index = this._ioName2index[ioName];
                return true;
            }
            else
            {
                return false;
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

    }//class
}
