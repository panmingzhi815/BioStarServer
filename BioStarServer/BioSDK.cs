using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Suprema
{
    public class BioSDK
    {
        protected IntPtr sdkContext = IntPtr.Zero;

        public BioSDK()
        {

        }

        public bool Init()
        {
            sdkContext = API.BS2_AllocateContext();
            if (sdkContext == IntPtr.Zero)
            {
                Console.WriteLine("环境初始化失败");
                return false;
            }

            BS2ErrorCode result = (BS2ErrorCode)API.BS2_Initialize(sdkContext);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("初始化SDK失败 : {0}", result);
                API.BS2_ReleaseContext(sdkContext);
                return false;
            }

            return true;
        }

        public bool ConnectDevice(string deviceIP, ref UInt32 deviceID)
        { 
            BS2ErrorCode result = (BS2ErrorCode) API.BS2_ConnectDeviceViaIP(sdkContext, deviceIP, 51211, out deviceID);
            if (BS2ErrorCode.BS_SDK_SUCCESS != result)
            {
                Console.WriteLine("连接设备失败 : {0} 设备IP {1}", result,deviceIP);
                return false;
            }

            return true;
        }

        public bool DisConnectDevice(ref UInt32 deviceID)
        {
            BS2ErrorCode result = (BS2ErrorCode) API.BS2_DisconnectDevice(sdkContext, deviceID);
            if (BS2ErrorCode.BS_SDK_SUCCESS != result)
            {
                Console.WriteLine("断开设备失败 : {0} 设备ID : {1}", result, deviceID);
                return false;
            }

            return true;
        }

        public bool InsertUser(ref UInt32 deviceID, BS2UserBlob[] userBlob)
        {
            Console.WriteLine("准备插入用户到设备");
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_EnrolUser(sdkContext, deviceID, userBlob, 1, 1);
            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("插入用户失败 : {0} 设备ID : {1}", result, deviceID);
            }

            if (userBlob[0].cardObjs != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(userBlob[0].cardObjs);
            }

            if (userBlob[0].fingerObjs != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(userBlob[0].fingerObjs);
            }

            if (userBlob[0].faceObjs != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(userBlob[0].faceObjs);
            }

            Console.WriteLine("插入用户成功 : {0} 设备ID : {1}", result, deviceID);
            return result == BS2ErrorCode.BS_SDK_SUCCESS;
        }

        public bool InsertUser(ref UInt32 deviceID,string userID, string userName,string[] cardIds)
        {
            Console.WriteLine("准备添加用户 : 用户ID={0} 用户姓名={1} 卡内码={2}",userID,userName,string.Join(",",cardIds));

            BS2UserBlob userBlob = Util.AllocateStructure<BS2UserBlob>();
            userBlob.user.version = 0;
            userBlob.user.formatVersion = 0;
            userBlob.user.faceChecksum = 0;
            userBlob.user.fingerChecksum = 0;
            userBlob.user.numCards = 0;
            userBlob.user.numFingers = 0;
            userBlob.user.numFaces = 0;
            userBlob.user.flag = 0;
            userBlob.cardObjs = IntPtr.Zero;
            userBlob.fingerObjs = IntPtr.Zero;
            userBlob.faceObjs = IntPtr.Zero;

            byte[] userIDArray = Encoding.UTF8.GetBytes(userID);
            Array.Clear(userBlob.user.userID, 0, BS2Envirionment.BS2_USER_ID_SIZE);
            Array.Copy(userIDArray, userBlob.user.userID, userIDArray.Length);

            userBlob.user.numCards = (byte)cardIds.Length;
            userBlob.cardObjs = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(BS2CSNCard)) * userBlob.user.numCards);
            IntPtr curCardObjs = userBlob.cardObjs;

            foreach (var cardId in cardIds)
            {
                byte[] bytes = Hex2bytes(cardId,32);

                byte cardType = Convert.ToByte(26);
                byte cardSize = Convert.ToByte(bytes.Length);
                byte[] cardData = bytes;

                Marshal.WriteByte(curCardObjs, cardType);
                curCardObjs += 1;
                Marshal.WriteByte(curCardObjs, cardSize);
                curCardObjs += 1;
                Marshal.Copy(cardData, 0, curCardObjs, BS2Envirionment.BS2_CARD_DATA_SIZE);
                curCardObjs += BS2Envirionment.BS2_CARD_DATA_SIZE;
            }

            return InsertUser(ref deviceID,new BS2UserBlob[1]{ userBlob });
        }

        public byte[] Hex2bytes(string hex,int len)
        {
            byte[] inputByteArray = new byte[hex.Length / 2];
            for (var x = 0; x < inputByteArray.Length; x++)
            {
                var i = Convert.ToInt32(hex.Substring(x * 2, 2), 16);
                inputByteArray[x] = (byte)i;
            }

            byte[] bytes = new byte[len];
            for (int i = 0; i < inputByteArray.Length; i++)
            {
                bytes[len - inputByteArray.Length + i] = inputByteArray[i];
            }

            return bytes;
        }
    }

}
