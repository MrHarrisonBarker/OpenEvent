using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenEvent.Data.Models.Recommendation;
using OpenEvent.Data.Models.Ticket;
using OpenEvent.Web.Contexts;
using OpenEvent.Web.Exceptions;
using QRCoder;

namespace OpenEvent.Web.Services
{
    /// <inheritdoc />
    public class TicketService : ITicketService
    {
        private readonly ILogger<TicketService> Logger;
        private readonly ApplicationContext ApplicationContext;
        private readonly IMapper Mapper;
        private readonly IAnalyticsService AnalyticsService;
        private readonly IRecommendationService RecommendationService;
        private readonly IWorkQueue WorkQueue;
        
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="context"></param>
        /// <param name="logger"></param>
        /// <param name="mapper"></param>
        /// <param name="analyticsService"></param>
        /// <param name="recommendationService"></param>
        /// <param name="workQueue"></param>
        public TicketService(ApplicationContext context, ILogger<TicketService> logger, IMapper mapper,
            IAnalyticsService analyticsService, IRecommendationService recommendationService, IWorkQueue workQueue)
        {
            Logger = logger;
            Mapper = mapper;
            ApplicationContext = context;
            AnalyticsService = analyticsService;
            WorkQueue = workQueue;
            RecommendationService = recommendationService;
        }

        /// <inheritdoc />
        /// <exception cref="UserNotFoundException">Thrown if the user is not found</exception>
        public async Task<List<TicketViewModel>> GetAllUsersTickets(Guid id)
        {
            var user = await ApplicationContext.Users
                .Include(x => x.Tickets).ThenInclude(x => x.Transaction)
                .Include(x => x.Tickets).ThenInclude(x => x.Event)
                .AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id);

            if (user == null) throw new UserNotFoundException();

            return user.Tickets.Where(x => x.Transaction !=null && x.Transaction.Paid).Select(x => Mapper.Map<TicketViewModel>(x)).ToList();
        }

        /// <inheritdoc />
        /// <exception cref="UnauthorizedAccessException">Thrown if the user doesn't own the ticket</exception>
        public async Task VerifyTicket(TicketVerifyBody ticketVerifyBody)
        {
            var ticket = await ApplicationContext.Tickets
                .Include(x => x.Event)
                .FirstOrDefaultAsync(x => x.Id == ticketVerifyBody.Id && x.Event.Id == ticketVerifyBody.EventId);

            if (ticket == null)
            {
                throw new UnauthorizedAccessException();
            }

            ticket.Uses++;

            try
            {
                WorkQueue.QueueWork(token =>
                    AnalyticsService.CaptureTicketVerifyAsync(token, ticket.Id, ticketVerifyBody.EventId,
                        DateTime.Now));

                WorkQueue.QueueWork(token =>
                    RecommendationService.InfluenceAsync(token, ticket.User.Id, ticket.Event.Id, Influence.Verify,
                        DateTime.Now));

                await ApplicationContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Logger.LogInformation(e.ToString());
                throw;
            }
        }

        /// <inheritdoc />
        /// <exception cref="TicketNotFoundException">Thrown if ticket is not found</exception>
        public async Task<TicketDetailModel> Get(Guid id)
        {
            var ticket = await ApplicationContext.Tickets
                .Include(x => x.Event)
                .Include(x => x.Transaction)
                .AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id);

            if (ticket == null)
            {
                Logger.LogInformation("Ticket not found");
                throw new TicketNotFoundException();
            }

            // if the ticket doesn't have a qr code then generate one
            if (ticket.QRCode == null)
            {
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(ticket.Id.ToString(), QRCodeGenerator.ECCLevel.Q);
                QRCode qrCode = new QRCode(qrCodeData);
                Bitmap qrCodeImage = qrCode.GetGraphic(20, Color.Black, Color.White, false);
                ticket.QRCode =
                    Encoding.UTF8.GetBytes(
                        $"data:image/png;base64,{Convert.ToBase64String(BitmapToBytes(qrCodeImage))}");
                await ApplicationContext.SaveChangesAsync();
            }

            return Mapper.Map<TicketDetailModel>(ticket);
        }

        // converts a bitmap image into a stream of bytes
        private static byte[] BitmapToBytes(Bitmap img)
        {
            using MemoryStream stream = new MemoryStream();
            img.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
    }
}