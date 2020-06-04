using System;
using System.Collections.Generic;
using System.Text;

namespace Ruvents.Shared
{
    public class Attendee
    {
        public int AttendeeId { get; set; }
        public string Sub { get; set; }
        public string UserName { get; set; }
        public bool IsAttending { get; set; }

        public int RuventId { get; set; }
    }
}
