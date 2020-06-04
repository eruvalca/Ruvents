using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ruvents.Server.Data;
using Ruvents.Shared;

namespace Ruvents.Server.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AttendeeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AttendeeController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<Attendee>> CreateAttendee(Attendee attendee)
        {
            _context.Attendees.Add(attendee);
            await _context.SaveChangesAsync();

            return attendee;
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Attendee>> UpdateAttendee(int id, Attendee attendee)
        {
            if (id != attendee.AttendeeId)
            {
                return BadRequest();
            }

            _context.Entry(attendee).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return attendee;
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<Attendee>> DeleteAttendee(int id)
        {
            var attendee = await _context.Attendees.FindAsync(id);
            if (attendee == null)
            {
                return NotFound();
            }

            try
            {
                _context.Attendees.Remove(attendee);
                await _context.SaveChangesAsync();
            }
            catch
            {
                return BadRequest();
            }

            return attendee;
        }
    }
}
