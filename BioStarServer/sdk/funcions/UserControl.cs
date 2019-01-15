using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Data.SQLite;

namespace Suprema
{
    using BS2_USER_MASK = UInt16;

    class DataBaseHandler : IDisposable
    {
        SQLiteConnection connection;

        public DataBaseHandler()
        {
            connection = openDataBase();
        }

        public void Dispose()
        {
            connection.Close();
        }

        SQLiteConnection openDataBase()
        {
            string dbPath = "Data Source=user.db;foreign keys=true;";

            SQLiteConnection connection = new SQLiteConnection(dbPath);
            connection.Open();

            SQLiteCommand cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS User(id CHAR(32) NOT NULL, formatVersion INTEGER, flag INTEGER, version INTEGER, fingerChecksum INTEGER, faceChecksum INTEGER, PRIMARY KEY(id))", connection);
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS BS2UserSetting(userID CHAR(32), startTime INTEGER, endTime INTEGER, fingerAuthMode INTEGER, cardAuthMode INTEGER, idAuthMode INTEGER, securityLevel INTEGER, FOREIGN KEY(userID) REFERENCES User(id) ON DELETE CASCADE)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS BS2UserName(userID CHAR(32), userName CHAR(192), FOREIGN KEY(userID) REFERENCES User(id) ON DELETE CASCADE)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS BS2PinCode(userID CHAR(32), hash BLOB, FOREIGN KEY(userID) REFERENCES User(id) ON DELETE CASCADE)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS BS2CSNCard(userID CHAR(32), type INTEGER, size INTEGER, data BLOB UNIQUE, FOREIGN KEY(userID) REFERENCES User(id) ON DELETE CASCADE)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS BS2Fingerprint(userID CHAR(32), fingerIndex INTEGER, flag INTEGER, data BLOB UNIQUE, templateFormat INTEGER NOT NULL, FOREIGN KEY(userID) REFERENCES User(id) ON DELETE CASCADE)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS BS2Face(userID CHAR(32), faceIndex INTEGER, numOfTemplate INTEGER, flag INTEGER, imageLen INTEGER, imageData BLOB UNIQUE, templateData BLOB UNIQUE, FOREIGN KEY(userID) REFERENCES User(id) ON DELETE CASCADE)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS UserAccessGroup(userID CHAR(32), accessGroupId INTEGER, FOREIGN KEY(userID) REFERENCES User(id) ON DELETE CASCADE)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS BS2UserPhoto(userID CHAR(32), size INTEGER, data BLOB, FOREIGN KEY(userID) REFERENCES User(id) ON DELETE CASCADE)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS BS2UserPhrase(userID CHAR(32), userPhrase CHAR(128), FOREIGN KEY(userID) REFERENCES User(id) ON DELETE CASCADE)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS BS2Job(userID CHAR(32), code INTEGER, label CHAR(48), FOREIGN KEY(userID) REFERENCES User(id) ON DELETE CASCADE)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE VIEW IF NOT EXISTS BS2User AS SELECT User.id AS userID, User.formatVersion, User.flag, User.version, User.fingerChecksum, User.faceChecksum, (SELECT COUNT(BS2CSNCard.userID) FROM BS2CSNCard WHERE BS2CSNCARD.userID = User.id) AS numCards, (SELECT COUNT(BS2Fingerprint.userID) FROM BS2Fingerprint WHERE BS2Fingerprint.userID = User.id) as numFingers, (SELECT COUNT(BS2Face.userID) FROM BS2Face WHERE BS2Face.userID = User.id) AS numFaces FROM User";
            cmd.ExecuteNonQuery();

            return connection;
        }

        public bool RemoveUser(string userID)
        {
            byte[] targetUid = new byte[BS2Envirionment.BS2_USER_ID_SIZE];
            byte[] uid = Encoding.UTF8.GetBytes(userID);

            Array.Clear(targetUid, 0, BS2Envirionment.BS2_USER_ID_SIZE);
            Array.Copy(uid, 0, targetUid, 0, uid.Length);
            SQLiteCommand cmd = new SQLiteCommand("DELETE FROM User WHERE id = @idParam", connection);

            cmd.Parameters.AddWithValue("@idParam", targetUid);
            if (cmd.ExecuteNonQuery() < 1)
            {
                return false;
            }

            return true;
        }

        public bool GetUserList(ref BS2SimpleDeviceInfo deviceInfo, ref List<BS2User> userList)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT userID, formatVersion, flag, version, fingerChecksum, faceChecksum, numCards, numFingers, numFaces FROM BS2User", connection);
            SQLiteDataReader rdr = cmd.ExecuteReader();

            userList.Clear();

            while (rdr.Read())
            {
                BS2User user = Util.AllocateStructure<BS2User>();
                Array.Clear(user.userID, 0, BS2Envirionment.BS2_USER_ID_SIZE);
                string userID = (string)rdr[0];
                byte[] uid = Encoding.UTF8.GetBytes(userID);
                Array.Copy(uid, 0, user.userID, 0, uid.Length);
                user.formatVersion = Convert.ToByte(rdr[1]);
                user.flag = Convert.ToByte(rdr[2]);
                user.version = Convert.ToUInt16(rdr[3]);
                user.fingerChecksum = Convert.ToUInt32(rdr[4]);
                user.faceChecksum = Convert.ToUInt32(rdr[5]);

                if (Convert.ToBoolean(deviceInfo.cardSupported))
                {
                    user.numCards = Convert.ToByte(rdr[6]);
                }
                else
                {
                    user.numCards = 0;
                }

                if (Convert.ToBoolean(deviceInfo.fingerSupported))
                {
                    user.numFingers = Convert.ToByte(rdr[7]);
                }
                else
                {
                    user.numFingers = 0;
                }

                if (Convert.ToBoolean(deviceInfo.faceSupported))
                {
                    user.numFaces = Convert.ToByte(rdr[8]);
                }
                else
                {
                    user.numFaces = 0;
                }

                userList.Add(user);
            }

            rdr.Close();
            return true;
        }

        public bool GetUserBlob(ref BS2SimpleDeviceInfo deviceInfo, ref BS2CSNCard csnCard, ref BS2UserBlob userBlob)
        {
            string userID = "";
            SQLiteCommand cmd = new SQLiteCommand("SELECT userID FROM BS2CSNCard WHERE type = @typeParam AND size = @sizeParam AND data = @dataParam", connection);
            cmd.Parameters.AddWithValue("@typeParam", csnCard.type);
            cmd.Parameters.AddWithValue("@sizeParam", csnCard.size);
            cmd.Parameters.AddWithValue("@dataParam", csnCard.data);

            SQLiteDataReader rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                userID = (string)rdr[0];
            }
            rdr.Close();

