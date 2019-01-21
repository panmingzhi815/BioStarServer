using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
using log4net;
using Suprema;

namespace BioStarServer
{
    public class Program
    {
        private readonly static ILog _log = LogManager.GetLogger("Program");

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            _log.Info("系统启动");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Main());
            _log.Info("系统退出");
            //BioSDK sdk = new BioSDK();
            //sdk.Init();
            //UInt32 deviceID = 0;
            //bool conn = sdk.ConnectDevice("192.168.2.249", ref deviceID);
            //if (conn)
            //{
            //    sdk.RemoveAllUser(ref deviceID);

            //    var cardIds = new List<string>();
            //    var fingers = new List<byte[]>();
            //    cardIds.Add("00FBDA1703");
            //    sdk.InsertUser(ref deviceID, "123", "潘明智", cardIds, fingers);
            //    sdk.DisConnectDevice(ref deviceID);
            //}
            //else
            //{
            //    Console.WriteLine("连接设备失败");
            //}
        }
    }    
}
