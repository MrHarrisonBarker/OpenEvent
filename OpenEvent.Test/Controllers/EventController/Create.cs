using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using OpenEvent.Data.Models.Event;
using OpenEvent.Web;
using OpenEvent.Web.Services;

namespace OpenEvent.Test.Controllers.EventController
{
    [TestFixture]
    public class Create
    {
        private readonly Mock<IEventService> EventServiceMock = new();
        private readonly Mock<IRecommendationService> RecommendationServiceMock = new();
        private readonly Mock<IWorkQueue> WorkQueueMock = new();

        private readonly CreateEventBody CreateEventBody = new();
        private readonly EventViewModel EventViewModel = new();

        private readonly CreateEventBody SaveErrorBody = new()
        {
            Name = "Should throw db save error"
        };

        private Web.Controllers.EventController EventController;

        [SetUp]
        public async Task Setup()
        {
            EventServiceMock.Setup(x => x.Create(CreateEventBody)).ReturnsAsync(EventViewModel);
            EventServiceMock.Setup(x => x.Create(SaveErrorBody)).ThrowsAsync(new DbUpdateException());
            EventController = new Web.Controllers.EventController(
                EventServiceMock.Object,
                new Mock<ILogger<Web.Controllers.EventController>>().Object, 
                RecommendationServiceMock.Object,
                WorkQueueMock.Object);
        }

        [Test]
        public async Task ShouldCreate()
        {
            var result = await EventController.Create(CreateEventBody);
            result.Should().BeOfType<ActionResult<EventViewModel>>();
        }

        [Test]
        public async Task ShouldThrowDbUpdateException()
        {
            var result = await EventController.Create(SaveErrorBody);
            result.Result.Should()
                .BeOfType<BadRequestObjectResult>()
                .Subject.Value.Should().BeOfType<DbUpdateException>();
        }
    }
}