using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BioStarServer
{
    class Device
    {
        public int Id { get; set; }
        public string Ip { get; set; }
        public string DeviceName { get; set; }
        public int Enable { get; set; }
        public string GroupCodeJoinStr { get; set; }
        public string GroupCodeNameJoinStr { get; set; }
        public string DeviceIdentifier { get; set; }
        public string DeviceTypeEnum { get; set; }
        public string LastRecordStamp { get; set; }
    }
}
