using log4net;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Suprema;
using System.Linq;
using BioStarServer.model;
using log4net.Util;

namespace BioStarServer
{
    internal class DeviceMonitor
    {
        private readonly static ILog _log = LogManager.GetLogger("DeviceMonitor");
        public readonly Device device;
        private readonly BioSDK bioSdk;
        private Timer timer;
        private SpinLock lLock = new SpinLock();
        private byte[] emptyUserIds = new byte[32];
        private uint lastRecordTime = 0;

        private bool Clean_Log;
        private bool Clean_User;
        private bool Get_User_Size;

        public DeviceMonitor(BioSDK bioSdk, Device device)
        {
            this.device = device;
            this.bioSdk = bioSdk;
            this.lastRecordTime = Convert.ToUInt32(device.LastRecordStamp);
        }

        internal void Start(int delay)
        {
            Stop();
            timer = new Timer(TimerCallback, null, delay * 2000, 10000);
        }

        internal void Stop()
        {
            if (timer != null)
            {
                timer.Dispose();
            }
        }

        private void TimerCallback(object state)
        {
            bool lockTaken = false;
            lLock.TryEnter(1000,ref lockTaken);

            if (!lockTaken)
            {
                _log.DebugFormat("设备 {0} 正在处理中 ...",device);
                return;
            }

            uint DeviceId = 0;
            
            try
            {
                var connectDevice = bioSdk.ConnectDevice(device.Ip, ref DeviceId);
                device.Enable = connectDevice ? 1 : 2;
                if (!connectDevice)
                {
                    device.Enable = 2;
                    return;
                }

                if (Clean_Log)
                {
                    bioSdk.CleanLog(ref DeviceId);
                    Clean_Log = false;
                    return;
                }


                if (Clean_User)
                {
                    bioSdk.RemoveAllUser(ref DeviceId);
                    Clean_User = false;
                    return;
                }

                if (Get_User_Size)
                {
                    bioSdk.GetUserSize(ref DeviceId);
                    Get_User_Size = false;
                    return;
                }

                bioSdk.SynDateTime(ref DeviceId,DatabaseHelper.ParseDateTimeTo1970Sec(DateTime.Now.AddHours(8)));

                var geTasks = DatabaseHelper.GeTasks(device.Id);
                foreach (var geTask in geTasks)
                {
                    if (geTask.RecordType == 1)
                    {
                        var cardIds = new List<string>();
                        cardIds.Add(geTask.CardID);
                        var fingers = DatabaseHelper.GetFingers(geTask.CardSN);
                        bool insert = bioSdk.InsertUser(ref DeviceId, geTask.CardSN,geTask.CardType, geTask.UserName, cardIds, fingers.Select(s=>s.Data).ToList());
                        if (insert)
                        {
                            DatabaseHelper.UpdateTask(geTask.Id, 1, 2);
                        }
                    }

                    if (geTask.RecordType == 3)
                    {
                        if(geTask.CardSN == null)
                        {
                            DatabaseHelper.DeleteTask(geTask.Id);
                            continue;
                        }
                        bool remove = bioSdk.RemoveUser(ref DeviceId, geTask.CardSN);
                        if (remove)
                        {
                            DatabaseHelper.DeleteTask(geTask.Id);
                        }
                    }
                }


                var bs2Events = bioSdk.ReadLog(ref DeviceId, lastRecordTime, 100);
                foreach (var bs2Event in bs2Events)
                {
                    var userId = Enumerable.SequenceEqual(bs2Event.userID, emptyUserIds) ? "" : Encoding.UTF8.GetString(bioSdk.Empty(bs2Event.userID));
                    _log.InfoFormat("deviceID:{0} dateTime:{1} userID:{2} code:{3} ", bioSdk.parseIdToIp(bs2Event.deviceID), bs2Event.dateTime, userId,bs2Event.code);

                    lastRecordTime = bs2Event.id;
                    if (userId.Length > 0)
                    {
                        int type = parseRecordCodeType(bs2Event.code);
                        DatabaseHelper.InsertRecord(device.Id, userId, bs2Event.dateTime, bs2Event.code, bs2Event.id,type);
                    }
                    //Thread.Sleep(500);
                }
            }
            catch (Exception e)
            {
                device.Enable = 2;
                _log.ErrorFormat("设备 {0} 通讯时发生异常", device.Ip);
            }
            finally
            {
                if (DeviceId > 0)
                {
                    bioSdk.DisConnectDevice(ref DeviceId);
                }

                lLock.Exit();
            }
        }

        private int parseRecordCodeType(ushort code)
        {
            if (code == 4102)
            {
                return 0;
            }

            if (code == 4865)
            {
                return 39;
            }

            return 16;
        }

        internal void CleanLog()
        {
            Clean_Log = true;
        }

        internal void CleanUser()
        {
            Clean_User = true;
        }

        internal void GetUserSize()
        {
            Get_User_Size = true;
        }
    }
}