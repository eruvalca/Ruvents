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
    public class RuventController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public RuventController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("{month}/{year}")]
        public async Task<ActionResult<IEnumerable<Ruvent>>> GetRuventsByMonthAndYear(int month, int year)
        {
            return await _context.Ruvents.Where(r => r.StartDate.Month == month && r.StartDate.Year == year)
                .Include("Attendees").ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Ruvent>> GetRuvent(int id)
        {
            if (!_context.Ruvents.Any(r => r.RuventId == id))
            {
                return NotFound();
            }

            return await _context.Ruvents.Where(r => r.RuventId == id).Include("Attendees").FirstOrDefaultAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Ruvent>> CreateRuvent(Ruvent ruvent)
        {
            _context.Ruvents.Add(ruvent);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetRuvent", new { id = ruvent.RuventId }, ruvent);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Ruvent>> UpdateRuvent(int id, Ruvent ruvent)
        {
            if (id != ruvent.RuventId)
            {
                return BadRequest();
            }

            _context.Entry(ruvent).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return ruvent;
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<Ruvent>> DeleteRuvent(int id)
        {
            var ruvent = await _context.Ruvents.FindAsync(id);

            if (ruvent == null)
            {
                return NotFound();
            }

            try
            {
                _context.Ruvents.Remove(ruvent);
                await _context.SaveChangesAsync();
            }
            catch
            {
                return BadRequest();
            }

            return ruvent;
        }
    }
}
