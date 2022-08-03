using System;
using System.Windows.Forms;

using Modbus.Data;



namespace modbusPlcSimulator
{
    public partial class RegisterInspector : Form
    {

        public string _selectIdStr { get; set; }
        private Node _node;

        public RegisterInspector(string str)
        {
            this._selectIdStr = str;
            this.InitializeComponent();
            this.SetStyle(ControlStyles.DoubleBuffer | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            this.initUI();
            this.timer1.Start();
        }


        private void initUI()
        {
            this.listView1.Clear();
            this.comboBox1.Items.Clear();
            for (int i = 0; i < NodeMgr._nodeList.Count; i++)
            {
                Node node = NodeMgr._nodeList[i];
                string name = node._name;
                this.comboBox1.Items.Add(name);

                if (node._id.ToString() == this._selectIdStr)
                {
                    this._node = node;
                    this.comboBox1.SelectedIndex = i;
                }
            }
            this.comboBox_Register.SelectedIndex = 0;
            this.refreshUI();
        }//initUI

        private void refreshUI()
        {
            //数据更新，UI暂时挂起，直到EndUpdate绘制控件，可以有效避免闪烁并大大提高加载速度 
            ModbusDataCollection<ushort> data = this.getRegisters();

            int startAdress = 0;
            int length = 1000;
            if (!int.TryParse(this.textBox_adress.Text, out startAdress))
            {
                startAdress = 0;
            }
            if (!int.TryParse(this.textBox_length.Text, out length))
            {
                length = 1000;
            }
            this.listView1.BeginUpdate();

            this.listView1.Clear();
            for (int index = 0; index < length; index++)
            {
                int address = index + startAdress;
                string line = "<" + address.ToString().PadLeft(4, '0') + ">: ";
                ushort value = data[address];
                line += value.ToString();

                ListViewItem item = new ListViewItem
                {
                    Text = line
                };
                this.listView1.Items.Add(item);
            }
            this.listView1.EndUpdate();  //结束数据处理，UI界面一次性绘制。 
        }

        private ModbusDataCollection<ushort> getRegisters()
        {
            ModbusDataCollection<ushort> data = null;
            switch (this.comboBox_Register.SelectedIndex)
            {
                case 0:
                    data = this._node.getDataStore().HoldingRegisters;
                    break;//03功能
                case 1:
                    data = this._node.getDataStore().InputRegisters;
                    break;//04功能
                default:
                    data = this._node.getDataStore().HoldingRegisters;
                    break;
            }
            return data;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            this.refreshUI();
        }

        private void comboBox1_SelectionChangeCommitted(object sender, EventArgs e)//设备变更
        {
            int index = this.comboBox1.SelectedIndex;
            this._node = NodeMgr._nodeList[index];
            this.refreshUI();
        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.listView1.SelectedItems.Count == 0)
            {
                return;
            }

            MessageBox.Show(this.listView1.FocusedItem.Text);
        }


        private void comboBox_Register_SelectionChangeCommitted(object sender, EventArgs e)
        {
            this.refreshUI();
        }



    }
}