            return GetUserBlob(ref deviceInfo, userID, ref userBlob);
        }

        public bool GetUserBlob(ref BS2SimpleDeviceInfo deviceInfo, string userID, ref BS2UserBlob userBlob)
        {
            if (userID.Length > 0)
            {
                BS2User targetUser = Util.AllocateStructure<BS2User>();

                Array.Clear(targetUser.userID, 0, BS2Envirionment.BS2_USER_ID_SIZE);
                byte[] uid = Encoding.UTF8.GetBytes(userID);
                Array.Copy(uid, 0, targetUser.userID, 0, uid.Length);

                SQLiteCommand cmd = new SQLiteCommand("SELECT userID, formatVersion, flag, version, fingerChecksum, faceChecksum, numCards, numFingers, numFaces FROM BS2User WHERE userID = @userIDParam", connection);
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);
                SQLiteDataReader rdr = cmd.ExecuteReader();

                if (rdr.Read())
                {
                    targetUser.formatVersion = Convert.ToByte(rdr[1]);
                    targetUser.flag = Convert.ToByte(rdr[2]);
                    targetUser.version = Convert.ToUInt16(rdr[3]);
                    targetUser.fingerChecksum = Convert.ToUInt32(rdr[4]);
                    targetUser.faceChecksum = Convert.ToUInt32(rdr[5]);

                    if (Convert.ToBoolean(deviceInfo.cardSupported))
                    {
                        targetUser.numCards = Convert.ToByte(rdr[6]);
                    }
                    else
                    {
                        targetUser.numCards = 0;
                    }

                    if (Convert.ToBoolean(deviceInfo.fingerSupported))
                    {
                        targetUser.numFingers = Convert.ToByte(rdr[7]);
                    }
                    else
                    {
                        targetUser.numFingers = 0;
                    }

                    if (Convert.ToBoolean(deviceInfo.faceSupported))
                    {
                        targetUser.numFaces = Convert.ToByte(rdr[7]);
                    }
                    else
                    {
                        targetUser.numFaces = 0;
                    }

                    return GetUserBlob(ref deviceInfo, ref targetUser, ref userBlob);
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        public bool GetUserBlob(ref BS2SimpleDeviceInfo deviceInfo, ref BS2User targetUser, ref BS2UserBlob userBlob)
        {
            userBlob.user = targetUser;

            SQLiteCommand cmd = new SQLiteCommand("SELECT startTime, endTime, fingerAuthMode, cardAuthMode, idAuthMode, securityLevel FROM BS2UserSetting WHERE userID = @userIDParam", connection);
            cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);
            SQLiteDataReader rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                userBlob.setting.startTime = Convert.ToUInt32(rdr[0]);
                userBlob.setting.endTime = Convert.ToUInt32(rdr[1]);
                userBlob.setting.fingerAuthMode = Convert.ToByte(rdr[2]);
                userBlob.setting.cardAuthMode = Convert.ToByte(rdr[3]);
                userBlob.setting.idAuthMode = Convert.ToByte(rdr[4]);
                userBlob.setting.securityLevel = Convert.ToByte(rdr[5]);
            }
            rdr.Close();

            Array.Clear(userBlob.name, 0, BS2Envirionment.BS2_USER_NAME_LEN);
            if (Convert.ToBoolean(deviceInfo.userNameSupported))
            {
                cmd.CommandText = "SELECT userName from BS2UserName WHERE userID = @userIDParam";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);
                rdr = cmd.ExecuteReader();

                if (rdr.Read())
                {
                    byte[] userName = Encoding.UTF8.GetBytes((string)rdr[0]);
                    Array.Copy(userName, 0, userBlob.name, 0, userName.Length);
                }
                rdr.Close();
            }

            userBlob.photo.size = 0;
            Array.Clear(userBlob.photo.data, 0, BS2Envirionment.BS2_USER_PHOTO_SIZE);
            if (Convert.ToBoolean(deviceInfo.userPhotoSupported))
            {
                cmd.CommandText = "SELECT size, data from BS2UserPhoto WHERE userID = @userIDParam";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);
                rdr = cmd.ExecuteReader();

                if (rdr.Read())
                {
                    UInt32 photoSize = Convert.ToUInt32(rdr[0]);
                    byte[] photoData = (byte[])rdr[1];

                    userBlob.photo.size = photoSize;
                    if (photoSize > 0)
                    {
                        Array.Copy(photoData, 0, userBlob.photo.data, 0, photoSize);
                    }
                }
                rdr.Close();
            }

            Array.Clear(userBlob.pin, 0, BS2Envirionment.BS2_PIN_HASH_SIZE);
            if (Convert.ToBoolean(deviceInfo.pinSupported))
            {
                cmd.CommandText = "SELECT hash from BS2PinCode WHERE userID = @userIDParam";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);
                rdr = cmd.ExecuteReader();

                if (rdr.Read())
                {
                    byte[] pinCode = (byte[])rdr[0];
                    Array.Copy(pinCode, 0, userBlob.pin, 0, pinCode.Length);
                }
                rdr.Close();
            }

            if (targetUser.numCards > 0)
            {
                cmd.CommandText = "SELECT type, size, data from BS2CSNCard WHERE userID = @userIDParam";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);

                rdr = cmd.ExecuteReader();
                userBlob.cardObjs = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(BS2CSNCard)) * targetUser.numCards);
                IntPtr curCardObjs = userBlob.cardObjs;

                while (rdr.Read())
                {
                    byte cardType = Convert.ToByte(rdr[0]);
                    byte cardSize = Convert.ToByte(rdr[1]);
                    byte[] cardData = (byte[])rdr[2];

                    Marshal.WriteByte(curCardObjs, cardType);
                    curCardObjs += 1;
                    Marshal.WriteByte(curCardObjs, cardSize);
                    curCardObjs += 1;
                    Marshal.Copy(cardData, 0, curCardObjs, BS2Envirionment.BS2_CARD_DATA_SIZE);
                    curCardObjs += BS2Envirionment.BS2_CARD_DATA_SIZE;
                }
                rdr.Close();
            }
            else
            {
                userBlob.cardObjs = IntPtr.Zero;
            }

            if (targetUser.numFingers > 0)
            {
                cmd.CommandText = "SELECT fingerIndex, flag, data FROM BS2Fingerprint WHERE userID = @userIDParam";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);

                rdr = cmd.ExecuteReader();
                userBlob.fingerObjs = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(BS2Fingerprint)) * targetUser.numFingers);
                IntPtr curFingerObjs = userBlob.fingerObjs;

                while (rdr.Read())
                {
                    byte fingerIndex = Convert.ToByte(rdr[0]);
                    byte fingerFlag = Convert.ToByte(rdr[1]);
                    byte[] templateData = (byte[])rdr[2];

                    Marshal.WriteByte(curFingerObjs, fingerIndex);
                    curFingerObjs += 1;
                    Marshal.WriteByte(curFingerObjs, fingerFlag);
                    curFingerObjs += 3;
                    Marshal.Copy(templateData, 0, curFingerObjs, BS2Envirionment.BS2_TEMPLATE_PER_FINGER * BS2Envirionment.BS2_FINGER_TEMPLATE_SIZE);
                    curFingerObjs += BS2Envirionment.BS2_TEMPLATE_PER_FINGER * BS2Envirionment.BS2_FINGER_TEMPLATE_SIZE;
                }
                rdr.Close();
            }
            else
            {
                userBlob.fingerObjs = IntPtr.Zero;
            }

            if (targetUser.numFaces > 0)
            {
                cmd.CommandText = "SELECT faceIndex, numOfTemplate, flag, imageLen, imageData, templateData FROM BS2Face WHERE userID = @userIDParam";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);

                rdr = cmd.ExecuteReader();
                userBlob.faceObjs = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(BS2Face)) * targetUser.numFaces);
                IntPtr curFaceObjs = userBlob.faceObjs;

                while (rdr.Read())
                {
                    byte faceIndex = Convert.ToByte(rdr[0]);
                    byte numOfTemplate = Convert.ToByte(rdr[1]);
                    byte flag = Convert.ToByte(rdr[2]);
                    UInt16 imageLen = Convert.ToUInt16(rdr[3]);

                    byte[] imageData = (byte[])rdr[4];
                    byte[] templateData = (byte[])rdr[5];

                    Marshal.WriteByte(curFaceObjs, faceIndex);
                    curFaceObjs += 1;
                    Marshal.WriteByte(curFaceObjs, numOfTemplate);
                    curFaceObjs += 1;
                    Marshal.WriteByte(curFaceObjs, flag);
                    curFaceObjs += 2; //flag(1) + reserved(1)
                    Marshal.WriteInt16(curFaceObjs, (Int16)imageLen);
                    curFaceObjs += 4; //imageLen(2) + reserved(2)
                    Marshal.Copy(imageData, 0, curFaceObjs, BS2Envirionment.BS2_FACE_IMAGE_SIZE);
                    curFaceObjs += BS2Envirionment.BS2_FACE_IMAGE_SIZE;
                    Marshal.Copy(templateData, 0, curFaceObjs, BS2Envirionment.BS2_TEMPLATE_PER_FACE * BS2Envirionment.BS2_FACE_TEMPLATE_LENGTH);
                    curFaceObjs += BS2Envirionment.BS2_TEMPLATE_PER_FACE * BS2Envirionment.BS2_FACE_TEMPLATE_LENGTH;
                }
                rdr.Close();
            }
            else
            {
                userBlob.faceObjs = IntPtr.Zero;
            }

            Array.Clear(userBlob.accessGroupId, 0, BS2Envirionment.BS2_MAX_ACCESS_GROUP_PER_USER);
            int access_group_count = 0;
            cmd.CommandText = "SELECT accessGroupId from UserAccessGroup WHERE userID = @userIDParam";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);

            rdr = cmd.ExecuteReader();

            while (rdr.Read())
            {
                userBlob.accessGroupId[access_group_count++] = Convert.ToUInt32(rdr[0]);
                if (access_group_count >= BS2Envirionment.BS2_MAX_ACCESS_GROUP_PER_USER)
                {
                    break;
                }
            }
            rdr.Close();

            return true;
        }

        public bool GetUserBlobEx(ref BS2SimpleDeviceInfo deviceInfo, ref BS2CSNCard csnCard, ref BS2UserBlobEx userBlob)
        {
            string userID = "";
            SQLiteCommand cmd = new SQLiteCommand("SELECT userID FROM BS2CSNCard WHERE type = @typeParam AND size = @sizeParam AND data = @dataParam", connection);
            cmd.Parameters.AddWithValue("@typeParam", csnCard.type);
            cmd.Parameters.AddWithValue("@sizeParam", csnCard.size);
            cmd.Parameters.AddWithValue("@dataParam", csnCard.data);

            SQLiteDataReader rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                userID = (string)rdr[0];
            }
            rdr.Close();

            return GetUserBlobEx(ref deviceInfo, userID, ref userBlob);
        }

        public bool GetUserBlobEx(ref BS2SimpleDeviceInfo deviceInfo, string userID, ref BS2UserBlobEx userBlob)
        {
            if (userID.Length > 0)
            {
                BS2User targetUser = Util.AllocateStructure<BS2User>();

                Array.Clear(targetUser.userID, 0, BS2Envirionment.BS2_USER_ID_SIZE);
                byte[] uid = Encoding.UTF8.GetBytes(userID);
                Array.Copy(uid, 0, targetUser.userID, 0, uid.Length);

                SQLiteCommand cmd = new SQLiteCommand("SELECT userID, formatVersion, flag, version, fingerChecksum, faceChecksum, numCards, numFingers, numFaces FROM BS2User WHERE userID = @userIDParam", connection);
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);
                SQLiteDataReader rdr = cmd.ExecuteReader();

                if (rdr.Read())
                {
                    targetUser.formatVersion = Convert.ToByte(rdr[1]);
                    targetUser.flag = Convert.ToByte(rdr[2]);
                    targetUser.version = Convert.ToUInt16(rdr[3]);
                    targetUser.fingerChecksum = Convert.ToUInt32(rdr[4]);
                    targetUser.faceChecksum = Convert.ToUInt32(rdr[5]);

                    if (Convert.ToBoolean(deviceInfo.cardSupported))
                    {
                        targetUser.numCards = Convert.ToByte(rdr[6]);
                    }
                    else
                    {
                        targetUser.numCards = 0;
                    }

                    if (Convert.ToBoolean(deviceInfo.fingerSupported))
                    {
                        targetUser.numFingers = Convert.ToByte(rdr[7]);
                    }
                    else
                    {
                        targetUser.numFingers = 0;
                    }

                    if (Convert.ToBoolean(deviceInfo.faceSupported))
                    {
                        targetUser.numFaces = Convert.ToByte(rdr[7]);
                    }
                    else
                    {
                        targetUser.numFaces = 0;
                    }

                    return GetUserBlobEx(ref deviceInfo, ref targetUser, ref userBlob);
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        public bool GetUserBlobEx(ref BS2SimpleDeviceInfo deviceInfo, ref BS2User targetUser, ref BS2UserBlobEx userBlob)
        {
            userBlob.user = targetUser;

            SQLiteCommand cmd = new SQLiteCommand("SELECT startTime, endTime, fingerAuthMode, cardAuthMode, idAuthMode, securityLevel FROM BS2UserSetting WHERE userID = @userIDParam", connection);
            cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);
            SQLiteDataReader rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                userBlob.setting.startTime = Convert.ToUInt32(rdr[0]);
                userBlob.setting.endTime = Convert.ToUInt32(rdr[1]);
                userBlob.setting.fingerAuthMode = Convert.ToByte(rdr[2]);
                userBlob.setting.cardAuthMode = Convert.ToByte(rdr[3]);
                userBlob.setting.idAuthMode = Convert.ToByte(rdr[4]);
                userBlob.setting.securityLevel = Convert.ToByte(rdr[5]);
            }
            rdr.Close();

            Array.Clear(userBlob.name, 0, BS2Envirionment.BS2_USER_NAME_LEN);
            if (Convert.ToBoolean(deviceInfo.userNameSupported))
            {
                cmd.CommandText = "SELECT userName from BS2UserName WHERE userID = @userIDParam";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);
                rdr = cmd.ExecuteReader();

                if (rdr.Read())
                {
                    byte[] userName = Encoding.UTF8.GetBytes((string)rdr[0]);
                    Array.Copy(userName, 0, userBlob.name, 0, userName.Length);
                }
                rdr.Close();
            }

            userBlob.photo.size = 0;
            Array.Clear(userBlob.photo.data, 0, BS2Envirionment.BS2_USER_PHOTO_SIZE);
            if (Convert.ToBoolean(deviceInfo.userPhotoSupported))
            {
                cmd.CommandText = "SELECT size, data from BS2UserPhoto WHERE userID = @userIDParam";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);
                rdr = cmd.ExecuteReader();

                if (rdr.Read())
                {
                    UInt32 photoSize = Convert.ToUInt32(rdr[0]);
                    byte[] photoData = (byte[])rdr[1];

                    userBlob.photo.size = photoSize;
                    if (photoSize > 0)
                    {
                        Array.Copy(photoData, 0, userBlob.photo.data, 0, photoSize);
                    }
                }
                rdr.Close();
            }

            Array.Clear(userBlob.pin, 0, BS2Envirionment.BS2_PIN_HASH_SIZE);
            if (Convert.ToBoolean(deviceInfo.pinSupported))
            {
                cmd.CommandText = "SELECT hash from BS2PinCode WHERE userID = @userIDParam";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);
                rdr = cmd.ExecuteReader();

                if (rdr.Read())
                {
                    byte[] pinCode = (byte[])rdr[0];
                    Array.Copy(pinCode, 0, userBlob.pin, 0, pinCode.Length);
                }
                rdr.Close();
            }

            if (targetUser.numCards > 0)
            {
                cmd.CommandText = "SELECT type, size, data from BS2CSNCard WHERE userID = @userIDParam";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);

                rdr = cmd.ExecuteReader();
                userBlob.cardObjs = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(BS2CSNCard)) * targetUser.numCards);
                IntPtr curCardObjs = userBlob.cardObjs;

                while (rdr.Read())
                {
                    byte cardType = Convert.ToByte(rdr[0]);
                    byte cardSize = Convert.ToByte(rdr[1]);
                    byte[] cardData = (byte[])rdr[2];

                    Marshal.WriteByte(curCardObjs, cardType);
                    curCardObjs += 1;
                    Marshal.WriteByte(curCardObjs, cardSize);
                    curCardObjs += 1;
                    Marshal.Copy(cardData, 0, curCardObjs, BS2Envirionment.BS2_CARD_DATA_SIZE);
                    curCardObjs += BS2Envirionment.BS2_CARD_DATA_SIZE;
                }
                rdr.Close();
            }
            else
            {
                userBlob.cardObjs = IntPtr.Zero;
            }

            if (targetUser.numFingers > 0)
            {
                cmd.CommandText = "SELECT fingerIndex, flag, data FROM BS2Fingerprint WHERE userID = @userIDParam";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);

                rdr = cmd.ExecuteReader();
                userBlob.fingerObjs = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(BS2Fingerprint)) * targetUser.numFingers);
                IntPtr curFingerObjs = userBlob.fingerObjs;

                while (rdr.Read())
                {
                    byte fingerIndex = Convert.ToByte(rdr[0]);
                    byte fingerFlag = Convert.ToByte(rdr[1]);
                    byte[] templateData = (byte[])rdr[2];

                    Marshal.WriteByte(curFingerObjs, fingerIndex);
                    curFingerObjs += 1;
                    Marshal.WriteByte(curFingerObjs, fingerFlag);
                    curFingerObjs += 3;
                    Marshal.Copy(templateData, 0, curFingerObjs, BS2Envirionment.BS2_TEMPLATE_PER_FINGER * BS2Envirionment.BS2_FINGER_TEMPLATE_SIZE);
                    curFingerObjs += BS2Envirionment.BS2_TEMPLATE_PER_FINGER * BS2Envirionment.BS2_FINGER_TEMPLATE_SIZE;
                }
                rdr.Close();
            }
            else
            {
                userBlob.fingerObjs = IntPtr.Zero;
            }

            if (targetUser.numFaces > 0)
            {
                cmd.CommandText = "SELECT faceIndex, numOfTemplate, flag, imageLen, imageData, templateData FROM BS2Face WHERE userID = @userIDParam";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);

                rdr = cmd.ExecuteReader();
                userBlob.faceObjs = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(BS2Face)) * targetUser.numFaces);
                IntPtr curFaceObjs = userBlob.faceObjs;

                while (rdr.Read())
                {
                    byte faceIndex = Convert.ToByte(rdr[0]);
                    byte numOfTemplate = Convert.ToByte(rdr[1]);
                    byte flag = Convert.ToByte(rdr[2]);
                    UInt16 imageLen = Convert.ToUInt16(rdr[3]);

                    byte[] imageData = (byte[])rdr[4];
                    byte[] templateData = (byte[])rdr[5];

                    Marshal.WriteByte(curFaceObjs, faceIndex);
                    curFaceObjs += 1;
                    Marshal.WriteByte(curFaceObjs, numOfTemplate);
                    curFaceObjs += 1;
                    Marshal.WriteByte(curFaceObjs, flag);
                    curFaceObjs += 2; //flag(1) + reserved(1)
                    Marshal.WriteInt16(curFaceObjs, (Int16)imageLen);
                    curFaceObjs += 4; //imageLen(2) + reserved(2)
                    Marshal.Copy(imageData, 0, curFaceObjs, BS2Envirionment.BS2_FACE_IMAGE_SIZE);
                    curFaceObjs += BS2Envirionment.BS2_FACE_IMAGE_SIZE;
                    Marshal.Copy(templateData, 0, curFaceObjs, BS2Envirionment.BS2_TEMPLATE_PER_FACE * BS2Envirionment.BS2_FACE_TEMPLATE_LENGTH);
                    curFaceObjs += BS2Envirionment.BS2_TEMPLATE_PER_FACE * BS2Envirionment.BS2_FACE_TEMPLATE_LENGTH;
                }
                rdr.Close();
            }
            else
            {
                userBlob.faceObjs = IntPtr.Zero;
            }

            userBlob.job.numJobs = 0;

            if (Convert.ToBoolean(deviceInfo.jobCodeSupported))
            {
                cmd.CommandText = "SELECT code, label from BS2Job WHERE userID = @userIDParam";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);
                rdr = cmd.ExecuteReader();

                byte numJobs = 0;
                while (rdr.Read())
                {
                    userBlob.job.jobs[numJobs].code = Convert.ToUInt32(rdr[0]);
                    Array.Clear(userBlob.job.jobs[numJobs].label, 0, BS2Envirionment.BS2_MAX_JOBLABEL_LEN);

                    byte[] label = Encoding.UTF8.GetBytes((string)rdr[1]);
                    Array.Copy(label, 0, userBlob.job.jobs[numJobs].label, 0, label.Length);

                    numJobs++;
                }
                rdr.Close();
                userBlob.job.numJobs = numJobs;
            }

            Array.Clear(userBlob.phrase, 0, BS2Envirionment.BS2_USER_PHRASE_SIZE);
            if (Convert.ToBoolean(deviceInfo.phraseCodeSupported))
            {
                cmd.CommandText = "SELECT userPhrase from BS2UserPhrase WHERE userID = @userIDParam";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);
                rdr = cmd.ExecuteReader();

                if (rdr.Read())
                {
                    byte[] phrase = Encoding.UTF8.GetBytes((string)rdr[0]);
                    Array.Copy(phrase, 0, userBlob.phrase, 0, phrase.Length);
                }
                rdr.Close();
            }

            Array.Clear(userBlob.accessGroupId, 0, BS2Envirionment.BS2_MAX_ACCESS_GROUP_PER_USER);
            int access_group_count = 0;
            cmd.CommandText = "SELECT accessGroupId from UserAccessGroup WHERE userID = @userIDParam";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@userIDParam", targetUser.userID);

            rdr = cmd.ExecuteReader();

            while (rdr.Read())
            {
                userBlob.accessGroupId[access_group_count++] = Convert.ToUInt32(rdr[0]);
                if (access_group_count >= BS2Envirionment.BS2_MAX_ACCESS_GROUP_PER_USER)
                {
                    break;
                }
            }
            rdr.Close();

            return true;
        }

        public bool GetFingerprintList(BS2FingerprintTemplateFormatEnum templateFormat, int limit, int offset, ref List<KeyValuePair<string, BS2Fingerprint>> fingerprintList)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT userID, fingerIndex, flag, data FROM BS2Fingerprint WHERE templateFormat = @templateFormatParam LIMIT @limitParam OFFSET @offsetParam", connection);
            cmd.Parameters.AddWithValue("@templateFormatParam", templateFormat);
            cmd.Parameters.AddWithValue("@limitParam", limit);
            cmd.Parameters.AddWithValue("@offsetParam", offset);

            fingerprintList.Clear();

            BS2Fingerprint fingerprint = Util.AllocateStructure<BS2Fingerprint>();
            SQLiteDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string userID = (string)rdr[0];
                fingerprint.index = Convert.ToByte(rdr[1]);
                fingerprint.flag = Convert.ToByte(rdr[2]);
                fingerprint.data = (byte[])rdr[3];

                fingerprintList.Add(new KeyValuePair<string, BS2Fingerprint>(userID, fingerprint));
            }
            rdr.Close();

            if (fingerprintList.Count > 0)
            {
                return true;
            }

            return false;
        }

        public bool AddUserBlob(ref BS2UserBlob userBlob, BS2FingerprintTemplateFormatEnum templateFormat)
        {
            SQLiteTransaction transaction = connection.BeginTransaction();

            SQLiteCommand cmd = new SQLiteCommand("INSERT INTO User (Id, formatVersion, flag, version, fingerChecksum, faceChecksum) VALUES (@userIDParam, @formatVersionParam, @flagParam, @versionParam, @fingerChecksumParam, @faceChecksumParam)", connection);
            cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
            cmd.Parameters.AddWithValue("@formatVersionParam", userBlob.user.formatVersion);
            cmd.Parameters.AddWithValue("@flagParam", userBlob.user.flag);
            cmd.Parameters.AddWithValue("@versionParam", userBlob.user.version);
            cmd.Parameters.AddWithValue("@fingerChecksumParam", userBlob.user.fingerChecksum);
            cmd.Parameters.AddWithValue("@faceChecksumParam", userBlob.user.faceChecksum);

            if (cmd.ExecuteNonQuery() < 1)
            {
                transaction.Rollback();
                return false;
            }

            cmd.CommandText = "INSERT INTO BS2UserSetting (userID, startTime, endTime, fingerAuthMode, cardAuthMode, idAuthMode, securityLevel) VALUES (@userIDParam, @startTimeParam, @endTimeParam, @fingerAuthModeParam, @cardAuthModeParam, @idAuthModeParam, @securityLevelParam)";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
            cmd.Parameters.AddWithValue("@startTimeParam", userBlob.setting.startTime);
            cmd.Parameters.AddWithValue("@endTimeParam", userBlob.setting.endTime);
            cmd.Parameters.AddWithValue("@fingerAuthModeParam", userBlob.setting.fingerAuthMode);
            cmd.Parameters.AddWithValue("@cardAuthModeParam", userBlob.setting.cardAuthMode);
            cmd.Parameters.AddWithValue("@idAuthModeParam", userBlob.setting.idAuthMode);
            cmd.Parameters.AddWithValue("@securityLevelParam", userBlob.setting.securityLevel);

            if (cmd.ExecuteNonQuery() < 1)
            {
                transaction.Rollback();
                return false;
            }

            string userName = System.Text.Encoding.UTF8.GetString(userBlob.name).TrimEnd(new char[] { '\0' });
            if (userName.Length > 0)
            {
                cmd.CommandText = "INSERT INTO BS2UserName (userID, userName) VALUES (@userIDParam, @userNameParam)";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                cmd.Parameters.AddWithValue("@userNameParam", userBlob.name);

                if (cmd.ExecuteNonQuery() < 1)
                {
                    transaction.Rollback();
                    return false;
                }
            }

            if (userBlob.photo.size > 0)
            {
                cmd.CommandText = "INSERT INTO BS2UserPhoto (userID, size, data) VALUES (@userIDParam, @sizeParam, @dataParam)";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                cmd.Parameters.AddWithValue("@sizeParam", userBlob.photo.size);
                cmd.Parameters.AddWithValue("@dataParam", userBlob.photo.data);

                if (cmd.ExecuteNonQuery() < 1)
                {
                    transaction.Rollback();
                    return false;
                }
            }

            bool isEmptyPinCode = true;
            for (int idx = 0; idx < BS2Envirionment.BS2_PIN_HASH_SIZE; ++idx)
            {
                if (userBlob.pin[idx] != 0)
                {
                    isEmptyPinCode = false;
                    break;
                }
            }

            if (!isEmptyPinCode)
            {
                cmd.CommandText = "INSERT INTO BS2PinCode (userID, hash) VALUES (@userIDParam, @hashParam)";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                cmd.Parameters.AddWithValue("@hashParam", userBlob.pin);

                if (cmd.ExecuteNonQuery() < 1)
                {
                    transaction.Rollback();
                    return false;
                }
            }

            if (userBlob.user.numCards > 0)
            {
                int structSize = Marshal.SizeOf(typeof(BS2CSNCard));
                Type type = typeof(BS2CSNCard);
                IntPtr curObjs = userBlob.cardObjs;
                cmd.CommandText = "INSERT INTO BS2CSNCard (userID, type, size, data) VALUES (@userIDParam, @typeParam, @sizeParam, @dataParam)";

                for (byte idx = 0; idx < userBlob.user.numCards; ++idx)
                {
                    BS2CSNCard csnCard = (BS2CSNCard)Marshal.PtrToStructure(curObjs, type);
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                    cmd.Parameters.AddWithValue("@typeParam", csnCard.type);
                    cmd.Parameters.AddWithValue("@sizeParam", csnCard.size);
                    cmd.Parameters.AddWithValue("@dataParam", csnCard.data);

                    if (cmd.ExecuteNonQuery() < 1)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    curObjs += structSize;
                }
            }

            if (userBlob.user.numFingers > 0)
            {
                int structSize = Marshal.SizeOf(typeof(BS2Fingerprint));
                Type type = typeof(BS2Fingerprint);
                IntPtr curObjs = userBlob.fingerObjs;
                cmd.CommandText = "INSERT INTO BS2Fingerprint (userID, fingerIndex, flag, data, templateFormat) VALUES (@userIDParam, @fingerIndexParam, @flagParam, @dataParam, @templateFormatParam)";

                for (byte idx = 0; idx < userBlob.user.numFingers; ++idx)
                {
                    BS2Fingerprint finger = (BS2Fingerprint)Marshal.PtrToStructure(curObjs, type);
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                    cmd.Parameters.AddWithValue("@fingerIndexParam", finger.index);
                    cmd.Parameters.AddWithValue("@flagParam", finger.flag);
                    cmd.Parameters.AddWithValue("@dataParam", finger.data);
                    cmd.Parameters.AddWithValue("@templateFormatParam", templateFormat);

                    if (cmd.ExecuteNonQuery() < 1)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    curObjs += structSize;
                }
            }

            if (userBlob.user.numFaces > 0)
            {
                int structSize = Marshal.SizeOf(typeof(BS2Face));
                Type type = typeof(BS2Face);
                IntPtr curObjs = userBlob.faceObjs;
                cmd.CommandText = "INSERT INTO BS2Face (userID, faceIndex, numOfTemplate, flag, imageLen, imageData, templateData) VALUES (@userIDParam, @faceIndexParam, @numOfTemplateParam, @flagParam, @imageLenParam, @imageDataParam, @templatedataParam)";

                for (byte idx = 0; idx < userBlob.user.numFaces; ++idx)
                {
                    BS2Face face = (BS2Face)Marshal.PtrToStructure(curObjs, type);
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                    cmd.Parameters.AddWithValue("@faceIndexParam", face.faceIndex);
                    cmd.Parameters.AddWithValue("@numOfTemplateParam", face.numOfTemplate);
                    cmd.Parameters.AddWithValue("@flagParam", face.flag);
                    cmd.Parameters.AddWithValue("@imageLenParam", face.imageLen);
                    cmd.Parameters.AddWithValue("@imageDataParam", face.imageData);
                    cmd.Parameters.AddWithValue("@templatedataParam", face.templateData);

                    if (cmd.ExecuteNonQuery() < 1)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    curObjs += structSize;
                }
            }

            cmd.CommandText = "INSERT INTO UserAccessGroup (userID, accessGroupId) VALUES (@userIDParam, @accessGroupIdParam)";
            for (int idx = 0; idx < BS2Envirionment.BS2_MAX_ACCESS_GROUP_PER_USER; ++idx)
            {
                if (userBlob.accessGroupId[idx] != 0)
                {
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                    cmd.Parameters.AddWithValue("@accessGroupIdParam", userBlob.accessGroupId[idx]);

                    if (cmd.ExecuteNonQuery() < 1)
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
                else
                {
                    break;
                }
            }

            transaction.Commit();
            return true;
        }

        public bool AddUserBlobEx(ref BS2UserBlobEx userBlob, BS2FingerprintTemplateFormatEnum templateFormat)
        {
            SQLiteTransaction transaction = connection.BeginTransaction();

            SQLiteCommand cmd = new SQLiteCommand("INSERT INTO User (Id, formatVersion, flag, version, fingerChecksum, faceChecksum) VALUES (@userIDParam, @formatVersionParam, @flagParam, @versionParam, @fingerChecksumParam, @faceChecksumParam)", connection);
            cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
            cmd.Parameters.AddWithValue("@formatVersionParam", userBlob.user.formatVersion);
            cmd.Parameters.AddWithValue("@flagParam", userBlob.user.flag);
            cmd.Parameters.AddWithValue("@versionParam", userBlob.user.version);
            cmd.Parameters.AddWithValue("@fingerChecksumParam", userBlob.user.fingerChecksum);
            cmd.Parameters.AddWithValue("@faceChecksumParam", userBlob.user.faceChecksum);

            if (cmd.ExecuteNonQuery() < 1)
            {
                transaction.Rollback();
                return false;
            }

            cmd.CommandText = "INSERT INTO BS2UserSetting (userID, startTime, endTime, fingerAuthMode, cardAuthMode, idAuthMode, securityLevel) VALUES (@userIDParam, @startTimeParam, @endTimeParam, @fingerAuthModeParam, @cardAuthModeParam, @idAuthModeParam, @securityLevelParam)";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
            cmd.Parameters.AddWithValue("@startTimeParam", userBlob.setting.startTime);
            cmd.Parameters.AddWithValue("@endTimeParam", userBlob.setting.endTime);
            cmd.Parameters.AddWithValue("@fingerAuthModeParam", userBlob.setting.fingerAuthMode);
            cmd.Parameters.AddWithValue("@cardAuthModeParam", userBlob.setting.cardAuthMode);
            cmd.Parameters.AddWithValue("@idAuthModeParam", userBlob.setting.idAuthMode);
            cmd.Parameters.AddWithValue("@securityLevelParam", userBlob.setting.securityLevel);

            if (cmd.ExecuteNonQuery() < 1)
            {
                transaction.Rollback();
                return false;
            }

            string userName = System.Text.Encoding.UTF8.GetString(userBlob.name).TrimEnd(new char[] { '\0' });
            if (userName.Length > 0)
            {
                cmd.CommandText = "INSERT INTO BS2UserName (userID, userName) VALUES (@userIDParam, @userNameParam)";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                cmd.Parameters.AddWithValue("@userNameParam", userBlob.name);

                if (cmd.ExecuteNonQuery() < 1)
                {
                    transaction.Rollback();
                    return false;
                }
            }

            if (userBlob.photo.size > 0)
            {
                cmd.CommandText = "INSERT INTO BS2UserPhoto (userID, size, data) VALUES (@userIDParam, @sizeParam, @dataParam)";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                cmd.Parameters.AddWithValue("@sizeParam", userBlob.photo.size);
                cmd.Parameters.AddWithValue("@dataParam", userBlob.photo.data);

                if (cmd.ExecuteNonQuery() < 1)
                {
                    transaction.Rollback();
                    return false;
                }
            }

            bool isEmptyPinCode = true;
            for (int idx = 0; idx < BS2Envirionment.BS2_PIN_HASH_SIZE; ++idx)
            {
                if (userBlob.pin[idx] != 0)
                {
                    isEmptyPinCode = false;
                    break;
                }
            }

            if (!isEmptyPinCode)
            {
                cmd.CommandText = "INSERT INTO BS2PinCode (userID, hash) VALUES (@userIDParam, @hashParam)";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                cmd.Parameters.AddWithValue("@hashParam", userBlob.pin);

                if (cmd.ExecuteNonQuery() < 1)
                {
                    transaction.Rollback();
                    return false;
                }
            }

            if (userBlob.user.numCards > 0)
            {
                int structSize = Marshal.SizeOf(typeof(BS2CSNCard));
                Type type = typeof(BS2CSNCard);
                IntPtr curObjs = userBlob.cardObjs;
                cmd.CommandText = "INSERT INTO BS2CSNCard (userID, type, size, data) VALUES (@userIDParam, @typeParam, @sizeParam, @dataParam)";

                for (byte idx = 0; idx < userBlob.user.numCards; ++idx)
                {
                    BS2CSNCard csnCard = (BS2CSNCard)Marshal.PtrToStructure(curObjs, type);
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                    cmd.Parameters.AddWithValue("@typeParam", csnCard.type);
                    cmd.Parameters.AddWithValue("@sizeParam", csnCard.size);
                    cmd.Parameters.AddWithValue("@dataParam", csnCard.data);

                    if (cmd.ExecuteNonQuery() < 1)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    curObjs += structSize;
                }
            }

            if (userBlob.user.numFingers > 0)
            {
                int structSize = Marshal.SizeOf(typeof(BS2Fingerprint));
                Type type = typeof(BS2Fingerprint);
                IntPtr curObjs = userBlob.fingerObjs;
                cmd.CommandText = "INSERT INTO BS2Fingerprint (userID, fingerIndex, flag, data, templateFormat) VALUES (@userIDParam, @fingerIndexParam, @flagParam, @dataParam, @templateFormatParam)";

                for (byte idx = 0; idx < userBlob.user.numFingers; ++idx)
                {
                    BS2Fingerprint finger = (BS2Fingerprint)Marshal.PtrToStructure(curObjs, type);
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                    cmd.Parameters.AddWithValue("@fingerIndexParam", finger.index);
                    cmd.Parameters.AddWithValue("@flagParam", finger.flag);
                    cmd.Parameters.AddWithValue("@dataParam", finger.data);
                    cmd.Parameters.AddWithValue("@templateFormatParam", templateFormat);

                    if (cmd.ExecuteNonQuery() < 1)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    curObjs += structSize;
                }
            }

            if (userBlob.user.numFaces > 0)
            {
                int structSize = Marshal.SizeOf(typeof(BS2Face));
                Type type = typeof(BS2Face);
                IntPtr curObjs = userBlob.faceObjs;
                cmd.CommandText = "INSERT INTO BS2Face (userID, faceIndex, numOfTemplate, flag, imageLen, imageData, templateData) VALUES (@userIDParam, @faceIndexParam, @numOfTemplateParam, @flagParam, @imageLenParam, @imageDataParam, @templatedataParam)";

                for (byte idx = 0; idx < userBlob.user.numFaces; ++idx)
                {
                    BS2Face face = (BS2Face)Marshal.PtrToStructure(curObjs, type);
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                    cmd.Parameters.AddWithValue("@faceIndexParam", face.faceIndex);
                    cmd.Parameters.AddWithValue("@numOfTemplateParam", face.numOfTemplate);
                    cmd.Parameters.AddWithValue("@flagParam", face.flag);
                    cmd.Parameters.AddWithValue("@imageLenParam", face.imageLen);
                    cmd.Parameters.AddWithValue("@imageDataParam", face.imageData);
                    cmd.Parameters.AddWithValue("@templatedataParam", face.templateData);

                    if (cmd.ExecuteNonQuery() < 1)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    curObjs += structSize;
                }
            }

            if (userBlob.job.numJobs > 0)
            {
                cmd.CommandText = "INSERT INTO BS2Job (userID, code, label) VALUES (@userIDParam, @codeParam, @labelParam)";
                for (int idx = 0; idx < userBlob.job.numJobs; ++idx)
                {
                    if (userBlob.job.jobs[idx].code != 0)
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                        cmd.Parameters.AddWithValue("@codeParam", userBlob.job.jobs[idx].code);
                        cmd.Parameters.AddWithValue("@labelParam", userBlob.job.jobs[idx].label);

                        if (cmd.ExecuteNonQuery() < 1)
                        {
                            transaction.Rollback();
                            return false;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            string userPhrase = System.Text.Encoding.UTF8.GetString(userBlob.phrase).TrimEnd(new char[] { '\0' });
            if (userPhrase.Length > 0)
            {
                cmd.CommandText = "INSERT INTO BS2UserPhrase (userID, userPhrase) VALUES (@userIDParam, @userPhraseParam)";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                cmd.Parameters.AddWithValue("@userPhraseParam", userBlob.phrase);

                if (cmd.ExecuteNonQuery() < 1)
                {
                    transaction.Rollback();
                    return false;
                }
            }


            cmd.CommandText = "INSERT INTO UserAccessGroup (userID, accessGroupId) VALUES (@userIDParam, @accessGroupIdParam)";
            for (int idx = 0; idx < BS2Envirionment.BS2_MAX_ACCESS_GROUP_PER_USER; ++idx)
            {
                if (userBlob.accessGroupId[idx] != 0)
                {
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@userIDParam", userBlob.user.userID);
                    cmd.Parameters.AddWithValue("@accessGroupIdParam", userBlob.accessGroupId[idx]);

                    if (cmd.ExecuteNonQuery() < 1)
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
                else
                {
                    break;
                }
            }

            transaction.Commit();
            return true;
        }
    }

    public class UserControl : FunctionModule
    {
        private const int USER_PAGE_SIZE = 1024;

        private API.OnReadyToScan cbCardOnReadyToScan = null;
        private API.OnReadyToScan cbFingerOnReadyToScan = null;
        private API.OnReadyToScan cbFaceOnReadyToScan = null;

        private DataBaseHandler dbHandler = new DataBaseHandler();
        private IntPtr sdkContext;

        protected override List<KeyValuePair<string, Action<IntPtr, UInt32, bool>>> getFunctionList(IntPtr sdkContext, UInt32 deviceID, bool isMasterDevice)
        {
            List<KeyValuePair<string, Action<IntPtr, UInt32, bool>>> functionList = new List<KeyValuePair<string, Action<IntPtr, uint, bool>>>();

            if (!isMasterDevice)
            {
                Console.WriteLine("Not supported in slave device.");
                return functionList;
            }

            functionList.Add(new KeyValuePair<string, Action<IntPtr, uint, bool>>("List a user from database", listUserFromDatabase));
            functionList.Add(new KeyValuePair<string, Action<IntPtr, uint, bool>>("Insert a user into database", insertUserIntoDatabase));
            functionList.Add(new KeyValuePair<string, Action<IntPtr, uint, bool>>("Delete a user from database", deleteUserFromDatabase));
            functionList.Add(new KeyValuePair<string, Action<IntPtr, uint, bool>>("List a user from device", listUserFromDevice));
            functionList.Add(new KeyValuePair<string, Action<IntPtr, uint, bool>>("Insert a user into device", insertUserIntoDevice));
            functionList.Add(new KeyValuePair<string, Action<IntPtr, uint, bool>>("Delete a user from device", deleteUserFromDevice));

            functionList.Add(new KeyValuePair<string, Action<IntPtr, uint, bool>>("Insert a userEx into database", insertUserIntoDatabaseEx));
            functionList.Add(new KeyValuePair<string, Action<IntPtr, uint, bool>>("Insert a userEx into device", insertUserIntoDeviceEx));

            functionList.Add(new KeyValuePair<string, Action<IntPtr, uint, bool>>("Get supported User Mask", getUserMask));

            return functionList;
        }
        

        public void getUserMask(IntPtr sdkContext, UInt32 deviceID, bool isMasterDevice)
        {
            BS2_USER_MASK userMask = 0;

            Console.WriteLine("Trying to get supported user mask");
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetSupportedUserMask(sdkContext, deviceID, out userMask);
            if (result == BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("Supported User Mask: 0x{0:X}", userMask);
            }
            else
            {
                Console.WriteLine("Got error({0}).", result);
                return;
            }
        }

        public void listUserFromDatabase(IntPtr sdkContext, UInt32 deviceID, bool isMasterDevice)
        {
            List<BS2User> userList = new List<BS2User>();
            if (dbHandler.GetUserList(ref deviceInfo, ref userList))
            {
                if (userList.Count > 0)
                {
                    foreach (BS2User user in userList)
                    {
                        print(sdkContext, user);
                    }
                }
                else
                {
                    Console.WriteLine("There is no user.");
                }
            }
            else
            {
                Console.WriteLine("An error occurred while attempting to retrieve user list.");
            }
        }

        public void insertUserIntoDatabase(IntPtr sdkContext, UInt32 deviceID, bool isMasterDevice)
        {
            BS2ErrorCode result = BS2ErrorCode.BS_SDK_SUCCESS;
            BS2FingerprintTemplateFormatEnum templateFormat = BS2FingerprintTemplateFormatEnum.FORMAT_SUPREMA;
            SortedSet<BS2CardAuthModeEnum> privateCardAuthMode = new SortedSet<BS2CardAuthModeEnum>();
            SortedSet<BS2FingerAuthModeEnum> privateFingerAuthMode = new SortedSet<BS2FingerAuthModeEnum>();
            SortedSet<BS2IDAuthModeEnum> privateIDAuthMode = new SortedSet<BS2IDAuthModeEnum>();
            SortedSet<BS2FaceAuthModeEnum> privateFaceAuthMode = new SortedSet<BS2FaceAuthModeEnum>();

            bool cardSupported = Convert.ToBoolean(deviceInfo.cardSupported);
            bool fingerSupported = Convert.ToBoolean(deviceInfo.fingerSupported);
            bool pinSupported = Convert.ToBoolean(deviceInfo.pinSupported);
            bool faceSupported = Convert.ToBoolean(deviceInfo.faceSupported);            

            privateIDAuthMode.Add(BS2IDAuthModeEnum.PROHIBITED);

            if (cardSupported)
            {
                privateCardAuthMode.Add(BS2CardAuthModeEnum.PROHIBITED);
                privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_ONLY);                

                if (pinSupported)
                {
                    privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_PIN);

                    privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_PIN);

                    if (fingerSupported)
                    {
                        privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_BIOMETRIC_OR_PIN);
                        privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_BIOMETRIC_PIN);

                        privateFingerAuthMode.Add(BS2FingerAuthModeEnum.BIOMETRIC_PIN);

                        privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_OR_PIN);
                        privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_PIN);
                    }

                    if (faceSupported)
                    {
                        privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_BIOMETRIC_OR_PIN);
                        privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_BIOMETRIC_PIN);

                        privateFaceAuthMode.Add(BS2FaceAuthModeEnum.BIOMETRIC_PIN);

                        privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_OR_PIN);
                        privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_PIN);
                    }
                }

                if (fingerSupported)
                {
                    privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_BIOMETRIC);

                    privateFingerAuthMode.Add(BS2FingerAuthModeEnum.BIOMETRIC_ONLY);

                    privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC);
                }

                if (faceSupported)
                {
                    privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_BIOMETRIC);

                    privateFaceAuthMode.Add(BS2FaceAuthModeEnum.BIOMETRIC_ONLY);

                    privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC);
                }
            }
            else if (fingerSupported)
            {
                if (pinSupported)
                {
                    privateFingerAuthMode.Add(BS2FingerAuthModeEnum.BIOMETRIC_PIN);

                    privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_OR_PIN);
                    privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_PIN);
                }

                privateFingerAuthMode.Add(BS2FingerAuthModeEnum.BIOMETRIC_ONLY);

                privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC);
            }
            else if (pinSupported)
            {
                privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_PIN);
            }
            else if (faceSupported)
            {
                if (pinSupported)
                {
                    privateFaceAuthMode.Add(BS2FaceAuthModeEnum.BIOMETRIC_PIN);

                    privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_OR_PIN);
                    privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_PIN);
                }

                privateFaceAuthMode.Add(BS2FaceAuthModeEnum.BIOMETRIC_ONLY);
                privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC);
            }

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

            Console.WriteLine("Enter the ID for the User which you want to enroll");
            Console.Write(">>>> ");
            string userID = Console.ReadLine();
            if (userID.Length == 0)
            {
                Console.WriteLine("The user id can not be empty.");
                return;
            }
            else if (userID.Length > BS2Envirionment.BS2_USER_ID_SIZE)
            {
                Console.WriteLine("The user id should less than {0} words.", BS2Envirionment.BS2_USER_ID_SIZE);
                return;
            }
            else
            {
                //TODO Alphabet user id is not implemented yet.
                UInt32 uid;
                if (!UInt32.TryParse(userID, out uid))
                {
                    Console.WriteLine("The user id should be a numeric.");
                    return;
                }

                byte[] userIDArray = Encoding.UTF8.GetBytes(userID);
                Array.Clear(userBlob.user.userID, 0, BS2Envirionment.BS2_USER_ID_SIZE);
                Array.Copy(userIDArray, userBlob.user.userID, userIDArray.Length);
            }

            Console.WriteLine("When is this user valid from? [default(Today), yyyy-MM-dd HH:mm:ss]");
            Console.Write(">>>> ");
            if (!Util.GetTimestamp("yyyy-MM-dd HH:mm:ss", 0, out userBlob.setting.startTime))
            {
                return;
            }

            Console.WriteLine("When is this user valid to? [default(Today), yyyy-MM-dd HH:mm:ss]");
            Console.Write(">>>> ");
            if (!Util.GetTimestamp("yyyy-MM-dd HH:mm:ss", 0, out userBlob.setting.endTime))
            {
                return;
            }

            if (cardSupported)
            {
                Console.WriteLine("Do you want to set the private card auth mode for this user? [Y/n]");
                Console.Write(">>>> ");
                if (Util.IsYes())
                {
                    Console.Write("Enter the private card auth mode. [");
                    foreach (BS2CardAuthModeEnum cardAuthModeEnum in privateCardAuthMode)
                    {
                        if (cardAuthModeEnum == BS2CardAuthModeEnum.CARD_ONLY)
                        {
                            Console.Write("{0}: {1}(default) ", (byte)cardAuthModeEnum, cardAuthModeEnum);
                        }
                        else
                        {
                            Console.Write("{0}: {1} ", (byte)cardAuthModeEnum, cardAuthModeEnum);
                        }
                    }
                    Console.WriteLine("]");
                    Console.Write(">>>> ");
                    userBlob.setting.cardAuthMode = Util.GetInput((byte)BS2CardAuthModeEnum.CARD_ONLY);
                }
                else
                {
                    userBlob.setting.cardAuthMode = (byte)BS2CardAuthModeEnum.NONE;
                }
            }

            if (fingerSupported)
            {
                Console.WriteLine("Enter the security level for this user: [{0}: {1}, {2}: {3}, {4}: {5}(default), {6}: {7}, {8}: {9}]",
                                (byte)BS2UserSecurityLevelEnum.LOWER,
                                BS2UserSecurityLevelEnum.LOWER,
                                (byte)BS2UserSecurityLevelEnum.LOW,
                                BS2UserSecurityLevelEnum.LOW,
                                (byte)BS2UserSecurityLevelEnum.NORMAL,
                                BS2UserSecurityLevelEnum.NORMAL,
                                (byte)BS2UserSecurityLevelEnum.HIGH,
                                BS2UserSecurityLevelEnum.HIGH,
                                (byte)BS2UserSecurityLevelEnum.HIGHER,
                                BS2UserSecurityLevelEnum.HIGHER);
                Console.Write(">>>> ");
                userBlob.setting.securityLevel = Util.GetInput((byte)BS2UserSecurityLevelEnum.NORMAL);

                Console.WriteLine("Do you want to set the private biometric auth mode for this user? [Y/n]");
                Console.Write(">>>> ");
                if (Util.IsYes())
                {
                    Console.Write("Enter the private biometric auth mode. [");
                    foreach (BS2FingerAuthModeEnum fingerAuthModeEnum in privateFingerAuthMode)
                    {
                        if (fingerAuthModeEnum == BS2FingerAuthModeEnum.BIOMETRIC_ONLY)
                        {
                            Console.Write("{0}: {1}(default) ", (byte)fingerAuthModeEnum, fingerAuthModeEnum);
                        }
                        else
                        {
                            Console.Write("{0}: {1} ", (byte)fingerAuthModeEnum, fingerAuthModeEnum);
                        }
                    }
                    Console.WriteLine("]");
                    Console.Write(">>>> ");
                    userBlob.setting.fingerAuthMode = Util.GetInput((byte)BS2FingerAuthModeEnum.BIOMETRIC_ONLY);
                }
                else
                {
                    userBlob.setting.fingerAuthMode = (byte)BS2FingerAuthModeEnum.NONE;
                }
            }

            if (faceSupported)
            {
                Console.WriteLine("Enter the security level for this user: [{0}: {1}, {2}: {3}, {4}: {5}, {6}: {7}, {8}: {9}]",
                                (byte)BS2UserSecurityLevelEnum.LOWER,
                                BS2UserSecurityLevelEnum.LOWER,
                                (byte)BS2UserSecurityLevelEnum.LOW,
                                BS2UserSecurityLevelEnum.LOW,
                                (byte)BS2UserSecurityLevelEnum.NORMAL,
                                BS2UserSecurityLevelEnum.NORMAL,
                                (byte)BS2UserSecurityLevelEnum.HIGH,
                                BS2UserSecurityLevelEnum.HIGH,
                                (byte)BS2UserSecurityLevelEnum.HIGHER,
                                BS2UserSecurityLevelEnum.HIGHER);
                Console.Write(">>>> ");
                userBlob.setting.securityLevel = Util.GetInput((byte)BS2UserSecurityLevelEnum.NORMAL);

                Console.WriteLine("Do you want to set the private biometric auth mode for this user? [Y/n]");
                Console.Write(">>>> ");
                if (Util.IsYes())
                {
                    Console.Write("Enter the private biometric auth mode. [");
                    foreach (BS2FaceAuthModeEnum faceAuthModeEnum in privateFaceAuthMode)
                    {
                        if (faceAuthModeEnum == BS2FaceAuthModeEnum.BIOMETRIC_ONLY)
                        {
                            Console.Write("{0}: {1}(default) ", (byte)faceAuthModeEnum, faceAuthModeEnum);
                        }
                        else
                        {
                            Console.Write("{0}: {1} ", (byte)faceAuthModeEnum, faceAuthModeEnum);
                        }
                    }
                    Console.WriteLine("]");
                    Console.Write(">>>> ");
                    userBlob.setting.fingerAuthMode = Util.GetInput((byte)BS2FaceAuthModeEnum.BIOMETRIC_ONLY);
                }
                else
                {
                    userBlob.setting.fingerAuthMode = (byte)BS2FaceAuthModeEnum.NONE;
                }
            }

            Console.WriteLine("Do you want to set the private id auth mode for this user? [Y/n]");
            Console.Write(">>>> ");
            if (Util.IsYes())
            {
                Console.Write("Enter the private id auth mode. [");
                foreach (BS2IDAuthModeEnum idAuthModeEnum in privateIDAuthMode)
                {
                    if (idAuthModeEnum == BS2IDAuthModeEnum.PROHIBITED)
                    {
                        Console.Write("{0}: {1}(default) ", (byte)idAuthModeEnum, idAuthModeEnum);
                    }
                    else
                    {
                        Console.Write("{0}: {1} ", (byte)idAuthModeEnum, idAuthModeEnum);
                    }
                }
                Console.WriteLine("]");
                Console.Write(">>>> ");
                userBlob.setting.idAuthMode = Util.GetInput((byte)BS2IDAuthModeEnum.PROHIBITED);
            }
            else
            {
                userBlob.setting.idAuthMode = (byte)BS2IDAuthModeEnum.NONE;
            }

            Array.Clear(userBlob.name, 0, BS2Envirionment.BS2_USER_NAME_LEN);
            Console.WriteLine("Do you want to set user name? [Y/n]");
            Console.Write(">>>> ");
            if (Util.IsYes())
            {
                Console.WriteLine("Enter the name for this user");
                Console.Write(">>>> ");
                string userName = Console.ReadLine();
                if (userName.Length == 0)
                {
                    Console.WriteLine("[Warning] user name will be displayed as empty.");
                }
                else if (userName.Length > BS2Envirionment.BS2_USER_NAME_LEN)
                {
                    Console.WriteLine("The user name should less than {0} words.", BS2Envirionment.BS2_USER_NAME_LEN);
                    return;
                }
                else
                {
                    byte[] userNameArray = Encoding.UTF8.GetBytes(userName);
                    Array.Copy(userNameArray, userBlob.name, userNameArray.Length);
                }
            }

            userBlob.photo.size = 0;
            Array.Clear(userBlob.photo.data, 0, BS2Envirionment.BS2_USER_PHOTO_SIZE);
            Console.WriteLine("Do you want to set profile image? [Y/n]");
            Console.Write(">>>> ");
            if (Util.IsYes())
            {
                Console.WriteLine("Enter the jpg file path for this user.");
                Console.Write(">>>> ");
                string imagePath = Console.ReadLine();

                if (!File.Exists(imagePath))
                {
                    Console.WriteLine("Invalid file path");
                    return;
                }

                Image profileImage = Image.FromFile(imagePath);
                if (!profileImage.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Jpeg))
                {
                    Console.WriteLine("Invalid image file format");
                    return;
                }

                IntPtr imageData = IntPtr.Zero;
                UInt32 imageDataLen = 0;

                if(Util.LoadBinary(imagePath, out imageData, out imageDataLen))
                {
                    if(imageDataLen == 0)
                    {
                        Console.WriteLine("Empty image file");
                        return;
                    }
                    else if (imageDataLen > BS2Envirionment.BS2_USER_PHOTO_SIZE)
                    {
                        Console.WriteLine("The profile image should less than {0} bytes.", BS2Envirionment.BS2_USER_PHOTO_SIZE);
                        return;
                    }

                    userBlob.photo.size = imageDataLen;
                    Marshal.Copy(imageData, userBlob.photo.data, 0, (int)imageDataLen);
                    Marshal.FreeHGlobal(imageData);
                }
            }

            Array.Clear(userBlob.pin, 0, BS2Envirionment.BS2_PIN_HASH_SIZE);
            Console.WriteLine("Do you want to set pin code? [Y/n]");
            Console.Write(">>>> ");
            if (Util.IsYes())
            {
                Console.WriteLine("Enter pin code for this user");
                Console.Write(">>>> ");
                string pin = Console.ReadLine();
                if (pin.Length == 0)
                {
                    Console.WriteLine("Pin code can not be empty.");
                    return;
                }
                else if (pin.Length > BS2Envirionment.BS2_PIN_HASH_SIZE)
                {
                    Console.WriteLine("Pin code should less than {0} words.", BS2Envirionment.BS2_PIN_HASH_SIZE);
                    return;
                }
                else
                {
                    IntPtr pinCode = Marshal.AllocHGlobal(BS2Envirionment.BS2_PIN_HASH_SIZE);
                    result = (BS2ErrorCode)API.BS2_MakePinCode(sdkContext, pin, pinCode);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("Can't generate pin code.");
                        return;
                    }

                    Marshal.Copy(pinCode, userBlob.pin, 0, BS2Envirionment.BS2_PIN_HASH_SIZE);
                    Marshal.FreeHGlobal(pinCode);
                }
            }

            if (cardSupported)
            {   
                Console.WriteLine("How many cards do you want to register? [1(default) - {0}]", BS2Envirionment.BS2_MAX_NUM_OF_CARD_PER_USER);
                Console.Write(">>>> ");
                userBlob.user.numCards = Util.GetInput((byte)1);

                if (userBlob.user.numCards > 0)
                {
                    int structSize = Marshal.SizeOf(typeof(BS2CSNCard));
                    BS2Card card = Util.AllocateStructure<BS2Card>();
                    userBlob.cardObjs = Marshal.AllocHGlobal(structSize * userBlob.user.numCards);
                    IntPtr curCardObjs = userBlob.cardObjs;
                    cbCardOnReadyToScan = new API.OnReadyToScan(ReadyToScanForCard);

                    for (byte idx = 0; idx < userBlob.user.numCards; )
                    {
                        Console.WriteLine("Trying to scan card.");
                        result = (BS2ErrorCode)API.BS2_ScanCard(sdkContext, deviceID, out card, cbCardOnReadyToScan);
                        if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                        {
                            Console.WriteLine("Got error({0}).", result);
                            return;
                        }
                        else if (Convert.ToBoolean(card.isSmartCard))
                        {
                            Console.WriteLine("CSN card is only available. Try again");
                        }
                        else
                        {
                            Marshal.Copy(card.cardUnion, 0, curCardObjs, structSize);
                            curCardObjs += structSize;
                            ++idx;
                        }
                    }

                    cbCardOnReadyToScan = null;
                }
            }

            if (fingerSupported)
            {
                Console.WriteLine("How many fingerprints do you want to register? [1(default) - {0}]", BS2Envirionment.BS2_MAX_NUM_OF_FINGER_PER_USER);
                Console.Write(">>>> ");
                userBlob.user.numFingers = Util.GetInput((byte)1);

                if (userBlob.user.numFingers > 0)
                {
                    BS2FingerprintConfig fingerprintConfig;
                    Console.WriteLine("Trying to get fingerprint config");
                    result = (BS2ErrorCode)API.BS2_GetFingerprintConfig(sdkContext, deviceID, out fingerprintConfig);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("Got error({0}).", result);
                        return;
                    }
                    else
                    {
                        templateFormat = (BS2FingerprintTemplateFormatEnum)fingerprintConfig.templateFormat;
                    }

                    int structSize = Marshal.SizeOf(typeof(BS2Fingerprint));
                    BS2Fingerprint fingerprint = Util.AllocateStructure<BS2Fingerprint>();
                    userBlob.fingerObjs = Marshal.AllocHGlobal(structSize * userBlob.user.numFingers);
                    IntPtr curFingerObjs = userBlob.fingerObjs;
                    cbFingerOnReadyToScan = new API.OnReadyToScan(ReadyToScanForFinger);

                    UInt32 outquality;
                    for (int idx = 0; idx < userBlob.user.numFingers; ++idx)
                    {
                        Console.WriteLine("Trying to get fingerprint[{0}]", idx);
                        for (UInt32 templateIndex = 0; templateIndex < BS2Envirionment.BS2_TEMPLATE_PER_FINGER; )
                        {
                            Console.WriteLine("Trying to scan finger.");
                            result = (BS2ErrorCode)API.BS2_ScanFingerprintEx(sdkContext, deviceID, ref fingerprint, templateIndex, (UInt32)BS2FingerprintQualityEnum.QUALITY_STANDARD, (byte)templateFormat, out outquality, cbFingerOnReadyToScan);
                            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                            {
                                if (result == BS2ErrorCode.BS_SDK_ERROR_EXTRACTION_LOW_QUALITY ||
                                    result == BS2ErrorCode.BS_SDK_ERROR_CAPTURE_LOW_QUALITY)
                                {
                                    Console.WriteLine("Bad fingerprint quality. Try again");
                                }
                                else
                                {
                                    Console.WriteLine("Got error({0}).", result);
                                    return;
                                }
                            }
                            else
                            {
                                ++templateIndex;
                            }
                        }

                        Console.WriteLine("Verify the fingerprints.");
                        result = (BS2ErrorCode)API.BS2_VerifyFingerprint(sdkContext, deviceID, ref fingerprint);
                        if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                        {
                            if (result == BS2ErrorCode.BS_SDK_ERROR_NOT_SAME_FINGERPRINT)
                            {
                                Console.WriteLine("The fingerprint does not match. Try again");
                                --idx;
                                continue;
                            }
                            else
                            {
                                Console.WriteLine("Got error({0}).", result);
                                return;
                            }
                        }

                        fingerprint.index = (byte)idx;
                        Console.WriteLine("Is it duress finger? [0 : Normal(default), 1 : Duress]");
                        Console.Write(">>>> ");
                        fingerprint.flag = Util.GetInput((byte)BS2FingerprintFlagEnum.NORMAL);

                        Marshal.StructureToPtr(fingerprint, curFingerObjs, false);
                        curFingerObjs += structSize;
                    }

                    cbFingerOnReadyToScan = null;
                }
            }

            if (faceSupported)
            {
                Console.WriteLine("How many faces do you want to register? [1(default) - {0}]", BS2Envirionment.BS2_MAX_NUM_OF_FACE_PER_USER);
                Console.Write(">>>> ");
                userBlob.user.numFaces = Util.GetInput((byte)1);

                if (userBlob.user.numFaces > 0)
                {
                    byte enrollThreshold;
                    BS2FaceConfig faceConfig;
                    Console.WriteLine("Trying to get face config");
                    result = (BS2ErrorCode)API.BS2_GetFaceConfig(sdkContext, deviceID, out faceConfig);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("Got error({0}).", result);
                        return;
                    }
                    else
                    {
                        enrollThreshold = faceConfig.enrollThreshold;
                    }                 

                    int structSize = Marshal.SizeOf(typeof(BS2Face));
                    BS2Face[] face = Util.AllocateStructureArray<BS2Face>(1);
                                                            
                    userBlob.faceObjs = Marshal.AllocHGlobal(structSize * userBlob.user.numFaces);
                    IntPtr curFaceObjs = userBlob.faceObjs;
                    cbFaceOnReadyToScan = new API.OnReadyToScan(ReadyToScanForFace);

                    for (int idx = 0; idx < userBlob.user.numFaces; ++idx)
                    {
                        Console.WriteLine("Trying to scan face[{0}]", idx);
                        result = (BS2ErrorCode)API.BS2_ScanFace(sdkContext, deviceID, face, enrollThreshold, cbFaceOnReadyToScan);
                        if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                        {                            
                            Console.WriteLine("Got error({0}).", result);
                            return;                            
                        }                        

                        face[0].faceIndex = (byte)idx;
                        Marshal.StructureToPtr(face[0], curFaceObjs, false);
                        curFaceObjs += structSize;
                    }

                    cbFaceOnReadyToScan = null;
                }
            }           

