using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Data.Models;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Helper.Api;
using System.IO;
using static RS1_2024_25.API.Endpoints.EventEndpoints.EventAddEndpoint;

namespace RS1_2024_25.API.Endpoints.EventEndpoints
{
    public class EventAddEndpoint : MyEndpointBaseAsync
        .WithRequest<EventAddRequest>
        .WithResult<EventAddResponse>
    {
        private readonly ApplicationDbContext _db;

        public EventAddEndpoint(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpPost("api/EventAdd")]
        public override async Task<EventAddResponse> HandleAsync([FromForm] EventAddRequest request, CancellationToken cancellationToken = default)
        {
          
            var roundedLongitude = Math.Round(request.Longitude, 6);
            var roundedLatitude = Math.Round(request.Latitude, 6);

            var eventItem = new Events
            {
                City = request.City,
                Country = request.Country,
                EventDate = request.EventDate,
                Venue = request.Venue,
                ArtistId = request.ArtistId,
                EventTitle = request.EventTitle,
                Longitude = roundedLongitude, 
                Latitude = roundedLatitude,  
            };

            if (request.EventCover != null)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(request.EventCover.FileName);
                var filePath = Path.Combine("wwwroot", "Images", "events", fileName);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.EventCover.CopyToAsync(stream, cancellationToken);
                }

                eventItem.EventCover = $"/Images/events/{fileName}";
            }

            _db.Events.Add(eventItem);
            await _db.SaveChangesAsync(cancellationToken);

            return new EventAddResponse
            {
                Id = eventItem.Id,
                City = eventItem.City,
                Country = eventItem.Country,
                EventDate = eventItem.EventDate,
                Venue = eventItem.Venue,
                EventCover = eventItem.EventCover,
                EventTitle = eventItem.EventTitle,
                Longitude = eventItem.Longitude,
                Latitude = eventItem.Latitude,
            };
        }


        public class EventAddRequest
        {
            public required string City { get; set; }
            public required string EventTitle { get; set; }

            public required string Country { get; set; }
            public required DateTime EventDate { get; set; }
            public required string Venue { get; set; }
            public IFormFile? EventCover { get; set; }
            public required decimal Longitude { get; set; }
            public required decimal Latitude { get; set; }
            public required int ArtistId { get; set; }
        }

        public class EventAddResponse
        {
            public int Id { get; set; }
            public string City { get; set; }
            public required string EventTitle { get; set; }

            public string Country { get; set; }
            public DateTime EventDate { get; set; }
            public string Venue { get; set; }
            public string? EventCover { get; set; }

            public required decimal Longitude { get; set; }
            public required decimal Latitude { get; set; }
        }
    }
}
