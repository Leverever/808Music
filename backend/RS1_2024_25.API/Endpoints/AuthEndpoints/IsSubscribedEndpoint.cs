using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Data.Models.Auth;
using RS1_2024_25.API.Helper.Api;

namespace RS1_2024_25.API.Endpoints.UserEndpoints
{
    public class IsSubscribedEndpoint(ApplicationDbContext db) : MyEndpointBaseAsync.WithRequest<int>.WithActionResult
    {
        [HttpGet("api/IsSubscribed")]
        public override async Task<ActionResult> HandleAsync(int userId, CancellationToken cancellationToken = default)
        {
            return Ok(new { isSubscribed = true, message = "User is subscribed." });
            var user = await db.MyAppUsers
                                .Include(u => u.Subscription)
                                .FirstOrDefaultAsync(u => u.ID == userId, cancellationToken);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            if (user.Subscription == null || user.Subscription.EndDate < DateTime.UtcNow)
            {
                return Ok(new { isSubscribed = false, message = "Your subscription has expired or is not active." });
            }

            return Ok(new { isSubscribed = true, message = "User is subscribed." });
        }
    }
}
