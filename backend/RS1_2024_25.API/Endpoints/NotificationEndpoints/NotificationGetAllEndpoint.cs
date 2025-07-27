using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Helper;
using RS1_2024_25.API.Helper.Api;
using RS1_2024_25.API.Services;

namespace RS1_2024_25.API.Endpoints.NotificationEndpoints
{
    public class NotificationGetAllEndpoint(ApplicationDbContext db, TokenProvider tp, NotificationTransformerService nt) : MyEndpointBaseAsync.WithoutRequest.WithActionResult<RichNotification>
    {
        [Authorize]
        [HttpGet]
        public override async Task<ActionResult<RichNotification>> HandleAsync(CancellationToken cancellationToken = default)
        {
            int userId = int.Parse(tp.GetJwtSub(Request));

            List<int> readNotiIds = await db.ReadNotifications.Where(rn => rn.MyAppUserId == userId).Select(rn => rn.NotificationId).ToListAsync(cancellationToken);
            List<int> artistIds = await db.Follows.Where(f => f.MyAppUserId == userId && f.WantsNotifications).Select(f => f.ArtistId).ToListAsync(cancellationToken);

            var notis = await db.Notifications.Where(n => !readNotiIds.Contains(n.Id) && artistIds.Contains(n.ArtistId) && n.Sent && n.CreatedAt.AddDays(90) > DateTime.Now).OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);

            List<RichNotification> result = new List<RichNotification>();
            var preference = await db.MyAppUserPreferences.Where(p => p.MyAppUserId == userId).Select(p => p.NotificationTypePriority).FirstOrDefaultAsync(cancellationToken);
            foreach (var n in notis)
            {
                var richNoti = await nt.GetRichNotificationAsync(n);
                richNoti.Priority = richNoti.Type == preference;
                result.Add(richNoti);
            }

            var msgs = await db.ChatMessages.Where(cm => (cm.UserChat.SecondaryChatterId == userId || cm.UserChat.PrimaryChatterId == userId) && cm.SenderId != userId && !cm.UserChat.Blocked && !cm.Seen).Include(cm => cm.UserChat)
                .ToListAsync(cancellationToken);

            foreach(var msg in msgs)
            {
                var richNoti = await nt.GetRichNotificationAsync(msg);
                richNoti.Priority = richNoti.Type == preference;
                result.Add(richNoti);
            }

            result = result.OrderByDescending(rn => rn.CreatedAt).ToList();

            return Ok(result);
        }
    }

    public class NotificationRequest : MyPagingRequest
    {
        
    }
}
