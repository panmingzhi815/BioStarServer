using log4net;
using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using Dapper;
using System.Threading;
using Suprema;

namespace BioStarServer
{
    public partial class Main : Form
    {
        private readonly static ILog _log = LogManager.GetLogger("Main");
        private bool stopPrintLog = false;

        private List<Device> deviceList = new List<Device>();
        private List<DeviceMonitor> deviceMonitors = new List<DeviceMonitor>();
        protected internal BioSDK bioSdk;

        public Main()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void Main_Load(object sender, EventArgs e)
        {
            MonitorLogPrint(true);
            ShowComputerInfo();
        }

        private void ShowComputerInfo()
        {
            var hostName = Dns.GetHostName();
            var ipHostEntry = Dns.GetHostEntry(hostName);
            var ipAddresses = ipHostEntry.AddressList.Where(w => w.ToString().Contains(".")).Select(s => s.ToString()).AsList();
            label2.Text = string.Join("/", ipAddresses);
            label4.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            var devices = DatabaseHelper.GetDevices(ipAddresses.ToArray());
            foreach (var device in devices)
            {
                device.Enable = 0;
                device.Ip = device.Ip.Split(':')[0];
                deviceList.Add(device);
            }

            ShowDeviceList();

            bioSdk = new BioSDK();
            if (bioSdk.Init())
            {
                for (int i = 0; i < deviceList.Count; i++)
                {
                    var deviceMonitor = new DeviceMonitor(bioSdk, deviceList[i]);
                    deviceMonitor.Start(i);
                    deviceMonitors.Add(deviceMonitor);
                }
            }
        }

        private void ShowMessageOnUi(object sender, UiLogEventArgs e)
        {
            if (!stopPrintLog)
            {
                SetText(e.Message);
            }
        }

        private delegate void SetTextCallback(string text);
        //在给textBox1.text赋值的地方调用以下方法即可
        private void SetText(string text)
        {
            // InvokeRequired需要比较调用线程ID和创建线程ID
            // 如果它们不相同则返回true
            if (this.textBox1.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                try
                {
                    this.Invoke(d, new object[] { text });
                }
                catch (Exception){}
            }
            else
            {
                this.textBox1.AppendText(text);
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            ShowDeviceList();
        }

        private void ShowDeviceList()
        {
            listView1.Items.Clear();
            foreach (var deviceMonitor in deviceMonitors)
            {
                Device device = deviceMonitor.device;
                var listViewItem = new ListViewItem
                {
                    Text = device.DeviceName + "\r\n" + device.Ip,
                    ImageIndex = device.Enable,
                    Tag = deviceMonitor
                };
                listView1.Items.Add(listViewItem);
            }
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (var deviceMonitor in deviceMonitors)
            {
                deviceMonitor.Stop();
            }
            MonitorLogPrint(false);
        }

        public void MonitorLogPrint(bool monitor)
        {
            var hierarchy = LogManager.GetRepository() as Hierarchy;
            var appenders = hierarchy.Root.Repository.GetAppenders();

            foreach (var appender in appenders)
            {
                var uiLogAppender = appender as UiLogAppender;
                if (uiLogAppender != null && monitor)
                {
                    uiLogAppender.UiLogReceived += ShowMessageOnUi;
                }

                if (uiLogAppender != null && !monitor)
                {
                    uiLogAppender.UiLogReceived -= ShowMessageOnUi;
                }
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            stopPrintLog = checkBox1.Checked;
        }

        private void 清空设备所有记录ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selectedListViewItemCollection = listView1.SelectedItems;
            if (selectedListViewItemCollection.Count == 0)
            {
                MessageBox.Show("请选择要操作的设备", "提示");
                return;
            };

            foreach (var VARIABLE in selectedListViewItemCollection)
            {
                ListViewItem item = (ListViewItem)VARIABLE;
                DeviceMonitor deviceMonitor = (DeviceMonitor) item.Tag;
                deviceMonitor.CleanLog();
            }
        }

        private void 清空设备所有用户ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selectedListViewItemCollection = listView1.SelectedItems;
            if (selectedListViewItemCollection.Count == 0)
            {
                MessageBox.Show("请选择要操作的设备", "提示");
                return;
            };

            foreach (var VARIABLE in selectedListViewItemCollection)
            {
                ListViewItem item = (ListViewItem)VARIABLE;
                DeviceMonitor deviceMonitor = (DeviceMonitor)item.Tag;
                deviceMonitor.CleanUser();
            }
        }
    }
}
