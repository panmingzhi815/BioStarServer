using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BioStarServer
{
    class Task
    {
        public int Id { get; set; }
        public string CardSN { get; set; }
        public string CardID { get; set; }
        public string UserName { get; set; }
        public int CardType { get; set; }
        public int RecordType { get; set; }
    }
}
