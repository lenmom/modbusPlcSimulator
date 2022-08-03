using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace modbusPlcSimulator
{
    public partial class DataPlayForm : Form
    {

        // DataPlay _playThread = null;

        private readonly List<DataPlay> _threadList = new List<DataPlay>();
        private bool _isPlaying = false;

        public DataPlayForm()
        {
            this.InitializeComponent();
            //startPlay(null,null);
        }

        private void startPlay(object sender, EventArgs e)
        {
            if (this._isPlaying)
            {
                return;
            }

            for (int i = 0; i < NodeMgr._nodeList.Count; i++)
            {
                DataPlay playThread = new DataPlay(this, i)
                {
                    _playMode = this.getPlayMode()
                };
                this._threadList.Add(playThread);
                playThread.toStart();
            }
            this._isPlaying = true;

        }

        private void stopPlay(object sender, EventArgs e)
        {
            if (this._isPlaying == false)
            {
                return;
            }

            for (int i = 0; i < this._threadList.Count; i++)
            {
                DataPlay playThread = this._threadList[i];
                if (playThread != null)
                {
                    playThread.toStop();
                }
            }
            this._threadList.Clear();
            this._isPlaying = false;

            Thread.Sleep(200);//暂停一下让写线程结束
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < this._threadList.Count; i++)
            {
                DataPlay playThread = this._threadList[i];
                playThread._playMode = this.getPlayMode();
            }
        }
        private int getPlayMode()
        {
            return this.radioButton1.Checked ? 0 : 1;// 0按序，1随机
        }

        private void formClosing(object sender, FormClosingEventArgs e)
        {
            this.stopPlay(sender, e);
        }

        private delegate void addLogCallBack(string text);
        private void AddLogText(string text)
        {
            if (this.richTextBox1.InvokeRequired)
            {
                addLogCallBack stcb = new addLogCallBack(this.AddLogText);
                this.Invoke(stcb, new object[] { text });
            }
            else
            {
                lock (this.richTextBox1)
                {
                    if (this.richTextBox1.Lines.Length > 300)
                    {
                        this.richTextBox1.Clear();
                    }

                    this.richTextBox1.AppendText(text);
                    //设置光标的位置到文本尾   
                    this.richTextBox1.Select(this.richTextBox1.TextLength, 0);
                    //滚动到控件光标处   
                    this.richTextBox1.ScrollToCaret();
                }
            }
        }
        public void log(string logStr)//对外调用的接口
        {
            this.AddLogText(logStr);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.stopPlay(sender, e);
        }


    }
}
