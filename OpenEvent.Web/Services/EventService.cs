using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OpenEvent.Web.Contexts;
using OpenEvent.Web.Exceptions;
using OpenEvent.Web.Helpers;
using OpenEvent.Web.Models.Address;
using OpenEvent.Web.Models.Analytic;
using OpenEvent.Web.Models.Category;
using OpenEvent.Web.Models.Event;
using OpenEvent.Web.Models.Promo;
using OpenEvent.Web.Models.Ticket;
using OpenEvent.Web.Models.Transaction;

namespace OpenEvent.Web.Services
{
    public interface IEventService
    {
        // Host methods
        Task<EventViewModel> Create(CreateEventBody createEventBody);
        Task Cancel(Guid id);
        Task<List<EventHostModel>> GetAllHosts(Guid hostId);

        //public methods
        Task<EventDetailModel> GetForPublic(Guid id, Guid? userId);
        Task<List<EventViewModel>> Search(string keyword, List<SearchFilter> filters, Guid? userId);

        Task<List<Category>> GetAllCategories();
        Task<ActionResult<EventHostModel>> GetForHost(Guid id);
        Task Update(UpdateEventBody updateEventBody);
        Task<List<EventViewModel>> GetRecommended(Guid id);
        Task<EventAnalytics> GetAnalytics(Guid id);
    }

    /// <summary>
    /// Service providing all event logic.
    /// </summary>
    public class EventService : IEventService
    {
        private readonly ILogger<EventService> Logger;
        private readonly ApplicationContext ApplicationContext;
        private readonly IMapper Mapper;
        private readonly HttpClient HttpClient;
        private readonly AppSettings AppSettings;
        private readonly IAnalyticsService AnalyticsService;
        private readonly IRecommendationService RecommendationService;
        private readonly IDistributedCache DistributedCache;
        private readonly IWorkQueue WorkQueue;
        private readonly IPopularityService PopularityService;

        public EventService(ApplicationContext context, ILogger<EventService> logger, IMapper mapper,
            HttpClient httpClient, IOptions<AppSettings> appSettings, IAnalyticsService analyticsService,
            IRecommendationService recommendationService, IDistributedCache distributedCache,
            IWorkQueue workQueue, IPopularityService popularityService)
        {
            Logger = logger;
            ApplicationContext = context;
            Mapper = mapper;
            HttpClient = httpClient;
            AppSettings = appSettings.Value;
            AnalyticsService = analyticsService;
            RecommendationService = recommendationService;
            DistributedCache = distributedCache;
            WorkQueue = workQueue;
            PopularityService = popularityService;
        }

