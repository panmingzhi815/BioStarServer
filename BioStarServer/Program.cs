using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Suprema;

namespace BioStarServer
{
    public class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()

        {
            BioSDK sdk = new BioSDK();
            sdk.Init();
            UInt32 deviceID = 0;
            bool conn = sdk.ConnectDevice("192.168.2.247", ref deviceID);
            if (conn)
            {
                sdk.InsertUser(ref deviceID, "123", "潘明智", new string[] {"0575F5" });
                sdk.DisConnectDevice(ref deviceID);
            }

            
        }
    }    
}
