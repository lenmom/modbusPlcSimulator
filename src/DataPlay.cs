using System;
using System.Data;
using System.Threading;

namespace modbusPlcSimulator
{
    internal class DataPlay
    {
        private DataTable _dt;

        public int _playMode { get; set; }//播放模式 0按序播放，1随机播放

        private bool _bStop = false;
        private readonly DataPlayForm _form;
        private readonly int _deviceIndex = 0;
        private string _dataFileName;

        public DataPlay(DataPlayForm form, int index)
        {
            this._form = form;//窗口指针
            this._deviceIndex = index;
            this._playMode = 0;
        }
        public bool readData(string filePath)
        {
            return CSVReader.readCSV(filePath, out this._dt);
        }

        public void toStart()
        {
            string configDirStr = MAppConfig.getValueByName("defaultCfgDir");
            this._dataFileName = configDirStr + "/" + NodeMgr._nodeList[this._deviceIndex].DeviceName + "_data.csv";

            if (!this.readData(this._dataFileName))
            {
                this.FormLog(this._dataFileName + "读取失败");
                return;
            }

            Thread thread = new Thread(this.run) { Name = "DataPlay" + NodeMgr._nodeList[this._deviceIndex].Name, IsBackground = true };
            thread.Start();
        }
        public void toStop()
        {
            this._bStop = true;//
            this.FormLog("device:" + NodeMgr._nodeList[this._deviceIndex].Name + " 停止更新数据\n");

        }

        private void FormLog(string logStr)
        {
            this._form.log(logStr);
        }

        private static int GetRandomSeed()
        {
            byte[] bytes = new byte[4];
            System.Security.Cryptography.RNGCryptoServiceProvider rng = new System.Security.Cryptography.RNGCryptoServiceProvider();
            rng.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        private void run()
        {
            int i = 0;
            Random rand = new Random(GetRandomSeed());
            while (!this._bStop)
            {
                try
                {
                    int index = 0;
                    if (this._playMode == 0)//按序模式
                    {
                        index = i % this._dt.Rows.Count;
                        i++;
                    }
                    else //if (_playMode == 1)
                    {
                        index = rand.Next() % this._dt.Rows.Count;//随机模式
                    }
                    this.FormLog(NodeMgr._nodeList[this._deviceIndex].Name.PadLeft(10) + " 更新数据第" + index.ToString().PadLeft(4) + "行\n");

                    DataRow Row = this._dt.Rows[index];
                    for (int col = 1; col < this._dt.Columns.Count; col++)//第0列作为时间暂不处理
                    {
                        string ioName = this._dt.Columns[col].ToString();
                        string valueStr = Row[col].ToString();
                        NodeMgr._nodeList[this._deviceIndex].SetValueByName(ioName, valueStr);

                    }
                }
                catch (Exception)
                {

                }
                finally
                {
                    Thread.Sleep(1000);
                }
            }//while
        }




    }
}