#if false //TODO TBD 
            if (Convert.ToBoolean(deviceInfo.faceSupported))
            {
            }
#endif
            Array.Clear(userBlob.accessGroupId, 0, BS2Envirionment.BS2_MAX_ACCESS_GROUP_PER_USER);

            Console.WriteLine("Which access groups does this user belongs to? [ex. ID_1 ID_2 ...]");
            Console.Write(">>>> ");
            int accessGroupIdIndex = 0;
            char[] delimiterChars = { ' ', ',', '.', ':', '\t' };
            string[] accessGroupIDs = Console.ReadLine().Split(delimiterChars);

            foreach (string accessGroupID in accessGroupIDs)
            {
                if (accessGroupID.Length > 0)
                {
                    UInt32 item;
                    if (UInt32.TryParse(accessGroupID, out item))
                    {
                        userBlob.accessGroupId[accessGroupIdIndex++] = item;
                    }
                }
            }

            Console.WriteLine("Trying to enroll user.");
            if (!dbHandler.AddUserBlob(ref userBlob, templateFormat))
            {
                Console.WriteLine("Can not enroll user in the system.");
            }

            //Release memory
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
        }

        public void deleteUserFromDatabase(IntPtr sdkContext, UInt32 deviceID, bool isMasterDevice)
        {
            Console.WriteLine("Enter the ID for the User which you want to remove");
            Console.Write(">>>> ");
            string userID = Console.ReadLine();
            if (userID.Length == 0)
            {
                Console.WriteLine("The user id can not be empty.");
                return;
            }
            else if (userID.Length > BS2Envirionment.BS2_USER_ID_SIZE)
            {
                Console.WriteLine("The user id should less than {0} words.", BS2Envirionment.BS2_USER_ID_SIZE);
                return;
            }
            else
            {
                Console.WriteLine("Trying to remove a user.");
                if (!dbHandler.RemoveUser(userID))
                {
                    Console.WriteLine("Can not remove user from the system.");
                }
            }
        }

        public void listUserFromDevice(IntPtr sdkContext, UInt32 deviceID, bool isMasterDevice)
        {
            IntPtr outUidObjs = IntPtr.Zero;
            UInt32 numUserIds = 0;
            API.IsAcceptableUserID cbIsAcceptableUserID = null; // we don't need to user id filtering

            Console.WriteLine("Trying to get user list.");
            BS2ErrorCode result = (BS2ErrorCode)API.BS2_GetUserList(sdkContext, deviceID, out outUidObjs, out numUserIds, cbIsAcceptableUserID);
            if (result == BS2ErrorCode.BS_SDK_SUCCESS)
            {
                if (numUserIds > 0)
                {
                    IntPtr curUidObjs = outUidObjs;
                    BS2UserBlob[] userBlobs = new BS2UserBlob[USER_PAGE_SIZE];

                    Console.WriteLine("Number of users : ({0}).", numUserIds);
                    for (UInt32 idx = 0; idx < numUserIds; )
                    {
                        UInt32 available = numUserIds - idx;
                        if(available > USER_PAGE_SIZE)
                        {
                            available = USER_PAGE_SIZE;
                        }

                        result = (BS2ErrorCode)API.BS2_GetUserDatas(sdkContext, deviceID, curUidObjs, available, userBlobs, (UInt32)BS2UserMaskEnum.ALL);
                        if (result == BS2ErrorCode.BS_SDK_SUCCESS)
                        {
                            for (UInt32 loop = 0; loop < available; ++loop)
                            {
                                print(sdkContext, userBlobs[loop].user);
                                // don't need to release cardObj, FingerObj, FaceObj because we get only BS2User
                                if (userBlobs[loop].cardObjs != IntPtr.Zero)
                                    API.BS2_ReleaseObject(userBlobs[loop].cardObjs);
                                if (userBlobs[loop].fingerObjs != IntPtr.Zero)
                                    API.BS2_ReleaseObject(userBlobs[loop].fingerObjs);
                                if (userBlobs[loop].faceObjs != IntPtr.Zero)
                                    API.BS2_ReleaseObject(userBlobs[loop].faceObjs);
                            }

                            idx += available;
                            curUidObjs += (int)available*BS2Envirionment.BS2_USER_ID_SIZE;
                        }
                        else
                        {
                            Console.WriteLine("Got error({0}).", result);
                            break;
                        }
                    }

                    API.BS2_ReleaseObject(outUidObjs);
                }
            }
            else
            {
                Console.WriteLine("Got error({0}).", result);
                return;
            }
        }


        public void insertUserIntoDevice(IntPtr sdkContext, UInt32 deviceID, bool isMasterDevice)
        {
            List<BS2User> userList = new List<BS2User>();
            if (dbHandler.GetUserList(ref deviceInfo, ref userList))
            {
                if (userList.Count > 0)
                {
                    Console.WriteLine("+----------------------------------------------------------------------------------------------------------+");
                    for (int idx = 0 ; idx < userList.Count ; ++idx)
                    {
                        Console.Write("[{0:000}] ==> ", idx);
                        print(sdkContext, userList[idx]);
                    }
                    Console.WriteLine("+----------------------------------------------------------------------------------------------------------+");
                    Console.WriteLine("Please, choose the index of the user which you want to enroll.");
                    Console.Write(">>>> ");

                    Int32 selection = Util.GetInput();
                    if (selection >= 0)
                    {
                        if (selection < userList.Count)
                        {
                            BS2User user = userList[selection];
                            BS2UserBlob[] userBlob = Util.AllocateStructureArray<BS2UserBlob>(1);
                            if (dbHandler.GetUserBlob(ref deviceInfo, ref user, ref userBlob[0]))
                            {
                                Console.WriteLine("Trying to enroll user.");
                                BS2ErrorCode result = (BS2ErrorCode)API.BS2_EnrolUser(sdkContext, deviceID, userBlob, 1, 1);
                                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                                {
                                    Console.WriteLine("Got error({0}).", result);
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
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid selection[{0}]", selection);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid user index");
                    }
                }
                else
                {
                    Console.WriteLine("There is no user.");
                }
            }
            else
            {
                Console.WriteLine("An error occurred while attempting to retrieve user list.");
            }
        }

        public void deleteUserFromDevice(IntPtr sdkContext, UInt32 deviceID, bool isMasterDevice)
        {
            BS2ErrorCode result = BS2ErrorCode.BS_SDK_SUCCESS;

            Console.WriteLine("Do you want to remove all users? [Y/n]");
            Console.Write(">>>> ");
            if (Util.IsYes())
            {
                Console.WriteLine("Trying to remove all user from device.");
                result = (BS2ErrorCode)API.BS2_RemoveAllUser(sdkContext, deviceID);
            }
            else
            {
                Console.WriteLine("Enter the ID for the User which you want to remove");
                Console.Write(">>>> ");
                string userID = Console.ReadLine();
                if (userID.Length == 0)
                {
                    Console.WriteLine("The user id can not be empty.");
                    return;
                }
                else if (userID.Length > BS2Envirionment.BS2_USER_ID_SIZE)
                {
                    Console.WriteLine("The user id should less than {0} words.", BS2Envirionment.BS2_USER_ID_SIZE);
                    return;
                }
                else
                {
                    byte[] uidArray = new byte[BS2Envirionment.BS2_USER_ID_SIZE];
                    byte[] rawUid = Encoding.UTF8.GetBytes(userID);
                    IntPtr uids = Marshal.AllocHGlobal(BS2Envirionment.BS2_USER_ID_SIZE);

                    Array.Clear(uidArray, 0, BS2Envirionment.BS2_USER_ID_SIZE);
                    Array.Copy(rawUid, 0, uidArray, 0, rawUid.Length);
                    Marshal.Copy(uidArray, 0, uids, BS2Envirionment.BS2_USER_ID_SIZE);

                    Console.WriteLine("Trying to remove a user.");
                    result = (BS2ErrorCode)API.BS2_RemoveUser(sdkContext, deviceID, uids, 1);

                    Marshal.FreeHGlobal(uids);
                }
            }

            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
            {
                Console.WriteLine("Got error({0}).", result);
            }
        }
        
        void ReadyToScanForCard(UInt32 deviceID, UInt32 sequence)
        {
            Console.WriteLine("Place your card on the device.");
        }

        void ReadyToScanForFinger(UInt32 deviceID, UInt32 sequence)
        {
            Console.WriteLine("Place your finger on the device.");
        }

        void ReadyToScanForFace(UInt32 deviceID, UInt32 sequence)
        {
            Console.WriteLine("Place your face on the device.");
        }

        void print(IntPtr sdkContext, BS2User user)
        {
            Console.WriteLine(">>>> User id[{0}] numCards[{1}] numFingers[{2}] numFaces[{3}]", 
                                Encoding.UTF8.GetString(user.userID).TrimEnd('\0'), 
                                user.numCards,
                                user.numFingers,
                                user.numFaces);
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //Ex
        public void insertUserIntoDatabaseEx(IntPtr sdkContext, UInt32 deviceID, bool isMasterDevice)
        {
            BS2ErrorCode result = BS2ErrorCode.BS_SDK_SUCCESS;
            BS2FingerprintTemplateFormatEnum templateFormat = BS2FingerprintTemplateFormatEnum.FORMAT_SUPREMA;
            SortedSet<BS2CardAuthModeEnum> privateCardAuthMode = new SortedSet<BS2CardAuthModeEnum>();
            SortedSet<BS2FingerAuthModeEnum> privateFingerAuthMode = new SortedSet<BS2FingerAuthModeEnum>();
            SortedSet<BS2IDAuthModeEnum> privateIDAuthMode = new SortedSet<BS2IDAuthModeEnum>();
            SortedSet<BS2FaceAuthModeEnum> privateFaceAuthMode = new SortedSet<BS2FaceAuthModeEnum>();

            bool cardSupported = Convert.ToBoolean(deviceInfo.cardSupported);
            bool fingerSupported = Convert.ToBoolean(deviceInfo.fingerSupported);
            bool pinSupported = Convert.ToBoolean(deviceInfo.pinSupported);
            bool faceSupported = Convert.ToBoolean(deviceInfo.faceSupported);
            bool jobSupported = Convert.ToBoolean(deviceInfo.jobCodeSupported);
            bool phraseSupported = Convert.ToBoolean(deviceInfo.phraseCodeSupported);

            privateIDAuthMode.Add(BS2IDAuthModeEnum.PROHIBITED);

            if (cardSupported)
            {
                privateCardAuthMode.Add(BS2CardAuthModeEnum.PROHIBITED);
                privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_ONLY);

                if (pinSupported)
                {
                    privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_PIN);

                    privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_PIN);

                    if (fingerSupported)
                    {
                        privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_BIOMETRIC_OR_PIN);
                        privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_BIOMETRIC_PIN);

                        privateFingerAuthMode.Add(BS2FingerAuthModeEnum.BIOMETRIC_PIN);

                        privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_OR_PIN);
                        privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_PIN);
                    }

                    if (faceSupported)
                    {
                        privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_BIOMETRIC_OR_PIN);
                        privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_BIOMETRIC_PIN);

                        privateFaceAuthMode.Add(BS2FaceAuthModeEnum.BIOMETRIC_PIN);

                        privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_OR_PIN);
                        privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_PIN);
                    }
                }

                if (fingerSupported)
                {
                    privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_BIOMETRIC);

                    privateFingerAuthMode.Add(BS2FingerAuthModeEnum.BIOMETRIC_ONLY);

                    privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC);
                }

                if (faceSupported)
                {
                    privateCardAuthMode.Add(BS2CardAuthModeEnum.CARD_BIOMETRIC);

                    privateFaceAuthMode.Add(BS2FaceAuthModeEnum.BIOMETRIC_ONLY);

                    privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC);
                }
            }
            else if (fingerSupported)
            {
                if (pinSupported)
                {
                    privateFingerAuthMode.Add(BS2FingerAuthModeEnum.BIOMETRIC_PIN);

                    privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_OR_PIN);
                    privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_PIN);
                }

                privateFingerAuthMode.Add(BS2FingerAuthModeEnum.BIOMETRIC_ONLY);

                privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC);
            }
            else if (pinSupported)
            {
                privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_PIN);
            }
            else if (faceSupported)
            {
                if (pinSupported)
                {
                    privateFaceAuthMode.Add(BS2FaceAuthModeEnum.BIOMETRIC_PIN);

                    privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_OR_PIN);
                    privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC_PIN);
                }

                privateFaceAuthMode.Add(BS2FaceAuthModeEnum.BIOMETRIC_ONLY);
                privateIDAuthMode.Add(BS2IDAuthModeEnum.ID_BIOMETRIC);
            }

            BS2UserBlobEx userBlob = Util.AllocateStructure<BS2UserBlobEx>();
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

            Console.WriteLine("Enter the ID for the User which you want to enroll");
            Console.Write(">>>> ");
            string userID = Console.ReadLine();
            if (userID.Length == 0)
            {
                Console.WriteLine("The user id can not be empty.");
                return;
            }
            else if (userID.Length > BS2Envirionment.BS2_USER_ID_SIZE)
            {
                Console.WriteLine("The user id should less than {0} words.", BS2Envirionment.BS2_USER_ID_SIZE);
                return;
            }
            else
            {
                //TODO Alphabet user id is not implemented yet.
                UInt32 uid;
                if (!UInt32.TryParse(userID, out uid))
                {
                    Console.WriteLine("The user id should be a numeric.");
                    return;
                }

                byte[] userIDArray = Encoding.UTF8.GetBytes(userID);
                Array.Clear(userBlob.user.userID, 0, BS2Envirionment.BS2_USER_ID_SIZE);
                Array.Copy(userIDArray, userBlob.user.userID, userIDArray.Length);
            }

            Console.WriteLine("When is this user valid from? [default(Today), yyyy-MM-dd HH:mm:ss]");
            Console.Write(">>>> ");
            if (!Util.GetTimestamp("yyyy-MM-dd HH:mm:ss", 0, out userBlob.setting.startTime))
            {
                return;
            }

            Console.WriteLine("When is this user valid to? [default(Today), yyyy-MM-dd HH:mm:ss]");
            Console.Write(">>>> ");
            if (!Util.GetTimestamp("yyyy-MM-dd HH:mm:ss", 0, out userBlob.setting.endTime))
            {
                return;
            }

            if (cardSupported)
            {
                Console.WriteLine("Do you want to set the private card auth mode for this user? [Y/n]");
                Console.Write(">>>> ");
                if (Util.IsYes())
                {
                    Console.Write("Enter the private card auth mode. [");
                    foreach (BS2CardAuthModeEnum cardAuthModeEnum in privateCardAuthMode)
                    {
                        if (cardAuthModeEnum == BS2CardAuthModeEnum.CARD_ONLY)
                        {
                            Console.Write("{0}: {1}(default) ", (byte)cardAuthModeEnum, cardAuthModeEnum);
                        }
                        else
                        {
                            Console.Write("{0}: {1} ", (byte)cardAuthModeEnum, cardAuthModeEnum);
                        }
                    }
                    Console.WriteLine("]");
                    Console.Write(">>>> ");
                    userBlob.setting.cardAuthMode = Util.GetInput((byte)BS2CardAuthModeEnum.CARD_ONLY);
                }
                else
                {
                    userBlob.setting.cardAuthMode = (byte)BS2CardAuthModeEnum.NONE;
                }
            }

            if (fingerSupported)
            {
                Console.WriteLine("Enter the security level for this user: [{0}: {1}, {2}: {3}, {4}: {5}(default), {6}: {7}, {8}: {9}]",
                                (byte)BS2UserSecurityLevelEnum.LOWER,
                                BS2UserSecurityLevelEnum.LOWER,
                                (byte)BS2UserSecurityLevelEnum.LOW,
                                BS2UserSecurityLevelEnum.LOW,
                                (byte)BS2UserSecurityLevelEnum.NORMAL,
                                BS2UserSecurityLevelEnum.NORMAL,
                                (byte)BS2UserSecurityLevelEnum.HIGH,
                                BS2UserSecurityLevelEnum.HIGH,
                                (byte)BS2UserSecurityLevelEnum.HIGHER,
                                BS2UserSecurityLevelEnum.HIGHER);
                Console.Write(">>>> ");
                userBlob.setting.securityLevel = Util.GetInput((byte)BS2UserSecurityLevelEnum.NORMAL);

                Console.WriteLine("Do you want to set the private biometric auth mode for this user? [Y/n]");
                Console.Write(">>>> ");
                if (Util.IsYes())
                {
                    Console.Write("Enter the private biometric auth mode. [");
                    foreach (BS2FingerAuthModeEnum fingerAuthModeEnum in privateFingerAuthMode)
                    {
                        if (fingerAuthModeEnum == BS2FingerAuthModeEnum.BIOMETRIC_ONLY)
                        {
                            Console.Write("{0}: {1}(default) ", (byte)fingerAuthModeEnum, fingerAuthModeEnum);
                        }
                        else
                        {
                            Console.Write("{0}: {1} ", (byte)fingerAuthModeEnum, fingerAuthModeEnum);
                        }
                    }
                    Console.WriteLine("]");
                    Console.Write(">>>> ");
                    userBlob.setting.fingerAuthMode = Util.GetInput((byte)BS2FingerAuthModeEnum.BIOMETRIC_ONLY);
                }
                else
                {
                    userBlob.setting.fingerAuthMode = (byte)BS2FingerAuthModeEnum.NONE;
                }
            }

            if (faceSupported)
            {
                Console.WriteLine("Enter the security level for this user: [{0}: {1}, {2}: {3}, {4}: {5}, {6}: {7}, {8}: {9}]",
                                (byte)BS2UserSecurityLevelEnum.LOWER,
                                BS2UserSecurityLevelEnum.LOWER,
                                (byte)BS2UserSecurityLevelEnum.LOW,
                                BS2UserSecurityLevelEnum.LOW,
                                (byte)BS2UserSecurityLevelEnum.NORMAL,
                                BS2UserSecurityLevelEnum.NORMAL,
                                (byte)BS2UserSecurityLevelEnum.HIGH,
                                BS2UserSecurityLevelEnum.HIGH,
                                (byte)BS2UserSecurityLevelEnum.HIGHER,
                                BS2UserSecurityLevelEnum.HIGHER);
                Console.Write(">>>> ");
                userBlob.setting.securityLevel = Util.GetInput((byte)BS2UserSecurityLevelEnum.NORMAL);

                Console.WriteLine("Do you want to set the private biometric auth mode for this user? [Y/n]");
                Console.Write(">>>> ");
                if (Util.IsYes())
                {
                    Console.Write("Enter the private biometric auth mode. [");
                    foreach (BS2FaceAuthModeEnum faceAuthModeEnum in privateFaceAuthMode)
                    {
                        if (faceAuthModeEnum == BS2FaceAuthModeEnum.BIOMETRIC_ONLY)
                        {
                            Console.Write("{0}: {1}(default) ", (byte)faceAuthModeEnum, faceAuthModeEnum);
                        }
                        else
                        {
                            Console.Write("{0}: {1} ", (byte)faceAuthModeEnum, faceAuthModeEnum);
                        }
                    }
                    Console.WriteLine("]");
                    Console.Write(">>>> ");
                    userBlob.setting.fingerAuthMode = Util.GetInput((byte)BS2FaceAuthModeEnum.BIOMETRIC_ONLY);
                }
                else
                {
                    userBlob.setting.fingerAuthMode = (byte)BS2FaceAuthModeEnum.NONE;
                }
            }

            Console.WriteLine("Do you want to set the private id auth mode for this user? [Y/n]");
            Console.Write(">>>> ");
            if (Util.IsYes())
            {
                Console.Write("Enter the private id auth mode. [");
                foreach (BS2IDAuthModeEnum idAuthModeEnum in privateIDAuthMode)
                {
                    if (idAuthModeEnum == BS2IDAuthModeEnum.PROHIBITED)
                    {
                        Console.Write("{0}: {1}(default) ", (byte)idAuthModeEnum, idAuthModeEnum);
                    }
                    else
                    {
                        Console.Write("{0}: {1} ", (byte)idAuthModeEnum, idAuthModeEnum);
                    }
                }
                Console.WriteLine("]");
                Console.Write(">>>> ");
                userBlob.setting.idAuthMode = Util.GetInput((byte)BS2IDAuthModeEnum.PROHIBITED);
            }
            else
            {
                userBlob.setting.idAuthMode = (byte)BS2IDAuthModeEnum.NONE;
            }

            Array.Clear(userBlob.name, 0, BS2Envirionment.BS2_USER_NAME_LEN);
            Console.WriteLine("Do you want to set user name? [Y/n]");
            Console.Write(">>>> ");
            if (Util.IsYes())
            {
                Console.WriteLine("Enter the name for this user");
                Console.Write(">>>> ");
                string userName = Console.ReadLine();
                if (userName.Length == 0)
                {
                    Console.WriteLine("[Warning] user name will be displayed as empty.");
                }
                else if (userName.Length > BS2Envirionment.BS2_USER_NAME_LEN)
                {
                    Console.WriteLine("The user name should less than {0} words.", BS2Envirionment.BS2_USER_NAME_LEN);
                    return;
                }
                else
                {
                    byte[] userNameArray = Encoding.UTF8.GetBytes(userName);
                    Array.Copy(userNameArray, userBlob.name, userNameArray.Length);
                }
            }

            userBlob.photo.size = 0;
            Array.Clear(userBlob.photo.data, 0, BS2Envirionment.BS2_USER_PHOTO_SIZE);
            Console.WriteLine("Do you want to set profile image? [Y/n]");
            Console.Write(">>>> ");
            if (Util.IsYes())
            {
                Console.WriteLine("Enter the jpg file path for this user.");
                Console.Write(">>>> ");
                string imagePath = Console.ReadLine();

                if (!File.Exists(imagePath))
                {
                    Console.WriteLine("Invalid file path");
                    return;
                }

                Image profileImage = Image.FromFile(imagePath);
                if (!profileImage.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Jpeg))
                {
                    Console.WriteLine("Invalid image file format");
                    return;
                }

                IntPtr imageData = IntPtr.Zero;
                UInt32 imageDataLen = 0;

                if (Util.LoadBinary(imagePath, out imageData, out imageDataLen))
                {
                    if (imageDataLen == 0)
                    {
                        Console.WriteLine("Empty image file");
                        return;
                    }
                    else if (imageDataLen > BS2Envirionment.BS2_USER_PHOTO_SIZE)
                    {
                        Console.WriteLine("The profile image should less than {0} bytes.", BS2Envirionment.BS2_USER_PHOTO_SIZE);
                        return;
                    }

                    userBlob.photo.size = imageDataLen;
                    Marshal.Copy(imageData, userBlob.photo.data, 0, (int)imageDataLen);
                    Marshal.FreeHGlobal(imageData);
                }
            }

            Array.Clear(userBlob.pin, 0, BS2Envirionment.BS2_PIN_HASH_SIZE);
            Console.WriteLine("Do you want to set pin code? [Y/n]");
            Console.Write(">>>> ");
            if (Util.IsYes())
            {
                Console.WriteLine("Enter pin code for this user");
                Console.Write(">>>> ");
                string pin = Console.ReadLine();
                if (pin.Length == 0)
                {
                    Console.WriteLine("Pin code can not be empty.");
                    return;
                }
                else if (pin.Length > BS2Envirionment.BS2_PIN_HASH_SIZE)
                {
                    Console.WriteLine("Pin code should less than {0} words.", BS2Envirionment.BS2_PIN_HASH_SIZE);
                    return;
                }
                else
                {
                    IntPtr pinCode = Marshal.AllocHGlobal(BS2Envirionment.BS2_PIN_HASH_SIZE);
                    result = (BS2ErrorCode)API.BS2_MakePinCode(sdkContext, pin, pinCode);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("Can't generate pin code.");
                        return;
                    }

                    Marshal.Copy(pinCode, userBlob.pin, 0, BS2Envirionment.BS2_PIN_HASH_SIZE);
                    Marshal.FreeHGlobal(pinCode);
                }
            }

            if (cardSupported)
            {
                Console.WriteLine("How many cards do you want to register? [1(default) - {0}]", BS2Envirionment.BS2_MAX_NUM_OF_CARD_PER_USER);
                Console.Write(">>>> ");
                userBlob.user.numCards = Util.GetInput((byte)1);

                if (userBlob.user.numCards > 0)
                {
                    int structSize = Marshal.SizeOf(typeof(BS2CSNCard));
                    BS2Card card = Util.AllocateStructure<BS2Card>();
                    userBlob.cardObjs = Marshal.AllocHGlobal(structSize * userBlob.user.numCards);
                    IntPtr curCardObjs = userBlob.cardObjs;
                    cbCardOnReadyToScan = new API.OnReadyToScan(ReadyToScanForCard);

                    for (byte idx = 0; idx < userBlob.user.numCards; )
                    {
                        Console.WriteLine("Trying to scan card.");
                        result = (BS2ErrorCode)API.BS2_ScanCard(sdkContext, deviceID, out card, cbCardOnReadyToScan);
                        if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                        {
                            Console.WriteLine("Got error({0}).", result);
                            return;
                        }
                        else if (Convert.ToBoolean(card.isSmartCard))
                        {
                            Console.WriteLine("CSN card is only available. Try again");
                        }
                        else
                        {
                            Marshal.Copy(card.cardUnion, 0, curCardObjs, structSize);
                            curCardObjs += structSize;
                            ++idx;
                        }
                    }

                    cbCardOnReadyToScan = null;
                }
            }

            if (fingerSupported)
            {
                Console.WriteLine("How many fingerprints do you want to register? [1(default) - {0}]", BS2Envirionment.BS2_MAX_NUM_OF_FINGER_PER_USER);
                Console.Write(">>>> ");
                userBlob.user.numFingers = Util.GetInput((byte)1);

                if (userBlob.user.numFingers > 0)
                {
                    BS2FingerprintConfig fingerprintConfig;
                    Console.WriteLine("Trying to get fingerprint config");
                    result = (BS2ErrorCode)API.BS2_GetFingerprintConfig(sdkContext, deviceID, out fingerprintConfig);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("Got error({0}).", result);
                        return;
                    }
                    else
                    {
                        templateFormat = (BS2FingerprintTemplateFormatEnum)fingerprintConfig.templateFormat;
                    }

                    int structSize = Marshal.SizeOf(typeof(BS2Fingerprint));
                    BS2Fingerprint fingerprint = Util.AllocateStructure<BS2Fingerprint>();
                    userBlob.fingerObjs = Marshal.AllocHGlobal(structSize * userBlob.user.numFingers);
                    IntPtr curFingerObjs = userBlob.fingerObjs;
                    cbFingerOnReadyToScan = new API.OnReadyToScan(ReadyToScanForFinger);

                    UInt32 outquality;
                    for (int idx = 0; idx < userBlob.user.numFingers; ++idx)
                    {
                        Console.WriteLine("Trying to get fingerprint[{0}]", idx);
                        for (UInt32 templateIndex = 0; templateIndex < BS2Envirionment.BS2_TEMPLATE_PER_FINGER; )
                        {
                            Console.WriteLine("Trying to scan finger.");
                            result = (BS2ErrorCode)API.BS2_ScanFingerprintEx(sdkContext, deviceID, ref fingerprint, templateIndex, (UInt32)BS2FingerprintQualityEnum.QUALITY_STANDARD, (byte)templateFormat, out outquality, cbFingerOnReadyToScan);
                            if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                            {
                                if (result == BS2ErrorCode.BS_SDK_ERROR_EXTRACTION_LOW_QUALITY ||
                                    result == BS2ErrorCode.BS_SDK_ERROR_CAPTURE_LOW_QUALITY)
                                {
                                    Console.WriteLine("Bad fingerprint quality. Try again");
                                }
                                else
                                {
                                    Console.WriteLine("Got error({0}).", result);
                                    return;
                                }
                            }
                            else
                            {
                                ++templateIndex;
                            }
                        }

                        Console.WriteLine("Verify the fingerprints.");
                        result = (BS2ErrorCode)API.BS2_VerifyFingerprint(sdkContext, deviceID, ref fingerprint);
                        if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                        {
                            if (result == BS2ErrorCode.BS_SDK_ERROR_NOT_SAME_FINGERPRINT)
                            {
                                Console.WriteLine("The fingerprint does not match. Try again");
                                --idx;
                                continue;
                            }
                            else
                            {
                                Console.WriteLine("Got error({0}).", result);
                                return;
                            }
                        }

                        fingerprint.index = (byte)idx;
                        Console.WriteLine("Is it duress finger? [0 : Normal(default), 1 : Duress]");
                        Console.Write(">>>> ");
                        fingerprint.flag = Util.GetInput((byte)BS2FingerprintFlagEnum.NORMAL);

                        Marshal.StructureToPtr(fingerprint, curFingerObjs, false);
                        curFingerObjs += structSize;
                    }

                    cbFingerOnReadyToScan = null;
                }
            }

            if (faceSupported)
            {
                Console.WriteLine("How many faces do you want to register? [1(default) - {0}]", BS2Envirionment.BS2_MAX_NUM_OF_FACE_PER_USER);
                Console.Write(">>>> ");
                userBlob.user.numFaces = Util.GetInput((byte)1);

                if (userBlob.user.numFaces > 0)
                {
                    byte enrollThreshold;
                    BS2FaceConfig faceConfig;
                    Console.WriteLine("Trying to get face config");
                    result = (BS2ErrorCode)API.BS2_GetFaceConfig(sdkContext, deviceID, out faceConfig);
                    if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                    {
                        Console.WriteLine("Got error({0}).", result);
                        return;
                    }
                    else
                    {
                        enrollThreshold = faceConfig.enrollThreshold;
                    }

                    int structSize = Marshal.SizeOf(typeof(BS2Face));
                    BS2Face[] face = Util.AllocateStructureArray<BS2Face>(1);

                    userBlob.faceObjs = Marshal.AllocHGlobal(structSize * userBlob.user.numFaces);
                    IntPtr curFaceObjs = userBlob.faceObjs;
                    cbFaceOnReadyToScan = new API.OnReadyToScan(ReadyToScanForFace);

                    for (int idx = 0; idx < userBlob.user.numFaces; ++idx)
                    {
                        Console.WriteLine("Trying to scan face[{0}]", idx);
                        result = (BS2ErrorCode)API.BS2_ScanFace(sdkContext, deviceID, face, enrollThreshold, cbFaceOnReadyToScan);
                        if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                        {
                            Console.WriteLine("Got error({0}).", result);
                            return;
                        }

                        face[0].faceIndex = (byte)idx;
                        Marshal.StructureToPtr(face[0], curFaceObjs, false);
                        curFaceObjs += structSize;
                    }

                    cbFaceOnReadyToScan = null;
                }
            }

            if (phraseSupported)
            {
                Array.Clear(userBlob.phrase, 0, BS2Envirionment.BS2_USER_PHRASE_SIZE);
                Console.WriteLine("Do you want to set user phrase? [Y/n]");
                Console.Write(">>>> ");
                if (Util.IsYes())
                {
                    Console.WriteLine("Enter the phrase for this user");
                    Console.Write(">>>> ");
                    string userPhrase = Console.ReadLine();
                    if (userPhrase.Length == 0)
                    {
                        Console.WriteLine("[Warning] user phrase will be displayed as empty.");
                    }
                    else if (userPhrase.Length > BS2Envirionment.BS2_USER_PHRASE_SIZE)
                    {
                        Console.WriteLine("The user phrase should less than {0} words.", BS2Envirionment.BS2_USER_PHRASE_SIZE);
                        return;
                    }
                    else
                    {
                        byte[] userPhraseArray = Encoding.UTF8.GetBytes(userPhrase);
                        Array.Copy(userPhraseArray, userBlob.phrase, userPhraseArray.Length);
                    }
                }
            }

            if (jobSupported)
            {
                Console.WriteLine("How many jobs do you want to set? [1(default)-16]");
                Console.Write(">>>> ");
                char[] delimiterChars2 = { ' ', ',', '.', ':', '\t' };
                byte amount = Util.GetInput(1);
                userBlob.job.numJobs = amount;

                for (int idx = 0; idx < amount; ++idx)
                {
                    Console.WriteLine("Enter a value for job[{0}]", idx);
                    Console.WriteLine("  Enter the code for the job which you want to set [1(default) .... N]");
                    Console.Write("  >>>> ");
                    userBlob.job.jobs[idx].code = (UInt32)Util.GetInput();
                    Console.WriteLine("  Enter the label for the job which you want to set");
                    Console.Write("  >>>> ");
                    string label = Console.ReadLine();
                    if (label.Length == 0)
                    {
                        Console.WriteLine("  [Warning] label will be displayed as empty.");
                    }
                    else if (label.Length > BS2Envirionment.BS2_MAX_JOBLABEL_LEN)
                    {
                        Console.WriteLine("  label of job should less than {0} words.", BS2Envirionment.BS2_MAX_JOBLABEL_LEN);
                        return;
                    }
                    else
                    {
                        byte[] labelArray = Encoding.UTF8.GetBytes(label);
                        Array.Clear(userBlob.job.jobs[idx].label, 0, BS2Envirionment.BS2_MAX_JOBLABEL_LEN);
                        Array.Copy(labelArray, userBlob.job.jobs[idx].label, labelArray.Length);
                    }
                }
            }

