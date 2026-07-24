using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Helper.Api;
using RS1_2024_25.API.Services;

namespace RS1_2024_25.API.Endpoints.UserEndpoints
{
    public class GetUserUnreadsEndpoint(ApplicationDbContext db, TokenProvider tp) : MyEndpointBaseAsync.WithoutRequest.WithActionResult<UnreadsResponse>
    {
        [Authorize]
        [HttpGet]
        public override async Task<ActionResult<UnreadsResponse>> HandleAsync(CancellationToken cancellationToken = default)
        {
            int userId = int.Parse(tp.GetJwtSub(Request));

            var chats = await db.UserChats.Where(c => c.PrimaryChatterId == userId || c.SecondaryChatterId == userId).Select(c => c.Id).ToListAsync();
            int unreadMsgCount = await db.ChatMessages.Where(cm => cm.SenderId != userId && !cm.Seen && chats.Contains(cm.UserChatId)).CountAsync(cancellationToken);

            var readNotis = await db.ReadNotifications.Where(rn => rn.MyAppUserId == userId).Select(rn => rn.NotificationId).ToListAsync(cancellationToken);
            var artistIds = await db.Follows.Where(f => f.WantsNotifications && f.MyAppUserId == userId).Select(f => f.ArtistId).ToListAsync(cancellationToken);
            var unreadNotisCount = await db.Notifications.Where(n => !readNotis.Contains(n.Id) && artistIds.Contains(n.ArtistId) && n.Sent && n.CreatedAt.AddDays(90) > DateTime.Now).CountAsync(cancellationToken);

            return Ok(new UnreadsResponse
            {
                UnreadMessaggesCount = unreadMsgCount,
                UnreadNotificationsCount = unreadNotisCount + unreadMsgCount
            });
        }
    }

    public class UnreadsResponse
    {
        public int UnreadMessaggesCount { get; set; }
        public int UnreadNotificationsCount { get; set; }
    }
}
