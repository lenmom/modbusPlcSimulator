using System;
using System.Collections.Generic;
using System.Data;



namespace modbusPlcSimulator
{
    internal class NodeMgr
    {
        public static List<Node> _nodeList = new List<Node>();
        private static bool _initFlag = false;//初始化标识




        public static List<Node> getNodeList()
        {
            return _nodeList;
        }
        public static bool init(string cfgFile)
        {
            if (_initFlag)
            {
                stopAll();
                _nodeList.Clear();
            }
            //读csv文件，初始化每个node
            DataTable dt;
            bool ret = CSVReader.readCSV(cfgFile, out dt);
            if (!ret)
            {
                return false;
            }

            try
            {
                for (int i = 0; i < dt.Rows.Count; i++) //写入各行数据
                {

                    int id = int.Parse(dt.Rows[i][0].ToString());
                    int port = int.Parse(dt.Rows[i][1].ToString());
                    string typeStr = dt.Rows[i][2].ToString();

                    Node node = new Node(id, port, typeStr);
                    _nodeList.Add(node);
                }
                _initFlag = true;//已初始化
                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        public static bool openCfgFile(string cfgFile)//打开一个配置文件
        {
            if (string.IsNullOrEmpty(cfgFile))
            {
                return false;
            }

            init(cfgFile);


            return true;
        }

        public static void stopAll()
        {
            foreach (Node node in _nodeList)
            {
                node.stop();
            }
        }

        public static void startAll()
        {
            foreach (Node node in _nodeList)
            {
                node.start();
            }
        }

        public static void startNode(int id)
        {
            foreach (Node node in _nodeList)
            {
                if (node._id == id)
                {
                    node.start();
                }
            }
        }

        public static void stopNode(int id)
        {
            foreach (Node node in _nodeList)
            {
                if (node._id == id)
                {
                    node.stop();
                }
            }
        }



    }
}
