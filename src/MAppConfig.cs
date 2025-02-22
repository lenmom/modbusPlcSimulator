﻿
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace modbusPlcSimulator
{
    public class MAppConfig
    {
        private static Dictionary<string, string> _nameValuePairList = new Dictionary<string, string>();

        public static string GetApplicationPath()
        {
            string ApplicationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName);
            return ApplicationPath;
        }
        public static void TrimString(ref string strLine)
        {
            if (string.IsNullOrEmpty(strLine))
            {
                return;
            }
            try
            {
                int commentPos = strLine.IndexOf("//");
                if (commentPos >= 0)
                {
                    strLine = strLine.Substring(0, commentPos);
                }
                strLine = System.Text.RegularExpressions.Regex.Replace(strLine, "\\s+", " ");
                strLine.Trim();
                if (strLine.Length <= 0)
                {
                    return;
                }
                if (strLine[strLine.Length - 1] == ' ' && strLine.Length > 0)
                {
                    strLine = strLine.Substring(0, strLine.Length - 1);
                }
                if (strLine.Length > 0 && strLine[0] == ' ')
                {
                    strLine = strLine.Substring(1, strLine.Length - 1);
                }
            }
            catch (Exception)
            {
                return;
            }
        }
        public static bool loadIni(string fullPathName, out Dictionary<string, string> retDic)
        {
            retDic = new Dictionary<string, string>();

            string filePath = fullPathName;

            try
            {
                Encoding encoding = Encoding.Default;
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    using (StreamReader sr = new StreamReader(fs, encoding))
                    {
                        string strLine;
                        while ((strLine = sr.ReadLine()) != null)
                        {
                            TrimString(ref strLine);
                            if (strLine.Length < 1)
                            { continue; }

                            int pos = strLine.IndexOf('=');
                            if (pos <= 0)
                            {
                                continue;
                            }

                            string name = strLine.Substring(0, pos);
                            string value = strLine.Substring(pos + 1);

                            TrimString(ref name);
                            TrimString(ref value);

                            if (string.IsNullOrEmpty(name))// || string.IsNullOrEmpty(value))
                            {
                                continue;
                            }

                            name = name.ToLower();
                            value = value.ToLower();

                            if (!retDic.ContainsKey(name))
                            {
                                retDic[name] = value;
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
                ;
            }

        }



        private static readonly Dictionary<string, string> _args = new Dictionary<string, string>();
        public static string getArg(string str) { if (_args.ContainsKey(str)) { return _args[str]; } return string.Empty; }
        public static void setArg(string name, string value) { _args[name] = value; }


        public static bool InitFromFile()
        {
            string fileName = string.Format(@"{0}{1}", GetApplicationPath(), @"\app.cfg");
            if (!File.Exists(fileName))
            {
                fileName = string.Format(@"{0}{1}", GetApplicationPath(), @"\config\app.cfg");
            }

            try
            {
                Dictionary<string, string> retDic;
                if (!loadIni(fileName, out retDic))
                {
                    return false;
                }

                _nameValuePairList = retDic;

                foreach (KeyValuePair<string, string> item in retDic)
                {
                    string name = item.Key;
                    string value = item.Value;
                    //int iValue = 0;
                }
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }


        public static string getValueByName(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                {
                    return string.Empty;
                }

                if (_nameValuePairList.ContainsKey(name.ToLower()))
                {
                    return _nameValuePairList[name.ToLower()];
                }
                return string.Empty;
            }
            catch (System.Exception)
            {
                return string.Empty;
            }
        }

        public static bool setValueByName(string name, string value)
        {
            try
            {
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
                {
                    return false;
                }
                _nameValuePairList[name.ToLower()] = value;
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        public static Dictionary<string, string> GetAppConfig()
        {
            Dictionary<string, string> appDic = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> keyPair in _nameValuePairList)
            {
                appDic[keyPair.Key] = keyPair.Value;
            }

            return appDic;
        }


    }
}