        /// <summary>
        /// Creates a new event.
        /// Gets event location from azure maps API.
        /// </summary>
        /// <param name="createEventBody"></param>
        /// <returns></returns>
        /// <exception cref="UserNotFoundException"></exception>
        public async Task<EventViewModel> Create(CreateEventBody createEventBody)
        {
            var host = await ApplicationContext.Users.AsSplitQuery().Include(x => x.HostedEvents)
                .FirstOrDefaultAsync(x => x.Id == createEventBody.HostId);

            if (host == null)
            {
                Logger.LogInformation("User not found");
                throw new UserNotFoundException();
            }

            var cords = await GetAddressCords(createEventBody.Address);
            createEventBody.Address.Lat = cords.Lat;
            createEventBody.Address.Lon = cords.Lon;

            Event newEvent = new Event()
            {
                Name = createEventBody.Name,
                Description = createEventBody.Description,
                Address = createEventBody.Address,
                isCanceled = false,
                Price = createEventBody.Price,
                IsOnline = createEventBody.IsOnline,
                EndLocal = createEventBody.EndLocal,
                EndUTC = createEventBody.EndLocal.ToUniversalTime(),
                StartLocal = createEventBody.StartLocal,
                StartUTC = createEventBody.StartLocal.ToUniversalTime(),
                Images = createEventBody.Images.Select(x => Mapper.Map<Image>(x)).ToList(),
                Thumbnail = createEventBody.Thumbnail != null
                    ? Mapper.Map<Image>(createEventBody.Thumbnail)
                    : new Image(),
                SocialLinks = createEventBody.SocialLinks != null
                    ? createEventBody.SocialLinks.Select(x => Mapper.Map<SocialLink>(x)).ToList()
                    : new List<SocialLink>(),
                TicketsLeft = createEventBody.NumberOfTickets,
                Created = new DateTime(),
                Finished = false
            };

            List<Ticket> tickets = new List<Ticket>();
            for (int i = 0; i < createEventBody.NumberOfTickets; i++)
            {
                tickets.Add(new Ticket()
                {
                    Event = newEvent,
                    Available = true,
                    Uses = 0
                });
            }

            host.HostedEvents.Add(newEvent);

            try
            {
                // Saving user to Db.
                await ApplicationContext.Events.AddAsync(newEvent);
                await ApplicationContext.Tickets.AddRangeAsync(tickets);
                await ApplicationContext.SaveChangesAsync();

                if (createEventBody.Categories != null)
                {
                    List<EventCategory> eventCategories = createEventBody.Categories
                        .Select(c => new EventCategory() {CategoryId = c.Id, EventId = newEvent.Id}).ToList();
                    await ApplicationContext.AddRangeAsync(eventCategories);
                    await ApplicationContext.SaveChangesAsync();
                }

                return Mapper.Map<EventViewModel>(newEvent);
            }
            catch
            {
                Logger.LogWarning("User failed to save");
                throw;
            }
        }

        private async Task<CoordinateAbbreviated> GetAddressCords(Address address)
        {
            var addressAsString = $"{address.AddressLine1},{address.City},{address.CountryName},{address.PostalCode}";
            Uri uri = new Uri(
                $"https://atlas.microsoft.com/search/address/json?&subscription-key={AppSettings.AzureMapsKey}&api-version=1.0&language=en-US&query=${addressAsString}");

            var response = await HttpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentType?.MediaType == "application/json")
            {
                var contentStream = await response.Content.ReadAsStreamAsync();

                using var streamReader = new StreamReader(contentStream);
                using var jsonReader = new JsonTextReader(streamReader);

                JsonSerializer serializer = new JsonSerializer();

                try
                {
                    var searchResponse = serializer.Deserialize<SearchAddressResponse>(jsonReader);
                    if (searchResponse == null)
                    {
                        throw new AddressNotFoundException();
                    }

                    return searchResponse.Results.First().Position;
                }
                catch (JsonReaderException)
                {
                    throw new JsonException();
                }
            }

            throw new AddressNotFoundException();
        }

        /// <summary>
        /// Sets IsCanceled true.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="EventNotFoundException"></exception>
        public async Task Cancel(Guid id)
        {
            var e = await GetEvent(id);

            if (e == null)
            {
                Logger.LogInformation("Event not found");
                throw new EventNotFoundException();
            }

            e.isCanceled = true;

            try
            {
                await ApplicationContext.SaveChangesAsync();
                Logger.LogInformation("Event canceled");
            }
            catch
            {
                Logger.LogWarning("Event failed to save");
                throw;
            }
        }

        private async Task<Event> GetEvent(Guid id)
        {
            return await ApplicationContext.Events.FirstOrDefaultAsync(x => x.Id == id);
        }

