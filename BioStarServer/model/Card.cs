using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BioStarServer.model
{
    class Card
    {
        public string CardId { get; set; }
        public string CardSn { get; set; }
        public string UserName { get; set; }
        public string UserIdentifier { get; set; }
        public string UserGroupCodeJoinStr { get; set; }
        public string UserGroupCodeNameJoinStr { get; set; }
    }
}
