using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ruvents.Server.Data;
using Ruvents.Shared;

namespace Ruvents.Server.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NotificationController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPut]
        public async Task<NotificationSubscription> Subscribe(NotificationSubscription subscription)
        {
            // We're storing at most one subscription per user, so delete old ones.
            // Alternatively, you could let the user register multiple subscriptions from different browsers/devices.
            //var oldSubscriptions = _context.NotificationSubscriptions.Where(n => n.Sub == subscription.Sub);
            //_context.NotificationSubscriptions.RemoveRange(oldSubscriptions);

            // Store new subscription
            _context.NotificationSubscriptions.Add(subscription);

            await _context.SaveChangesAsync();
            return subscription;
        }
    }
}
