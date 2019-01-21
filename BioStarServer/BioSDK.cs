using BioStarServer;
using log4net;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Suprema
{
    public class BioSDK
    {
        private readonly ILog _log = LogManager.GetLogger("BioSDK");

        protected IntPtr sdkContext = IntPtr.Zero;
        private const int USER_PAGE_SIZE = 1024;
        private Dictionary<UInt32,string> idMapIp = new Dictionary<UInt32, string>();

        public BioSDK()
        {

        }

        public bool Init()
        {
            sdkContext = API.BS2_AllocateContext();
            if (sdkContext == IntPtr.Zero)
            {
                _log.Error("环境初始化失败");
                return false;
            }

            BS2ErrorCode result = (BS2ErrorCode)API.BS2_Initialize(sdkContext);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                _log.ErrorFormat("环境初始化失败 {0}", result);
                API.BS2_ReleaseContext(sdkContext);
                return false;
            }

            return true;
        }

        public bool ConnectDevice(string deviceIP, ref UInt32 deviceID)
        {
            try
            {
                _log.InfoFormat("正在连接 {0}", deviceIP);
                BS2ErrorCode result = (BS2ErrorCode)API.BS2_ConnectDeviceViaIP(sdkContext, deviceIP, 51211, out deviceID);
                if (BS2ErrorCode.BS_SDK_SUCCESS != result)
                {
                    _log.ErrorFormat("连接设备失败, 错误码 : {0} 设备IP {1}", result, deviceIP);
                    return false;
                }
                if (!idMapIp.ContainsKey(deviceID))
                {
                    idMapIp.Add(deviceID, deviceIP);
                }
                _log.ErrorFormat("连接设备成功 设备ID : {0}", deviceID);
                return true;
            }
            catch (Exception)
            {
                _log.ErrorFormat("连接设备异常, 设备IP {0}", deviceIP);
                return false;
            }
        }

        public string parseIdToIp(UInt32 deviceId)
        {
            string deviceIp = deviceId.ToString();
            idMapIp.TryGetValue(deviceId,out deviceIp);
            return deviceIp;
        }

        public bool DisConnectDevice(ref UInt32 deviceID)
        {
            try
            {
                _log.InfoFormat("正在断开 {0}", parseIdToIp(deviceID));
                BS2ErrorCode result = (BS2ErrorCode)API.BS2_DisconnectDevice(sdkContext, deviceID);
                if (BS2ErrorCode.BS_SDK_SUCCESS != result)
                {
                    _log.ErrorFormat("断开设备失败 : {0} 设备ID : {1}", result, parseIdToIp(deviceID));
                    return false;
                }

                _log.ErrorFormat("断开设备成功 : {0} 设备ID : {1}", result, parseIdToIp(deviceID));
                return true;
            }
            catch (Exception)
            {
                _log.ErrorFormat("断开设备异常，设备ID : {0}", parseIdToIp(deviceID));
                return false;
            }
        }

        public bool InsertUser(ref UInt32 deviceID, BS2UserBlob userBlob)
        {
            _log.InfoFormat("准备插入用户到设备 {0}", parseIdToIp(deviceID));
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_EnrolUser(sdkContext, deviceID, new BS2UserBlob[] { userBlob }, 1, 1);

            if (userBlob.cardObjs != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(userBlob.cardObjs);
            }

            if (userBlob.fingerObjs != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(userBlob.fingerObjs);
            }

            if (userBlob.faceObjs != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(userBlob.faceObjs);
            }

            if (result == BS2ErrorCode.BS_SDK_ERROR_DUPLICATE_CARD)
            {
                string userId = Encoding.UTF8.GetString(Empty(userBlob.user.userID));
                _log.ErrorFormat("卡片己存在, 先从 设备ID : {0} 删除用户 {1}", parseIdToIp(deviceID), userId);
                RemoveUser(ref deviceID, userId);
                return false;
            }
            else if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                _log.ErrorFormat("插入用户失败, 状态码 : {0} 设备ID : {1}", result, parseIdToIp(deviceID));
                return false;
            }

            _log.ErrorFormat("插入用户成功, 状态码 : {0} 设备ID : {1} ，结果 : {2}", result, parseIdToIp(deviceID), result == BS2ErrorCode.BS_SDK_SUCCESS);
            return result == BS2ErrorCode.BS_SDK_SUCCESS;
        }

        public bool RemoveUser(ref UInt32 deviceID, string userID)
        {
            _log.InfoFormat("从设备 {0} 删除用户 {1}", parseIdToIp(deviceID), userID);
            byte[] uidArray = new byte[BS2Envirionment.BS2_USER_ID_SIZE];
            byte[] rawUid = Encoding.UTF8.GetBytes(userID);
            IntPtr uids = Marshal.AllocHGlobal(BS2Envirionment.BS2_USER_ID_SIZE);

            Array.Clear(uidArray, 0, BS2Envirionment.BS2_USER_ID_SIZE);
            Array.Copy(rawUid, 0, uidArray, 0, rawUid.Length);
            Marshal.Copy(uidArray, 0, uids, BS2Envirionment.BS2_USER_ID_SIZE);
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_RemoveUser(sdkContext, deviceID, uids, 1);
            
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                _log.ErrorFormat("删除用户:{0} 失败, 状态码 : {1} 设备ID : {2}", userID, result, parseIdToIp(deviceID));
                return false;
            }

            _log.ErrorFormat("删除用户:{0} 成功, 状态码 : {1} 设备ID : {2}", userID, result, parseIdToIp(deviceID));
            return result == BS2ErrorCode.BS_SDK_SUCCESS;
        }


        public bool InsertUser(ref UInt32 deviceID,string userID,int type, string userName,List<string> cardIds,List<byte[]> fingerList)
        {
            _log.InfoFormat("准备添加用户 : 用户ID={0} 用户姓名={1} 卡内码={2} 卡类型={3} 指纹数量={4}",userID,userName,string.Join(",",cardIds), type, fingerList.Count);

            BS2UserBlob userBlob = Util.AllocateStructure<BS2UserBlob>();
            userBlob.user.version = 0;
            userBlob.user.formatVersion = 0;
            userBlob.user.faceChecksum = 0;
            userBlob.user.fingerChecksum = 0;
            userBlob.user.numCards = (byte)cardIds.Count;
            userBlob.user.numFingers = (byte)fingerList.Count; ;
            userBlob.user.numFaces = 0;
            userBlob.user.flag = 0;
            userBlob.cardObjs = IntPtr.Zero;
            userBlob.fingerObjs = IntPtr.Zero;
            userBlob.faceObjs = IntPtr.Zero;

            userBlob.setting.startTime = 0;
            userBlob.setting.endTime = DatabaseHelper.ParseDateTimeTo1970Sec(DateTime.Now.AddYears(10));
            userBlob.setting.cardAuthMode = (byte)BS2CardAuthModeEnum.NONE;
            userBlob.setting.fingerAuthMode = (byte)BS2FingerAuthModeEnum.NONE;
            userBlob.setting.idAuthMode = (byte)BS2IDAuthModeEnum.NONE;
            userBlob.accessGroupId[0] = 0;

            byte[] userIDArray = Encoding.UTF8.GetBytes(userID);
            Array.Clear(userBlob.user.userID, 0, BS2Envirionment.BS2_USER_ID_SIZE);
            Array.Copy(userIDArray, userBlob.user.userID, userIDArray.Length);

            if (cardIds.Count > 0)
            {
                userBlob.cardObjs = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(BS2CSNCard)) * userBlob.user.numCards);
                IntPtr curCardObjs = userBlob.cardObjs;

                foreach (var cardId in cardIds)
                {
                    byte cardType = Convert.ToByte(type);
                    byte cardSize = Convert.ToByte(32);
                    byte[] cardData = Hex2bytes(cardId, 32);

                    Marshal.WriteByte(curCardObjs, cardType);
                    curCardObjs += 1;
                    Marshal.WriteByte(curCardObjs, cardSize);
                    curCardObjs += 1;
                    Marshal.Copy(cardData, 0, curCardObjs, BS2Envirionment.BS2_CARD_DATA_SIZE);
                    curCardObjs += BS2Envirionment.BS2_CARD_DATA_SIZE;
                }

            }

            if (fingerList.Count > 0)
            {
                userBlob.fingerObjs = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(BS2Fingerprint)) * userBlob.user.numFingers);
                IntPtr curFingerObjs = userBlob.fingerObjs;

                for (int i = 0; i < fingerList.Count; i++)
                {
                    byte fingerIndex = Convert.ToByte(i);
                    byte fingerFlag = Convert.ToByte(0);
                    byte[] templateData = fingerList[i];

                    Marshal.WriteByte(curFingerObjs, fingerIndex);
                    curFingerObjs += 1;
                    Marshal.WriteByte(curFingerObjs, fingerFlag);
                    curFingerObjs += 3;
                    Marshal.Copy(templateData, 0, curFingerObjs, BS2Envirionment.BS2_TEMPLATE_PER_FINGER * BS2Envirionment.BS2_FINGER_TEMPLATE_SIZE);
                    curFingerObjs += BS2Envirionment.BS2_TEMPLATE_PER_FINGER * BS2Envirionment.BS2_FINGER_TEMPLATE_SIZE;
                }
            }

            return InsertUser(ref deviceID, userBlob);
        }

        public bool CleanLog(ref UInt32 deviceID)
        {
            _log.InfoFormat("清空设备 {0} 删除所有记录", parseIdToIp(deviceID));
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_ClearLog(sdkContext, deviceID);
            _log.InfoFormat("清空设备 {0} 删除所有记录结果 {1}", parseIdToIp(deviceID), result == BS2ErrorCode.BS_SDK_SUCCESS);
            return result == BS2ErrorCode.BS_SDK_SUCCESS;
        }

        public bool RemoveAllUser(ref UInt32 deviceID)
        {
            _log.InfoFormat("从设备 {0} 删除所有用户", parseIdToIp(deviceID));
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_RemoveAllUser(sdkContext, deviceID);
            _log.InfoFormat("从设备 {0} 删除所有用户结果 {1}", parseIdToIp(deviceID), result == BS2ErrorCode.BS_SDK_SUCCESS);
            return result == BS2ErrorCode.BS_SDK_SUCCESS;
        }

        public List<BS2Event> ReadLog(ref UInt32 deviceID,uint start, uint size)
        {
            _log.InfoFormat("开始采集设备记录，起始ID {0}，最多采集数量 {1}", start, size);

            List<BS2Event> events = new List<BS2Event>();
            Type structureType = typeof(BS2Event);
            int structSize = Marshal.SizeOf(structureType);
            IntPtr uid = IntPtr.Zero;
            IntPtr outEventLogObjs = IntPtr.Zero;
            UInt32 outNumEventLogs = 0;

            BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetLog(sdkContext, deviceID, start, size, out outEventLogObjs, out outNumEventLogs);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                _log.ErrorFormat("采集设备 {0} 记录失败：{1}", parseIdToIp(deviceID), result);
            }
            else if (outNumEventLogs > 0)
            {
                IntPtr curEventLogObjs = outEventLogObjs;
                for (int idx = 0; idx < outNumEventLogs; idx++)
                {
                    BS2Event eventLog = (BS2Event)Marshal.PtrToStructure(curEventLogObjs, structureType);
                    Console.WriteLine(Util.GetLogMsg(eventLog));
                    curEventLogObjs = (IntPtr)((long)curEventLogObjs + structSize);
                    events.Add(eventLog);
                }

                API.BS2_ReleaseObject(outEventLogObjs);
                _log.InfoFormat("采集设备 {0} 记录数量：{1}", parseIdToIp(deviceID), outNumEventLogs);
            }
            else
            {
                _log.InfoFormat("采集设备 {0} 记录为0", parseIdToIp(deviceID));
            }

            return events;
        }

        public byte[] Hex2bytes(string hex,int len)
        {
            byte[] inputByteArray = new byte[hex.Length/2];
            for (var x = 0; x < inputByteArray.Length; x++)
            {
                var i = Convert.ToInt32(hex.Substring(x*2,2),16);
                inputByteArray[x] = (byte)Convert.ToInt32(hex.Substring(x * 2, 2), 16);
            }

            byte[] bytes = new byte[len];
            for (int i = 0; i < inputByteArray.Length; i++)
            {
                bytes[len - i - 1] = inputByteArray[inputByteArray.Length - i - 1];
            }

            return bytes;
        }

        public byte[] Empty(byte[] parse)
        {
            for (int i = 0; i < parse.Length; i++)
            {
                if (parse[i] == 0)
                {
                    var bytes = new byte[i];
                    Array.Copy(parse, 0, bytes, 0, bytes.Length);
                    return bytes;
                }
            }
            return new byte[0];
        }
    }

}
