using System;
using System.Collections.Generic;
using System.Text;

namespace Ruvents.Shared
{
    public class Ruvent
    {
        public int RuventId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime CreateDate { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedBySub { get; set; }
        public DateTime? ModifyDate { get; set; }
        public string ModifyBy { get; set; }

        public List<Attendee> Attendees { get; set; }
    }
}
