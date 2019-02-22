using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using BioStarServer.model;
using Dapper;

namespace BioStarServer
{
    class DatabaseHelper
    {
        public static SqlConnection GetConnection()
        {
            string ConnectionString = ConfigurationManager.ConnectionStrings["ConnectionString"].ConnectionString;
            return new SqlConnection(ConnectionString);
        }

        public static List<Task> GeTasks(int DeviceId)
        {
            using (IDbConnection conn = GetConnection())
            {
                return conn.Query<Task>("select TOP 10 ACR.id as Id, PC.SerialNumber as CardSN,PC.PhysicalId as CardID,CU.name as UserName,PC.cardType as CardType,ACR.accessControlState as RecordType from AccessControlRecord ACR LEFT JOIN Device D on ACR.device = D.id LEFT JOIN PhysicalCard PC on ACR.card_id = PC.id LEFT JOIN CardUser CU on PC.cardUser = CU.id WHERE D.id=@Id AND accessControlState in(1,3) ORDER BY ACR.id", new { Id = DeviceId }).AsList();
            }
        }

        public static List<Device> GetDevices(string[] hostName)
        {
            using (IDbConnection conn = GetConnection())
            {
                return conn.Query<Device>("select d.id as Id,L.address as Ip,d.Name as DeviceName,d.groupCodeJoinStr as groupCodeJoinStr,d.groupCodeNameJoinStr as GroupCodeNameJoinStr,d.Identifier as DeviceIdentifier,d.Type as DeviceTypeEnum,d.Identifier as DeviceIdentifier,d.faceAttendanceStamp as LastRecordStamp from Device d left join Link L on d.Link = L.id left join Host H on L.host = H.id where d.Type=@DeviceType and H.name in @HostName", new { DeviceType = "SPMAttendanceDevice", HostName = hostName }).AsList();
            }
        }

        public static List<Finger> GetFingers(string serialNumber)
        {
            using (IDbConnection conn = GetConnection())
            {
                return conn.Query<Finger>("select CUFT.id AS Id,CUFT.tmp AS Data,CUFT.size AS Size from CardUserFingerTemplate CUFT LEFT JOIN CardUser CU on CUFT.cardUser_id = CU.id LEFT JOIN PhysicalCard PC on CU.id = PC.cardUser WHERE PC.SerialNumber=@SerialNumber and CUFT.size = 768 and CUFT.tmp is not null", new { SerialNumber = serialNumber }).AsList();
            }
        }

        public static void UpdateTask(int id, int oldState, int newState)
        {
            using (IDbConnection conn = GetConnection())
            {
                conn.Execute("update AccessControlRecord set accessControlState=@newAccessControlState , uploadTime=@uploadTime where accessControlState=@oldAccessControlState and id=@Id;", new { newAccessControlState = newState, uploadTime = DateTime.Now, oldAccessControlState=oldState, Id = id });
            }
        }

        internal static void InsertRecord(int deviceId, string userId, uint dateTime, ushort code, uint id,int type)
        {
            using (IDbConnection conn = GetConnection())
            {
                Device device = conn.Query<Device>("select d.id as Id,L.address as Ip,d.Name as DeviceName,d.groupCodeJoinStr as groupCodeJoinStr,d.groupCodeNameJoinStr as GroupCodeNameJoinStr,d.Identifier as DeviceIdentifier,d.Type as DeviceTypeEnum from Device d left join Link L on d.Link = L.id left join Host H on L.host = H.id where D.id = @Id",new {Id = deviceId}).SingleOrDefault(c => true);

                if (device == null)
                {
                    return;
                }

                Card card = conn.Query<Card>("select PC.PhysicalId AS CardId,PC.SerialNumber as CardSn,CU.identifier as UserIdentifier,CU.groupCodeJoinStr as UserGroupCodeJoinStr,CU.groupCodeNameJoinStr as UserGroupCodeNameJoinStr,CU.name as UserName from PhysicalCard PC LEFT JOIN CardUser CU on PC.cardUser = CU.id WHERE PC.SerialNumber=@SerialNumber",new {SerialNumber = userId}).SingleOrDefault(c => true);

                if (card == null)
                {
                    card = new Card();
                }


                conn.Execute("insert into CardUsage (type, cardid, cardSerialNumber, deviceGroupCodeJoinStr, deviceGroupCodeNameJoinStr, deviceIdentifier, deviceName, deviceTypeEnum, eventType, eventtime, timestamp, userGroupCodeJoinStr, userGroupCodeNameJoinStr, userIdentifier, userName,doorOpen,doorSwitch,emergency,preventBreak) values" +
                             " ('normal',@cardid,@cardSerialNumber,@deviceGroupCodeJoinStr,@deviceGroupCodeNameJoinStr,@deviceIdentifier, " +
                             "@deviceName,@deviceTypeEnum,@eventType,@eventtime,@timestamp,@userGroupCodeJoinStr,@userGroupCodeNameJoinStr,@userIdentifier,@userName,@doorOpen,@doorSwitch,@emergency,@preventBreak)", 
                    new
                    {
                        cardid = card.CardId,
                        cardSerialNumber = card.CardSn,
                        deviceGroupCodeJoinStr = device.GroupCodeJoinStr,
                        deviceGroupCodeNameJoinStr = device.GroupCodeNameJoinStr,
                        deviceIdentifier = device.DeviceIdentifier,
                        deviceName = device.DeviceName,
                        deviceTypeEnum = device.DeviceTypeEnum,
                        eventType = type,
                        eventtime = GetDateTimeFrom1970Ticks(dateTime),
                        timestamp = DateTime.Now,
                        userGroupCodeJoinStr  = card.UserGroupCodeJoinStr,
                        userGroupCodeNameJoinStr = card.UserGroupCodeNameJoinStr,
                        userIdentifier = card.UserIdentifier,
                        userName = card.UserName,
                        doorOpen = 0,
                        doorSwitch = 0,
                        emergency = 0,
                        preventBreak = 0
                    });

                conn.Execute("update Device set faceAttendanceStamp=@faceAttendanceStamp where id=@id", new {faceAttendanceStamp = Convert.ToString(id), id = deviceId});
            }
        }

        internal static void DeleteTask(int id)
        {
            using (IDbConnection conn = GetConnection())
            {
                conn.Execute("delete from AccessControlRecord where id=@Id", new {Id = id});
            }
        }

        public static DateTime GetDateTimeFrom1970Ticks(long curSeconds)
        {
            DateTime dtStart = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1));
            return dtStart.AddSeconds(curSeconds);
        }

        public static uint ParseDateTimeTo1970Sec(DateTime dateTime)
        {
            DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 当地时区
            return (uint)(dateTime - startTime).TotalSeconds; // 相差秒数
        }
    }
}