        /// <summary>
        /// Gets all of the users hosted events.
        /// </summary>
        /// <param name="hostId"></param>
        /// <returns></returns>
        public async Task<List<EventHostModel>> GetAllHosts(Guid hostId)
        {
            var events = ApplicationContext.Events
                .Include(x => x.EventCategories).ThenInclude(x => x.Category)
                .Include(x => x.Tickets)
                .Include(x => x.Transactions)
                .Include(x => x.Host)
                .Include(x => x.Promos)
                .AsSplitQuery().AsNoTracking().AsEnumerable();

            events = events.Where(x => x.Host.Id == hostId && !x.isCanceled);

            return events.Select(
                x => new EventHostModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    Address = x.Address,
                    Categories = x.EventCategories.Any()
                        ? x.EventCategories.Select(c => Mapper.Map<CategoryViewModel>(c.Category)).ToList()
                        : new List<CategoryViewModel>(),
                    Price = x.Price,
                    Thumbnail = x.Thumbnail.Source != null ? Mapper.Map<ImageViewModel>(x.Thumbnail) : null,
                    Images = x.Images.Any()
                        ? x.Images.Select(i => Mapper.Map<ImageViewModel>(i)).ToList()
                        : new List<ImageViewModel>(),
                    Tickets = x.Tickets.Any()
                        ? x.Tickets.Select(t => Mapper.Map<TicketViewModel>(t)).ToList()
                        : new List<TicketViewModel>(),
                    EndLocal = x.EndLocal,
                    IsOnline = x.IsOnline,
                    SocialLinks = x.SocialLinks.Any()
                        ? x.SocialLinks.Select(s => Mapper.Map<SocialLinkViewModel>(s)).ToList()
                        : new List<SocialLinkViewModel>(),
                    StartLocal = x.StartLocal,
                    TicketsLeft = x.Tickets.Count(ticket => ticket.Available),
                    EndUTC = x.EndUTC,
                    StartUTC = x.StartUTC,
                    Transactions = x.Transactions.Any()
                        ? x.Transactions.Select(transaction => Mapper.Map<TransactionViewModel>(transaction)).ToList()
                        : new List<TransactionViewModel>(),
                    Promos = x.Promos.Any()
                        ? x.Promos.Select(promo => new PromoViewModel()
                        {
                            Active = promo.Active,
                            Discount = promo.Discount,
                            End = promo.End,
                            Id = promo.Id,
                            Start = promo.Start,
                            NumberOfSales = x.Transactions.Count(x => x.PromoId == promo.Id)
                        }).ToList()
                        : new List<PromoViewModel>(),
                    Created = x.Created,
                    Finished = x.Finished
                }).ToList();
        }

        /// <summary>
        /// Gets all public facing data for an event.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        /// <exception cref="EventNotFoundException"></exception>
        public async Task<EventDetailModel> GetForPublic(Guid id, Guid? userId)
        {
            var e = await CacheHelpers.Get<Event>(DistributedCache, id.ToString(), "PublicEvent");

            if (e == null)
            {
                e = await ApplicationContext.Events
                    .Include(x => x.Promos)
                    .Include(x => x.Address)
                    .Include(x => x.EventCategories).ThenInclude(x => x.Category)
                    .Include(x => x.Images)
                    .Include(x => x.Thumbnail)
                    .Include(x => x.SocialLinks).AsSplitQuery().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

                if (e == null)
                {
                    Logger.LogInformation("Event: {ID} not fond", id);
                    throw new EventNotFoundException();
                }

                Logger.LogInformation("Caching event: {ID}", id);

                await CacheHelpers.Set<Event>(DistributedCache, id.ToString(), "PublicEvent", e);
            }
            else
            {
                Logger.LogInformation("Found event: {ID} in cache", id);
            }


            // var t = (DateTime.Now - e.Created);
            // Logger.LogInformation("creating page views for {Days} days for {UserId}", t.TotalDays, userId);
            // for (int i = 0; i < t.Days; i++)
            // {
            //     for (int j = 0; j < i; j++)
            //     {
            //         var date = DateTime.Now.AddDays(-i);
            //         WorkQueue.QueueWork(token => AnalyticsService.CapturePageViewAsync(token, e.Id, userId, date));
            //     }
            // }

            WorkQueue.QueueWork(token => AnalyticsService.CapturePageViewAsync(token, e.Id, userId, DateTime.Now));
            if (userId != null)
                WorkQueue.QueueWork(token =>
                    RecommendationService.InfluenceAsync(token, (Guid) userId, e.Id, Influence.PageView, DateTime.Now));
            WorkQueue.QueueWork(token => PopularityService.PopulariseEvent(token, e.Id, DateTime.Now));

            return new EventDetailModel
            {
                Id = e.Id,
                Address = e.Address,
                Name = e.Name,
                Description = e.Description,
                Categories = e.EventCategories.Select(c => Mapper.Map<CategoryViewModel>(c.Category)).ToList(),
                Images = e.Images.Select(i => Mapper.Map<ImageViewModel>(i)).ToList(),
                Price = e.Price,
                Thumbnail = Mapper.Map<ImageViewModel>(e.Thumbnail),
                EndLocal = e.EndLocal,
                IsOnline = e.IsOnline,
                SocialLinks = e.SocialLinks.Select(s => Mapper.Map<SocialLinkViewModel>(s)).ToList(),
                StartLocal = e.StartLocal,
                TicketsLeft = e.TicketsLeft,
                EndUTC = e.EndUTC,
                StartUTC = e.StartUTC,
                Promos = e.Promos.Where(x => x.Active && x.Start < DateTime.Now && DateTime.Now < x.End)
                    .Select(promo => Mapper.Map<PromoViewModel>(promo)).ToList(),
                Finished = e.Finished
            };
        }

        /// <summary>
        /// Searches events.
        /// </summary>
        /// <param name="keyword"></param>
        /// <param name="filters"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<List<EventViewModel>> Search(string keyword, List<SearchFilter> filters, Guid? userId)
        {
            List<Guid> searchCategories = filters.Where(x => x.Key == SearchParam.Category)
                .Select(x => Guid.Parse(x.Value)).ToList();

            var isOnline = filters.Find(x => x.Key == SearchParam.IsOnline) != null;

            var cords = filters.Find(x => x.Key == SearchParam.Location)?.Value.Split(",").Select(double.Parse)
                .ToArray();

            var date = filters.Find(x => x.Key == SearchParam.Date)?.Value;

            // TODO: this search is not explicit does it want to be?
            // TODO: order of the result?

            IEnumerable<Event> events;

            String searchKey = keyword + String.Concat(filters.Select(x => x.Key + x.Value.ToString()));

            events = await CacheHelpers.Get<List<Event>>(DistributedCache, searchKey, "Search");

            if (events == null)
            {
                events = ApplicationContext.Events
                    .Include(x => x.EventCategories)
                    .Include(x => x.Promos)
                    .AsSplitQuery().AsEnumerable();

                if (!string.IsNullOrEmpty(keyword))
                {
                    events = events.Where(x => x.Name.Contains(keyword!));
                }

                if (isOnline) events = events.Where(x => x.IsOnline);

                if (searchCategories.Any())
                    events = events.Where(x => x.EventCategories.Any(c => searchCategories.Contains(c.CategoryId)));

                if (cords != null)
                    events = events.Where(x => !x.IsOnline && EventDistance(x.Address.Lat, x.Address.Lon, cords));

                if (date != null)
                {
                    var realDate = DateTime.Parse(date);
                    events = events.Where(x =>
                        new DateTime(x.StartLocal.Year, x.StartLocal.Month, x.StartLocal.Day).Equals(new DateTime(
                            realDate.Year,
                            realDate.Month, realDate.Day)));
                }

                events = events.Take(50);

                Logger.LogInformation("Caching search");

                await CacheHelpers.Set<List<Event>>(DistributedCache, searchKey, "Search", events.ToList());
            }
            else
            {
                Logger.LogInformation("Found search in cache");
            }

            WorkQueue.QueueWork(token =>
                AnalyticsService.CaptureSearchAsync(token, keyword, String.Join(",", filters), userId, DateTime.Now));
            if (userId != null)
                WorkQueue.QueueWork(token =>
                    RecommendationService.InfluenceAsync(token, (Guid) userId, keyword, filters, DateTime.Now));

            return events.Select(e => Mapper.Map<EventViewModel>(e)).ToList();
        }

        private static bool EventDistance(double lat, double lon, double[] cords)
        {
            return CalculateDistance.Calculate(new Location()
            {
                Latitude = lat,
                Longitude = lon
            }, new Location()
            {
                Latitude = cords[0],
                Longitude = cords[1]
            }) < cords[2];
        }

        /// <summary>
        /// Gets all event categories.
        /// </summary>
        /// <returns></returns>
        public Task<List<Category>> GetAllCategories()
        {
            return ApplicationContext.Categories.ToListAsync();
        }

        /// <summary>
        /// Gets all data for event.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="EventNotFoundException"></exception>
        public async Task<ActionResult<EventHostModel>> GetForHost(Guid id)
        {
            var e = await ApplicationContext.Events
                .Include(x => x.EventCategories).ThenInclude(x => x.Category)
                .Include(x => x.Transactions)
                .Include(x => x.Tickets)
                .Include(x => x.Promos)
                .AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id);

            if (e == null)
            {
                Logger.LogInformation("Event not fond");
                throw new EventNotFoundException();
            }

            return new EventHostModel
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                Address = e.Address,
                Price = e.Price,
                EndLocal = e.EndLocal,
                IsOnline = e.IsOnline,
                EndUTC = e.EndUTC,
                StartUTC = e.StartUTC,
                Created = e.Created,
                StartLocal = e.StartLocal,
                TicketsLeft = e.Tickets.Count(ticket => ticket.Available),
                Categories = e.EventCategories.Any()
                    ? e.EventCategories.Select(c => Mapper.Map<CategoryViewModel>(c.Category)).ToList()
                    : new List<CategoryViewModel>(),
                Thumbnail = e.Thumbnail.Source != null ? Mapper.Map<ImageViewModel>(e.Thumbnail) : null,
                Images = e.Images.Any()
                    ? e.Images.Select(i => Mapper.Map<ImageViewModel>(i)).ToList()
                    : new List<ImageViewModel>(),
                Tickets = e.Tickets.Any()
                    ? e.Tickets.Select(t => Mapper.Map<TicketViewModel>(t)).ToList()
                    : new List<TicketViewModel>(),
                SocialLinks = e.SocialLinks.Any()
                    ? e.SocialLinks.Select(s => Mapper.Map<SocialLinkViewModel>(s)).ToList()
                    : new List<SocialLinkViewModel>(),
                Transactions = e.Transactions.Any()
                    ? e.Transactions.Select(transaction => Mapper.Map<TransactionViewModel>(transaction))
                        .ToList()
                    : new List<TransactionViewModel>(),
                Promos = e.Promos.Any()
                    ? e.Promos.Select(promo => new PromoViewModel()
                    {
                        Active = promo.Active,
                        Discount = promo.Discount,
                        End = promo.End,
                        Id = promo.Id,
                        Start = promo.Start,
                        NumberOfSales = e.Transactions.Count(x => x.PromoId == promo.Id)
                    }).ToList()
                    : new List<PromoViewModel>(),
                Finished = e.Finished
            };
            ;
        }

        /// <summary>
        /// Updates the event.
        /// </summary>
        /// <param name="updateEventBody"></param>
        /// <returns></returns>
        /// <exception cref="EventNotFoundException"></exception>
        public async Task Update(UpdateEventBody updateEventBody)
        {
            var e = await ApplicationContext.Events.Include(x => x.EventCategories)
                .FirstOrDefaultAsync(x => x.Id == updateEventBody.Id);

            if (e == null)
            {
                Logger.LogInformation("Event not fond");
                throw new EventNotFoundException();
            }

            e.Name = updateEventBody.Name;
            e.Description = updateEventBody.Description;
            e.Price = updateEventBody.Price;
            e.IsOnline = updateEventBody.IsOnline;
            e.Images = updateEventBody.SocialLinks != null
                ? updateEventBody.Images.Select(x => Mapper.Map<Image>(x)).ToList()
                : null;
            e.Thumbnail = updateEventBody.Thumbnail != null
                ? Mapper.Map<Image>(updateEventBody.Thumbnail)
                : new Image();
            e.SocialLinks = updateEventBody.SocialLinks != null
                ? updateEventBody.SocialLinks.Select(x => Mapper.Map<SocialLink>(x)).ToList()
                : new List<SocialLink>();
            e.StartLocal = updateEventBody.StartLocal;
            e.EndLocal = updateEventBody.EndLocal;
            e.StartUTC = updateEventBody.StartLocal.ToUniversalTime();
            e.EndUTC = updateEventBody.EndLocal.ToUniversalTime();
            e.Address = updateEventBody.Address;
            e.EventCategories = updateEventBody.Categories != null
                ? updateEventBody.Categories.Select(c => new EventCategory() {CategoryId = c.Id}).ToList()
                : new List<EventCategory>();
            e.Finished = updateEventBody.Finished;

            try
            {
                await ApplicationContext.SaveChangesAsync();
                Logger.LogInformation("Event updated");
            }
            catch
            {
                Logger.LogWarning("User failed to save");
                throw;
            }
        }

        public async Task<List<EventViewModel>> GetRecommended(Guid id)
        {
            var recommendationScores = await ApplicationContext.RecommendationScores.AsNoTracking().AsSplitQuery()
                .Include(x => x.Category).Where(x => x.User.Id == id).ToListAsync();

            if (recommendationScores == null)
            {
                Logger.LogInformation("The user has no recommendation scores");
                return await Search("", new List<SearchFilter>(), id);
            }

            Dictionary<string, double> recommendationDictionary =
                recommendationScores.ToDictionary(x => x.Category.Name, x => x.Weight);

            var averageScore = recommendationScores.Select(x => x.Weight).Average();

            var recommendedEvents = ApplicationContext.Events.AsSplitQuery().Include(x => x.EventCategories)
                .ThenInclude(x => x.Category).AsEnumerable();

            recommendedEvents = recommendedEvents.Where(x => ShouldRecommend(x, recommendationDictionary, averageScore))
                .ToList();

            return recommendedEvents.Select(e => Mapper.Map<EventViewModel>(e)).ToList();
            ;
        }

        public async Task<EventAnalytics> GetAnalytics(Guid id)
        {
            // var e = await ApplicationContext.Events.AsNoTracking().AsSplitQuery().Include(x => x.EventCategories).FirstOrDefaultAsync(x => x.Id == id);

            var pageViewEvents = await ApplicationContext.PageViewEvents
                .Include(x => x.User)
                .AsNoTracking().AsSplitQuery()
                .Where(x => x.Event.Id == id).ToListAsync();
            var ticketVerificationEvents = await ApplicationContext.VerificationEvents.AsNoTracking().AsSplitQuery()
                .Where(x => x.Event.Id == id).ToListAsync();
            // var recommendationScores = await ApplicationContext.RecommendationScores
            // .Where(x => e.EventCategories.Any(c => c.CategoryId == x.Category.Id)).ToListAsync();

            var demographics = pageViewEvents.Where(x => x.User != null).GroupBy(x => x.User.DateOfBirth).Select(x =>
                new Demographic()
                {
                    Age = (DateTime.Now - x.Key).Days / 365,
                    // Age = new DateTime(DateTime.Now.Subtract(x.Key).Ticks).Year,
                    Count = x.Count()
                }).ToList();

            return new EventAnalytics()
            {
                PageViewEvents = pageViewEvents.Select(x => Mapper.Map<PageViewEventViewModel>(x))
                    .OrderByDescending(x => x.Created).ToList(),
                TicketVerificationEvents = ticketVerificationEvents
                    .Select(x => Mapper.Map<TicketVerificationEventViewModel>(x)).OrderByDescending(x => x.Created)
                    .ToList(),
                Demographics = demographics
            };
        }

        private bool ShouldRecommend(Event e, Dictionary<string, double> scores, double averageScore)
        {
            List<double> eScores = new List<double>();
            if (e.EventCategories.Any())
            {
                e.EventCategories.ForEach(categoryEvent => { eScores.Add(scores[categoryEvent.Category.Name]); });
                if (eScores.Average() >= averageScore) return true;
            }

            return false;
        }
    }
}