#if false //TODO TBD 
            if (Convert.ToBoolean(deviceInfo.faceSupported))
            {
            }
#endif
            Array.Clear(userBlob.accessGroupId, 0, BS2Envirionment.BS2_MAX_ACCESS_GROUP_PER_USER);

            Console.WriteLine("Which access groups does this user belongs to? [ex. ID_1 ID_2 ...]");
            Console.Write(">>>> ");
            int accessGroupIdIndex = 0;
            char[] delimiterChars = { ' ', ',', '.', ':', '\t' };
            string[] accessGroupIDs = Console.ReadLine().Split(delimiterChars);

            foreach (string accessGroupID in accessGroupIDs)
            {
                if (accessGroupID.Length > 0)
                {
                    UInt32 item;
                    if (UInt32.TryParse(accessGroupID, out item))
                    {
                        userBlob.accessGroupId[accessGroupIdIndex++] = item;
                    }
                }
            }

            Console.WriteLine("Trying to enroll user.");
            if (!dbHandler.AddUserBlobEx(ref userBlob, templateFormat))
            {
                Console.WriteLine("Can not enroll user in the system.");
            }

            //Release memory
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
        }

        public void insertUserIntoDeviceEx(IntPtr sdkContext, UInt32 deviceID, bool isMasterDevice)
        {
            List<BS2User> userList = new List<BS2User>();
            if (dbHandler.GetUserList(ref deviceInfo, ref userList))
            {
                if (userList.Count > 0)
                {
                    Console.WriteLine("+----------------------------------------------------------------------------------------------------------+");
                    for (int idx = 0; idx < userList.Count; ++idx)
                    {
                        Console.Write("[{0:000}] ==> ", idx);
                        print(sdkContext, userList[idx]);
                    }
                    Console.WriteLine("+----------------------------------------------------------------------------------------------------------+");
                    Console.WriteLine("Please, choose the index of the user which you want to enroll.");
                    Console.Write(">>>> ");

                    Int32 selection = Util.GetInput();
                    if (selection >= 0)
                    {
                        if (selection < userList.Count)
                        {
                            BS2User user = userList[selection];
                            BS2UserBlobEx[] userBlob = Util.AllocateStructureArray<BS2UserBlobEx>(1);
                            if (dbHandler.GetUserBlobEx(ref deviceInfo, ref user, ref userBlob[0]))
                            {
                                Console.WriteLine("Trying to enroll user.");
                                BS2ErrorCode result = (BS2ErrorCode)API.BS2_EnrolUserEx(sdkContext, deviceID, userBlob, 1, 1);
                                if (result != BS2ErrorCode.BS_SDK_SUCCESS)
                                {
                                    Console.WriteLine("Got error({0}).", result);
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
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid selection[{0}]", selection);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid user index");
                    }
                }
                else
                {
                    Console.WriteLine("There is no user.");
                }
            }
            else
            {
                Console.WriteLine("An error occurred while attempting to retrieve user list.");
            }
        }
    }
}
