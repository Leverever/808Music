using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Services;
using RS1_2024_25.API.Services.Interfaces;
using RS1_2024_25.Tests.Data;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using static ProductAddEndpoint;

namespace RS1_2024_25.Tests.Endpoints.Product
{
    public class ProductAddEndpointTests
    {
        private readonly ApplicationDbContext _db;
        private readonly IMyFileHandler _fileHandler;

        public ProductAddEndpointTests()
        {
            _db = TestApplicationDbContext.CreateTestDbContext().GetAwaiter().GetResult();
            _fileHandler = new TestFileHandler();
        }

        [Fact]
        public async Task HandleAsync_ShouldAddProduct_WhenValidRequest()
        {
            string tempDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products", "thumbnails");
            if (!Directory.Exists(tempDirectory))
            {
                Directory.CreateDirectory(tempDirectory);
            }

            using var image = new Image<Rgba32>(1, 1);
            using var imageStream = new MemoryStream();
            await image.SaveAsPngAsync(imageStream);
            var photoBytes = imageStream.ToArray();

            // Get a token for the user
            var user = _db.MyAppUsers.First();
            var tokenProvider = new TokenProvider(TestIConfiguration.CreateIConfiguration(), _db);
            string token = tokenProvider.Create(user);

            // Create Http context and add JWT Bearer
            var context = new DefaultHttpContext();
            context.Request.Headers.Add("Authorization", $"Bearer {token}");

            var endpoint = new ProductAddEndpoint(_db, _fileHandler)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = context,
                }
            };

            var request = new ProductAddRequest
            {
                Title = "Test Product",
                Price = 19.99f,
                Quantity = 10,
                isDigital = false,
                Bio = "Test bio",
                ArtistId = 1,
                ProductType = RS1_2024_25.API.Data.Models.ProductType.Clothes,
                ClothesType = RS1_2024_25.API.Data.Models.ClothesType.Jacket,
                Photos = new List<IFormFile>
                {
                    new FormFile(new MemoryStream(photoBytes), 0, photoBytes.Length, "file", "test.png")
                }
            };

            var response = await endpoint.HandleAsync(request, CancellationToken.None);

            Assert.NotNull(response);
            Assert.Equal("Test Product", response.Title);
            Assert.Equal(19.99f, response.Price);
            Assert.Equal(10, response.Quantity);
            Assert.Equal(1, response.ArtistId);
            Assert.Equal(RS1_2024_25.API.Data.Models.ProductType.Clothes, response.ProductType);
        }

        [Fact]
        public async Task HandleAsync_ShouldReturnNull_WhenClothesTypeIsProvidedButProductTypeIsNotClothes()
        {
            var request = new ProductAddRequest
            {
                Title = "Test Product",
                Price = 19.99f,
                Quantity = 10,
                Bio = "Test bio",
                isDigital = false,
                ArtistId = 1,
                ProductType = RS1_2024_25.API.Data.Models.ProductType.CDS,
                ClothesType = RS1_2024_25.API.Data.Models.ClothesType.Jacket,
            };

            // Get a token for the user
            var user = _db.MyAppUsers.First();
            var tokenProvider = new TokenProvider(TestIConfiguration.CreateIConfiguration(), _db);
            string token = tokenProvider.Create(user);

            // Create Http context and add JWT Bearer
            var context = new DefaultHttpContext();
            context.Request.Headers.Add("Authorization", $"Bearer {token}");

            var endpoint = new ProductAddEndpoint(_db, _fileHandler)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = context,
                }
            };

            var response = await endpoint.HandleAsync(request, CancellationToken.None);

            Assert.Null(response);
        }
    }
}
