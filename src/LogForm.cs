using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace modbusPlcSimulator
{
    public partial class LogForm : Form
    {
        private readonly List<string> logList = new List<string>();

        public LogForm()
        {
            //  Application.SetCompatibleTextRenderingDefault(false);

            this.InitializeComponent();
            this.addLog("开始Log:");
        }

        public void addLog(string log)
        {
            string Time = DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss.fff]");//Convert.ToString(DateTime.Now.);
            string logStr = Time + " " + log + "\n";
            this.AddLogText(logStr);
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
                this.richTextBox1.AppendText(text);
                if (this.richTextBox1.Lines.Length > 2000)//超过2000行清空。
                {
                    this.richTextBox1.Clear();
                }
            }
        }


        public void addWriteLog(string log)
        {
            string Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");//Convert.ToString(DateTime.Now.);
            string logStr = Time + " " + log + "\n";
            this.AddWriteLogText(logStr);
        }

        private delegate void addLogCallBack2(string text);
        private void AddWriteLogText(string text)//真正干活的函数
        {
            if (this.richTextBox2.InvokeRequired)
            {
                addLogCallBack stcb = new addLogCallBack(this.AddWriteLogText);
                this.Invoke(stcb, new object[] { text });
            }
            else
            {
                this.richTextBox2.AppendText(text);
                if (this.richTextBox2.Lines.Length > 2000)//超过2000行清空。
                {
                    this.richTextBox2.Clear();
                }
            }
        }


        private void LogForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            switch (e.CloseReason)
            {
                //应用程序要求关闭窗口
                case CloseReason.ApplicationExitCall:
                    e.Cancel = false; //不拦截，响应操作
                    break;
                //自身窗口上的关闭按钮
                case CloseReason.FormOwnerClosing:
                    e.Cancel = true;//拦截，不响应操作
                    break;
                //MDI窗体关闭事件
                //不明原因的关闭
                case CloseReason.None:
                    break;
                //任务管理器关闭进程
                case CloseReason.TaskManagerClosing:
                    e.Cancel = false;//不拦截，响应操作
                    break;
                //用户通过UI关闭窗口或者通过Alt+F4关闭窗口
                case CloseReason.UserClosing:
                    e.Cancel = true;//拦截，不响应操作
                    this.Visible = false;
                    break;
                //操作系统准备关机
                case CloseReason.WindowsShutDown:
                    e.Cancel = false;//不拦截，响应操作
                    break;
                default:
                    break;
            }
        }


    }//class
}
