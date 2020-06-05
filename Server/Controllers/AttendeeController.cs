using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ruvents.Server.Data;
using Ruvents.Shared;
using WebPush;

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

            var ruvent = await _context.Ruvents.Where(r => r.RuventId == attendee.RuventId).FirstOrDefaultAsync();

            if (attendee.Sub != ruvent.CreatedBySub)
            {
                var subscription = await _context.NotificationSubscriptions.Where(n => n.Sub == ruvent.CreatedBySub).FirstOrDefaultAsync();

                if (subscription != null)
                {
                    var sb = new StringBuilder();
                    sb.AppendFormat($"{attendee.UserName} ");

                    if (attendee.IsAttending)
                    {
                        sb.AppendFormat($"is attending {ruvent.Title}!");
                    }
                    else
                    {
                        sb.AppendFormat($"is not attending {ruvent.Title}.");
                    }

                    await SendNotificationAsync(ruvent, subscription, sb.ToString());
                }
            }

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

            var ruvent = await _context.Ruvents.Where(r => r.RuventId == attendee.RuventId).FirstOrDefaultAsync();

            if (attendee.Sub != ruvent.CreatedBySub)
            {
                var subscription = await _context.NotificationSubscriptions.Where(n => n.Sub == ruvent.CreatedBySub).FirstOrDefaultAsync();

                if (subscription != null)
                {
                    var sb = new StringBuilder();
                    sb.AppendFormat($"{attendee.UserName} ");

                    if (attendee.IsAttending)
                    {
                        sb.AppendFormat($"is attending {ruvent.Title}!");
                    }
                    else
                    {
                        sb.AppendFormat($"is not attending {ruvent.Title}.");
                    }

                    await SendNotificationAsync(ruvent, subscription, sb.ToString());
                }
            }

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

            var ruvent = await _context.Ruvents.Where(r => r.RuventId == attendee.RuventId).FirstOrDefaultAsync();

            if (attendee.Sub != ruvent.CreatedBySub)
            {
                var subscription = await _context.NotificationSubscriptions.Where(n => n.Sub == ruvent.CreatedBySub).FirstOrDefaultAsync();

                if (subscription != null)
                {
                    var sb = new StringBuilder();
                    sb.AppendFormat($"{attendee.UserName} is now undecided about attending {ruvent.Title}!");

                    await SendNotificationAsync(ruvent, subscription, sb.ToString());
                }
            }

            return attendee;
        }

        private static async Task SendNotificationAsync(Ruvent ruvent, NotificationSubscription subscription, string message)
        {
            // For a real application, generate your own
            var publicKey = "BPsXC1sL8PQyEEkJY_zoAPPwfM7LuDBtbZMD0Vbqh2IAIv-xv94h8CoJl5EZYoRQqttjqMz8KF593NtuJtplQuw";
            var privateKey = "i6tV7C1m5zloELFkEHqkyF1PepcprpR5fPgree2hMP4";

            var pushSubscription = new PushSubscription(subscription.Url, subscription.P256dh, subscription.Auth);
            var vapidDetails = new VapidDetails("mailto:eruvalca@outlook.com", publicKey, privateKey);
            var webPushClient = new WebPushClient();
            try
            {
                var payload = JsonSerializer.Serialize(new
                {
                    message,
                    url = $"https://ruvents.azurewebsites.net/detail/{ruvent.RuventId}"
                });

                await webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error sending push notification: " + ex.Message);
            }
        }
    }
}
