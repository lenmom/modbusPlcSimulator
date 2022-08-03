﻿using System;
using System.Linq;
using System.Windows.Forms;



namespace modbusPlcSimulator
{
    internal static class Program
    {
        public static LogForm _logForm = null;
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            string turbine;
            if (args.Count<string>() > 0) //用来传入参数
            {
                turbine = args[1];
            }
            if (!MAppConfig.InitFromFile())
            {

            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            _logForm = new LogForm();
            _logForm.Show();//不show 会出问题
            _logForm.Visible = false;

            Application.Run(new Form1());
        }


    }
}
