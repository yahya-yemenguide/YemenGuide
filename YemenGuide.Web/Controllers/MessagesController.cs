using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YemenGuide.Core.DTOs.Customer;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.Constants;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Hubs;
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Controllers
{
    [Authorize(Roles = "Customer")]
    public class MessagesController : Controller
    {
        private const int MaximumMessageLength = 4000;
        private const int MaximumQuotedTextLength = 300;
        private const int MaximumFilesPerMessage = 10;
        private const long MaximumFileSizeBytes = 10L * 1024L * 1024L;
        private const long MaximumRequestFilesSizeBytes = 25L * 1024L * 1024L;

        private static readonly HashSet<string> AllowedFileExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".gif", ".webp",
                ".mp4", ".webm", ".mov",
                ".pdf", ".doc", ".docx", ".xls", ".xlsx",
                ".zip", ".rar"
            };

        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly INotificationService _notificationService;

        public MessagesController(
            AppDbContext context,
            IWebHostEnvironment environment,
            IHubContext<ChatHub> hubContext,
            INotificationService notificationService)
        {
            _context = context;
            _environment = environment;
            _hubContext = hubContext;
            _notificationService = notificationService;
        }

        // =====================================================================
        // صفحة محادثات العميل
        // =====================================================================

        [HttpGet]
        public async Task<IActionResult> MyMessages(
            int? placeId,
            int? withOwnerId,
            CancellationToken cancellationToken)
        {
            if (!TryGetCustomerId(out int customerId))
                return Unauthorized();

            List<CustomerChatContactDto> contacts = await BuildContactsAsync(
                customerId,
                placeId,
                withOwnerId,
                cancellationToken);

            int? requestedOwnerId = withOwnerId;

            if (placeId.HasValue && !requestedOwnerId.HasValue)
            {
                requestedOwnerId = contacts
                    .FirstOrDefault(contact => contact.PlaceId == placeId.Value)
                    ?.OwnerId;
            }

            CustomerChatPageModel pageModel = new CustomerChatPageModel
            {
                ChatContacts = contacts,
                RequestedPlaceId = placeId,
                RequestedOwnerId = requestedOwnerId
            };

            ViewBag.OpenPlaceId = placeId ?? 0;
            ViewBag.OpenOwnerId = requestedOwnerId ?? 0;
            ViewBag.TotalUnreadConversations = pageModel.TotalUnreadConversations;

            return View(
                "~/Views/Panel/Messages/MyMessages.cshtml",
                pageModel);
        }

        private async Task<List<CustomerChatContactDto>> BuildContactsAsync(
            int customerId,
            int? requestedPlaceId,
            int? requestedOwnerId,
            CancellationToken cancellationToken)
        {
            var bookingRows = await _context.Bookings
                .AsNoTracking()
                .Where(booking => booking.CustomerId == customerId)
                .Select(booking => new
                {
                    booking.BookingId,
                    booking.PlaceId,
                    booking.BookingState,
                    booking.StartDate,
                    booking.EndDate,
                    booking.CreatedAt
                })
                .ToListAsync(cancellationToken);

            List<Chat> visibleChats = await _context.Chats
                .AsNoTracking()
                .Where(chat =>
                    !chat.IsReportMessage &&
                    chat.ReportId == null &&
                    (
                        (chat.SenderId == customerId && !chat.IsDeletedForSender) ||
                        (chat.ReceiverId == customerId && !chat.IsDeletedForReceiver)
                    ))
                .OrderByDescending(chat => chat.SendTime)
                .ThenByDescending(chat => chat.MessageId)
                .ToListAsync(cancellationToken);

            HashSet<int> relatedOwnerIds = visibleChats
                .Select(chat => chat.SenderId == customerId
                    ? chat.ReceiverId
                    : chat.SenderId)
                .Where(userId => userId > 0 && userId != customerId)
                .ToHashSet();

            HashSet<int> relevantPlaceIds = bookingRows
                .Select(booking => booking.PlaceId)
                .ToHashSet();

            foreach (Chat chat in visibleChats)
            {
                if (chat.PlaceId.HasValue)
                    relevantPlaceIds.Add(chat.PlaceId.Value);
            }

            if (requestedPlaceId.HasValue && requestedPlaceId.Value > 0)
                relevantPlaceIds.Add(requestedPlaceId.Value);

            if (requestedOwnerId.HasValue && requestedOwnerId.Value > 0)
                relatedOwnerIds.Add(requestedOwnerId.Value);

            List<Place> directlyRelevantPlaces = relevantPlaceIds.Count == 0
                ? new List<Place>()
                : await _context.Places
                    .AsNoTracking()
                    .Include(place => place.Owner)
                    .Include(place => place.PlacePolicy)
                    .Where(place => relevantPlaceIds.Contains(place.PlaceId))
                    .ToListAsync(cancellationToken);

            foreach (Place place in directlyRelevantPlaces)
            {
                if (place.OwnerId.HasValue)
                    relatedOwnerIds.Add(place.OwnerId.Value);
            }

            // نحتاج منشآت الملاك المرتبطين لتحديد الرسائل القديمة التي لا تحمل PlaceId.
            List<Place> ownerPlaces = relatedOwnerIds.Count == 0
                ? new List<Place>()
                : await _context.Places
                    .AsNoTracking()
                    .Include(place => place.Owner)
                    .Include(place => place.PlacePolicy)
                    .Where(place =>
                        place.OwnerId.HasValue &&
                        relatedOwnerIds.Contains(place.OwnerId.Value))
                    .ToListAsync(cancellationToken);

            Dictionary<int, Place> placesById = ownerPlaces
                .Concat(directlyRelevantPlaces)
                .GroupBy(place => place.PlaceId)
                .ToDictionary(group => group.Key, group => group.First());

            Dictionary<int, HashSet<int>> relationshipPlacesByOwner =
                new Dictionary<int, HashSet<int>>();

            foreach (var booking in bookingRows)
            {
                if (placesById.TryGetValue(booking.PlaceId, out Place? place) &&
                    place.OwnerId.HasValue)
                {
                    AddOwnerPlaceLink(
                        relationshipPlacesByOwner,
                        place.OwnerId.Value,
                        booking.PlaceId);
                }
            }

            foreach (Chat chat in visibleChats.Where(chat => chat.PlaceId.HasValue))
            {
                if (!placesById.TryGetValue(chat.PlaceId!.Value, out Place? place) ||
                    !place.OwnerId.HasValue)
                {
                    continue;
                }

                int counterpartId = chat.SenderId == customerId
                    ? chat.ReceiverId
                    : chat.SenderId;

                if (counterpartId != place.OwnerId.Value)
                    continue;

                AddOwnerPlaceLink(
                    relationshipPlacesByOwner,
                    counterpartId,
                    place.PlaceId);
            }

            Dictionary<(int OwnerId, int PlaceId), List<Chat>> chatsByConversation =
                new Dictionary<(int OwnerId, int PlaceId), List<Chat>>();

            foreach (Chat chat in visibleChats)
            {
                int ownerId = chat.SenderId == customerId
                    ? chat.ReceiverId
                    : chat.SenderId;

                int? resolvedPlaceId = null;

                if (chat.PlaceId.HasValue &&
                    placesById.TryGetValue(chat.PlaceId.Value, out Place? chatPlace) &&
                    chatPlace.OwnerId == ownerId)
                {
                    resolvedPlaceId = chat.PlaceId.Value;
                }
                else if (!chat.PlaceId.HasValue)
                {
                    resolvedPlaceId = ResolveLegacyPlaceId(
                        ownerId,
                        relationshipPlacesByOwner,
                        ownerPlaces);
                }

                if (!resolvedPlaceId.HasValue)
                    continue;

                var key = (ownerId, resolvedPlaceId.Value);

                if (!chatsByConversation.TryGetValue(key, out List<Chat>? conversationChats))
                {
                    conversationChats = new List<Chat>();
                    chatsByConversation[key] = conversationChats;
                }

                conversationChats.Add(chat);
            }

            HashSet<(int OwnerId, int PlaceId)> contactKeys =
                chatsByConversation.Keys.ToHashSet();

            foreach (var booking in bookingRows)
            {
                if (placesById.TryGetValue(booking.PlaceId, out Place? place) &&
                    place.OwnerId.HasValue)
                {
                    contactKeys.Add((place.OwnerId.Value, booking.PlaceId));
                }
            }

            if (requestedPlaceId.HasValue &&
                placesById.TryGetValue(requestedPlaceId.Value, out Place? requestedPlace) &&
                requestedPlace.OwnerId.HasValue &&
                (!requestedOwnerId.HasValue || requestedOwnerId.Value == requestedPlace.OwnerId.Value))
            {
                contactKeys.Add((requestedPlace.OwnerId.Value, requestedPlace.PlaceId));
            }

            List<CustomerChatContactDto> contacts =
                new List<CustomerChatContactDto>();

            foreach (var key in contactKeys)
            {
                if (!placesById.TryGetValue(key.PlaceId, out Place? place) ||
                    place.OwnerId != key.OwnerId ||
                    place.Owner == null ||
                    !string.Equals(place.Owner.Role, "Owner", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                chatsByConversation.TryGetValue(key, out List<Chat>? conversationChats);
                conversationChats ??= new List<Chat>();

                Chat? lastMessage = conversationChats
                    .OrderByDescending(chat => chat.SendTime ?? DateTime.MinValue)
                    .ThenByDescending(chat => chat.MessageId)
                    .FirstOrDefault();

                var latestBooking = bookingRows
                    .Where(booking => booking.PlaceId == key.PlaceId)
                    .OrderByDescending(booking => booking.CreatedAt ?? DateTime.MinValue)
                    .ThenByDescending(booking => booking.BookingId)
                    .FirstOrDefault();

                int unreadCount = conversationChats.Count(chat =>
                    chat.SenderId == key.OwnerId &&
                    chat.ReceiverId == customerId &&
                    !chat.IsRead &&
                    !chat.IsDeletedForReceiver);

                bool hasConversation = lastMessage != null;
                bool hasBooking = latestBooking != null;
                bool chatEnabled = place.PlacePolicy?.IsChatEnabled != false;
                bool ownerAccountActive = !IsSystemAccountUnavailable(place.Owner.AccountState);
                bool canStartPublicConversation =
                    IsPublicPlaceAvailableForNewConversation(place) &&
                    chatEnabled &&
                    ownerAccountActive;

                string ownerName = GetDisplayName(place.Owner, "مالك");

                contacts.Add(new CustomerChatContactDto
                {
                    OwnerId = key.OwnerId,
                    OwnerName = ownerName,
                    ProfileImage = place.Owner.ProfileImage,
                    InitialLetter = GetInitial(ownerName, "م"),
                    PlaceId = place.PlaceId,
                    PlaceName = place.PlaceNameAndDesc,
                    PlaceImage = FirstNonEmpty(place.LogoImage, place.CoverImage),
                    LastMessage = hasConversation
                        ? GetMessagePreview(lastMessage!)
                        : hasBooking
                            ? "ابدأ محادثة بخصوص حجزك"
                            : "ابدأ استفسارك مع المنشأة",
                    LastMessageTime = lastMessage?.SendTime,
                    UnreadCount = unreadCount,
                    HasPreviousConversation = hasConversation,
                    HasBooking = hasBooking,
                    CanStartConversation =
                        ownerAccountActive &&
                        (hasConversation || hasBooking || canStartPublicConversation),
                    IsChatEnabled = chatEnabled,
                    LatestBookingId = latestBooking?.BookingId,
                    LatestBookingState = latestBooking?.BookingState,
                    LatestBookingStartDate = latestBooking?.StartDate,
                    LatestBookingEndDate = latestBooking?.EndDate,
                    IsOnline = place.Owner.IsOnline,
                    LastSeen = place.Owner.LastSeen,
                    OwnerAccountState = string.IsNullOrWhiteSpace(place.Owner.AccountState)
                        ? "Active"
                        : place.Owner.AccountState,
                    IsBlockedByOwner = false
                });
            }

            return contacts
                .OrderByDescending(contact =>
                    requestedPlaceId.HasValue &&
                    contact.PlaceId == requestedPlaceId.Value &&
                    (!requestedOwnerId.HasValue || contact.OwnerId == requestedOwnerId.Value))
                .ThenByDescending(contact => contact.UnreadCount > 0)
                .ThenByDescending(contact => contact.LastMessageTime ?? DateTime.MinValue)
                .ThenByDescending(contact => contact.LatestBookingId ?? 0)
                .ThenBy(contact => contact.PlaceName)
                .ThenBy(contact => contact.OwnerName)
                .ToList();
        }

        // =====================================================================
        // قراءة رسائل محادثة منشأة محددة
        // =====================================================================

        [HttpGet]
        public async Task<IActionResult> GetChatMessages(
            int ownerId,
            int? placeId,
            CancellationToken cancellationToken)
        {
            if (!TryGetCustomerId(out int customerId))
                return JsonFailure("انتهت الجلسة، يرجى تسجيل الدخول مجددًا.");

            ConversationContext? conversation = await ResolveConversationAsync(
                customerId,
                ownerId,
                placeId,
                requireSendingPermission: false,
                cancellationToken);

            if (conversation == null)
                return JsonFailure("تعذر تحديد محادثة المنشأة المطلوبة.");

            bool isWithinWorkingHours =
                IsWithinWorkingHours(conversation.Policy, DateTime.Now);

            // رسالة الترحيب رسالة Chat فعلية من المالك وليست تلميحًا بصريًا.
            // إذا كانت شاشة المحادثة الحالية لا تحتوي أي رسالة ظاهرة للعميل،
            // ننشئ رسالة الترحيب مرة واحدة لتظهر كفقاعة مرسلة من المالك.
            // هذا يشمل حالة أن العميل مسح المحادثة من شاشته ثم فتحها من جديد.
            if (conversation.Policy?.IsChatEnabled != false &&
                !string.IsNullOrWhiteSpace(conversation.Policy?.WelcomeMessage))
            {
                bool hasVisibleConversation = await ScopedConversationQuery(
                        customerId,
                        ownerId,
                        conversation.Place.PlaceId,
                        conversation.IncludeLegacyMessages,
                        includeDeletedForCustomer: false)
                    .AnyAsync(cancellationToken);

                if (!hasVisibleConversation)
                {
                    Chat welcomeMessage = new Chat
                    {
                        SenderId = ownerId,
                        ReceiverId = customerId,
                        PlaceId = conversation.Place.PlaceId,
                        MessageText = conversation.Policy!.WelcomeMessage!.Trim(),
                        AttachmentUrl = string.Empty,
                        SendTime = DateTime.Now,
                        IsRead = true,
                        IsDeletedForSender = false,
                        IsDeletedForReceiver = false,
                        IsEdited = false,
                        IsReportMessage = false,
                        ReportId = null
                    };

                    _context.Chats.Add(welcomeMessage);
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            List<Chat> rawMessages = await ScopedConversationQuery(
                    customerId,
                    ownerId,
                    conversation.Place.PlaceId,
                    conversation.IncludeLegacyMessages,
                    includeDeletedForCustomer: false)
                .OrderBy(chat => chat.SendTime)
                .ThenBy(chat => chat.MessageId)
                .ToListAsync(cancellationToken);

            List<Chat> unreadMessages = await ScopedConversationQuery(
                    customerId,
                    ownerId,
                    conversation.Place.PlaceId,
                    conversation.IncludeLegacyMessages,
                    includeDeletedForCustomer: true)
                .Where(chat =>
                    chat.SenderId == ownerId &&
                    chat.ReceiverId == customerId &&
                    !chat.IsRead &&
                    !chat.IsDeletedForReceiver)
                .ToListAsync(cancellationToken);

            if (unreadMessages.Count > 0)
            {
                foreach (Chat unreadMessage in unreadMessages)
                {
                    unreadMessage.IsRead = true;

                    if (!unreadMessage.PlaceId.HasValue &&
                        conversation.IncludeLegacyMessages)
                    {
                        unreadMessage.PlaceId = conversation.Place.PlaceId;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);

                await _hubContext.Clients.User(ownerId.ToString())
                    .SendAsync(
                        "MessagesRead",
                        new
                        {
                            placeId = conversation.Place.PlaceId,
                            readerId = customerId,
                            messageIds = unreadMessages
                                .Select(message => message.MessageId)
                                .ToArray()
                        },
                        cancellationToken);
            }

            var latestBooking = await _context.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.CustomerId == customerId &&
                    booking.PlaceId == conversation.Place.PlaceId)
                .OrderByDescending(booking => booking.CreatedAt)
                .ThenByDescending(booking => booking.BookingId)
                .Select(booking => new
                {
                    booking.BookingId,
                    booking.BookingState,
                    booking.StartDate,
                    booking.EndDate
                })
                .FirstOrDefaultAsync(cancellationToken);

            bool hasConfirmedBooking = await _context.Bookings
                .AsNoTracking()
                .AnyAsync(booking =>
                    booking.CustomerId == customerId &&
                    booking.PlaceId == conversation.Place.PlaceId &&
                    booking.BookingState != "Cancelled" &&
                    booking.BookingState != "Canceled" &&
                    booking.BookingState != "Pending" &&
                    booking.BookingState != "Rejected",
                    cancellationToken);

            List<ChatMessageDto> messages = rawMessages
                .Select(chat => BuildMessageDto(
                    chat,
                    customerId,
                    conversation.Place.PlaceId))
                .ToList();

            return Json(new
            {
                success = true,
                data = messages,
                statusText = BuildPresenceText(conversation.Owner),
                isOnline = conversation.Owner.IsOnline,
                hasBooking = hasConfirmedBooking,
                activePlaceId = conversation.Place.PlaceId,
                activePlaceName = conversation.Place.PlaceNameAndDesc,
                placeImage = FirstNonEmpty(
                    conversation.Place.LogoImage,
                    conversation.Place.CoverImage),
                chatEnabled = conversation.Policy?.IsChatEnabled != false,
                isWithinWorkingHours,
                welcomeMessage = conversation.Policy?.WelcomeMessage ?? string.Empty,
                offlineReplyEnabled = conversation.Policy?.OfflineReplyToggle ?? false,
                offlineMessage = conversation.Policy?.OfflineMessage ?? string.Empty,
                // نعيد الأسئلة الفعلية الجاهزة للعرض بدل الاعتماد على قراءة JSON في المتصفح فقط.
                // إذا لم تُحفظ إعدادات الأسئلة الجديدة بعد نستخدم الاختيارات الافتراضية الظاهرة للمالك.
                quickQuestions = BuildCustomerQuickQuestions(conversation.Policy?.QuickRepliesJson),
                quickRepliesJson = conversation.Policy?.QuickRepliesJson ?? "[]",
                emergencyContact = conversation.Policy?.EmergencyContact ?? string.Empty,
                latestBooking
            });
        }

        // =====================================================================
        // إرسال رسالة جديدة
        // =====================================================================

        [HttpPost]
        public async Task<IActionResult> SendMessage(
            [FromForm] int receiverId,
            [FromForm] int? placeId,
            [FromForm] string? messageText,
            [FromForm] string? quickTopic,
            [FromForm] string? quotedText,
            [FromForm] string? quotedSender,
            CancellationToken cancellationToken)
        {
            if (!TryGetCustomerId(out int customerId))
                return JsonFailure("انتهت الجلسة، يرجى تسجيل الدخول مجددًا.");

            ConversationContext? conversation = await ResolveConversationAsync(
                customerId,
                receiverId,
                placeId,
                requireSendingPermission: true,
                cancellationToken);

            if (conversation == null)
                return JsonFailure("لا يمكنك بدء هذه المحادثة أو أن المنشأة لم تعد متاحة للمراسلة.");

            if (conversation.Policy?.IsChatEnabled == false)
                return JsonFailure("الدردشة متوقفة حاليًا في إعدادات المنشأة.");

            if (IsSystemAccountUnavailable(conversation.Owner.AccountState))
                return JsonFailure("لا يمكن مراسلة حساب المالك حاليًا.");

            string normalizedText = NormalizeMessageText(messageText);
            IFormFileCollection attachments = Request.Form.Files;

            if (string.IsNullOrWhiteSpace(normalizedText) && attachments.Count == 0)
                return JsonFailure("لا يمكن إرسال رسالة فارغة.");

            if (normalizedText.Length > MaximumMessageLength)
                return JsonFailure($"الحد الأقصى لنص الرسالة هو {MaximumMessageLength} حرف.");

            string? filesError = ValidateAttachments(attachments);
            if (filesError != null)
                return JsonFailure(filesError);

            bool isWithinWorkingHours =
                IsWithinWorkingHours(conversation.Policy, DateTime.Now);

            bool isOfflineAutomaticReply = false;
            bool isTopicAutomaticReply = false;
            string? automaticReplyText = null;

            // الأسئلة التي يستطيع النظام الإجابة عنها من البيانات الحالية تعمل طوال الوقت،
            // حتى لو كانت المنشأة خارج وقت العمل؛ لأنها لا تحتاج حضور المالك.
            // وإذا لم يفهم النظام السؤال بثقة، ننتقل لرسالة خارج أوقات العمل (إن كانت مفعلة).
            // لا نخمّن في الرسائل التي تحتوي على مرفقات، ولا نحاول تفسير رسالة فارغة.
            if (attachments.Count == 0 && !string.IsNullOrWhiteSpace(normalizedText))
            {
                HashSet<string> enabledTopics =
                    GetEnabledCustomerTopics(conversation.Policy?.QuickRepliesJson);

                string requestedQuickTopic =
                    (quickTopic ?? string.Empty).Trim().ToLowerInvariant();

                // عند الضغط على زر سؤال سريع يرسل العميل مفتاح الموضوع صراحةً،
                // فلا نعتمد على تخمين النص. الكتابة الحرة فقط تمر عبر المطابقة اللغوية.
                string? detectedTopic =
                    IsQuickQuestionTopic(requestedQuickTopic) &&
                    enabledTopics.Contains(requestedQuickTopic)
                        ? requestedQuickTopic
                        : DetectCustomerTopic(normalizedText);

                if (!string.IsNullOrWhiteSpace(detectedTopic))
                {
                    // إذا سأل العميل عن قاعدة مخصصة كتبها المالك، نعطي القاعدة نفسها
                    // بدل الاكتفاء بملخص القواعد الأربع الثابتة.
                    if (detectedTopic == "rules" && string.IsNullOrWhiteSpace(requestedQuickTopic))
                    {
                        automaticReplyText = BuildCustomHouseRuleReply(
                            normalizedText,
                            conversation.Place.Terms);
                    }

                    if (string.IsNullOrWhiteSpace(automaticReplyText))
                    {
                        automaticReplyText = await BuildAutomaticTopicReplyAsync(
                            detectedTopic,
                            conversation,
                            cancellationToken);
                    }

                    isTopicAutomaticReply =
                        !string.IsNullOrWhiteSpace(automaticReplyText);
                }

                // حتى لو لم يقع السؤال تحت موضوع ثابت، نحاول مطابقته مع أي قاعدة
                // إضافية حفظها المالك للمنشأة. إذا لم نجد تطابقًا واضحًا لا نخمن.
                if (!isTopicAutomaticReply && string.IsNullOrWhiteSpace(requestedQuickTopic))
                {
                    automaticReplyText = BuildCustomHouseRuleReply(
                        normalizedText,
                        conversation.Place.Terms);

                    isTopicAutomaticReply =
                        !string.IsNullOrWhiteSpace(automaticReplyText);
                }

                // الرد خارج أوقات العمل هو fallback فقط للأسئلة/الرسائل التي
                // لم يستطع النظام الإجابة عنها، وليس بديلًا عن الأسئلة السريعة.
                if (!isTopicAutomaticReply &&
                    !isWithinWorkingHours &&
                    conversation.Policy?.OfflineReplyToggle == true &&
                    !string.IsNullOrWhiteSpace(conversation.Policy.OfflineMessage))
                {
                    automaticReplyText = conversation.Policy.OfflineMessage!.Trim();
                    isOfflineAutomaticReply = true;
                }
            }

            string finalMessage = BuildMessageWithQuote(
                normalizedText,
                quotedText,
                quotedSender);

            DateTime now = DateTime.Now;
            List<Chat> messagesToSave = new List<Chat>();
            List<string> createdPhysicalFiles = new List<string>();

            try
            {
                if (attachments.Count == 0)
                {
                    messagesToSave.Add(new Chat
                    {
                        SenderId = customerId,
                        ReceiverId = receiverId,
                        PlaceId = conversation.Place.PlaceId,
                        MessageText = finalMessage,
                        AttachmentUrl = string.Empty,
                        SendTime = now,
                        IsRead = isTopicAutomaticReply,
                        IsDeletedForSender = false,
                        IsDeletedForReceiver = false,
                        IsEdited = false,
                        IsReportMessage = false,
                        ReportId = null
                    });
                }
                else
                {
                    string uploadsFolder = Path.Combine(
                        _environment.WebRootPath,
                        "uploads",
                        "chats");

                    Directory.CreateDirectory(uploadsFolder);

                    for (int index = 0; index < attachments.Count; index++)
                    {
                        IFormFile file = attachments[index];
                        string storedFileName = BuildSafeStoredFileName(file.FileName);
                        string physicalPath = Path.Combine(uploadsFolder, storedFileName);

                        await using (FileStream stream = new FileStream(
                            physicalPath,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None))
                        {
                            await file.CopyToAsync(stream, cancellationToken);
                        }

                        createdPhysicalFiles.Add(physicalPath);

                        messagesToSave.Add(new Chat
                        {
                            SenderId = customerId,
                            ReceiverId = receiverId,
                            PlaceId = conversation.Place.PlaceId,
                            MessageText = index == 0 ? finalMessage : string.Empty,
                            AttachmentUrl = $"/uploads/chats/{storedFileName}",
                            SendTime = now.AddMilliseconds(index),
                            IsRead = isTopicAutomaticReply,
                            IsDeletedForSender = false,
                            IsDeletedForReceiver = false,
                            IsEdited = false,
                            IsReportMessage = false,
                            ReportId = null
                        });
                    }
                }

                _context.Chats.AddRange(messagesToSave);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                foreach (string physicalPath in createdPhysicalFiles)
                    TryDeletePhysicalFile(physicalPath);

                return JsonFailure("تعذر حفظ الرسالة حاليًا، يرجى إعادة المحاولة.");
            }

            Chat? automaticReply = null;

            if (!string.IsNullOrWhiteSpace(automaticReplyText))
            {
                automaticReply = new Chat
                {
                    SenderId = receiverId,
                    ReceiverId = customerId,
                    PlaceId = conversation.Place.PlaceId,
                    MessageText = automaticReplyText.Trim(),
                    AttachmentUrl = string.Empty,
                    SendTime = now.AddMilliseconds(messagesToSave.Count + 5),
                    IsRead = false,
                    IsDeletedForSender = false,
                    IsDeletedForReceiver = false,
                    IsEdited = false,
                    IsReportMessage = false,
                    ReportId = null
                };

                _context.Chats.Add(automaticReply);
                await _context.SaveChangesAsync(cancellationToken);
            }

            foreach (Chat message in messagesToSave)
            {
                ChatMessageDto dto = BuildMessageDto(
                    message,
                    customerId,
                    conversation.Place.PlaceId);

                await _hubContext.Clients.User(receiverId.ToString())
                    .SendAsync(
                        "ReceiveMessage",
                        new
                        {
                            messageId = dto.MessageId,
                            senderId = dto.SenderId,
                            receiverId = dto.ReceiverId,
                            placeId = dto.PlaceId,
                            text = dto.Text ?? string.Empty,
                            time = dto.Time ?? string.Empty,
                            attachmentUrl = dto.AttachmentUrl ?? string.Empty,
                            attachmentName = dto.AttachmentName ?? string.Empty,
                            attachmentSize = dto.AttachmentSize ?? string.Empty,
                            isImage = dto.IsImage,
                            isVideo = dto.IsVideo,
                            sendTimeRaw = dto.SendTimeRaw
                        },
                        cancellationToken);
            }

            if (automaticReply != null)
            {
                ChatMessageDto automaticDto = BuildMessageDto(
                    automaticReply,
                    customerId,
                    conversation.Place.PlaceId);

                await _hubContext.Clients.User(customerId.ToString())
                    .SendAsync(
                        "ReceiveMessage",
                        new
                        {
                            messageId = automaticDto.MessageId,
                            senderId = automaticDto.SenderId,
                            receiverId = automaticDto.ReceiverId,
                            placeId = automaticDto.PlaceId,
                            text = automaticDto.Text ?? string.Empty,
                            time = automaticDto.Time ?? string.Empty,
                            attachmentUrl = automaticDto.AttachmentUrl ?? string.Empty,
                            attachmentName = automaticDto.AttachmentName ?? string.Empty,
                            attachmentSize = automaticDto.AttachmentSize ?? string.Empty,
                            isImage = automaticDto.IsImage,
                            isVideo = automaticDto.IsVideo,
                            sendTimeRaw = automaticDto.SendTimeRaw
                        },
                        cancellationToken);
            }

            // السؤال الذي أجاب عنه النظام بثقة لا يحتاج إزعاج المالك بإشعار جديد.
            // خارج وقت العمل تبقى رسالة العميل غير مقروءة ويصل إشعار للمالك ليتابعها لاحقًا.
            if (!isTopicAutomaticReply || isOfflineAutomaticReply)
            {
                try
                {
                string messagePreview;

                if (!string.IsNullOrWhiteSpace(normalizedText))
                {
                    messagePreview = normalizedText.Trim();
                    if (messagePreview.Length > 120)
                    {
                        messagePreview =
                            messagePreview[..120] + "…";
                    }
                }
                else
                {
                    messagePreview =
                        attachments.Count == 1
                            ? "أرسل لك العميل مرفقًا جديدًا."
                            : $"أرسل لك العميل {attachments.Count} مرفقات جديدة.";
                }

                await _notificationService.CreateAsync(
                    new CreateNotificationDto
                    {
                        UserId = receiverId,
                        NotificationTitle =
                            "رسالة جديدة من عميل",
                        NotificationText =
                            $"{conversation.Place.PlaceNameAndDesc}: {messagePreview}",
                        NotificationType =
                            NotificationTypes.Message,
                        TargetUrl =
                            "/Owner/Chat/ChatInterface",
                        ReferenceType =
                            NotificationReferenceTypes.Chat,
                        ReferenceId =
                            messagesToSave.FirstOrDefault()?.MessageId
                    },
                    cancellationToken);
                }
                catch (Exception notificationException)
                {
                    Console.WriteLine(
                        $"Chat notification error: {notificationException.Message}");
                }
            }

            return Json(new
            {
                success = true,
                placeId = conversation.Place.PlaceId,
                autoReplied = automaticReply != null,
                autoReplyKind = isOfflineAutomaticReply
                    ? "offline"
                    : isTopicAutomaticReply ? "topic" : string.Empty,
                messageIds = messagesToSave
                    .Select(message => message.MessageId)
                    .ToArray()
            });
        }

        // =====================================================================
        // تعديل رسالة
        // =====================================================================

        [HttpPost]
        public async Task<IActionResult> EditMessage(
            int messageId,
            int? placeId,
            string? newText,
            CancellationToken cancellationToken)
        {
            if (!TryGetCustomerId(out int customerId))
                return JsonFailure("انتهت الجلسة.");

            Chat? message = await _context.Chats
                .FirstOrDefaultAsync(
                    chat =>
                        chat.MessageId == messageId &&
                        !chat.IsReportMessage &&
                        chat.ReportId == null,
                    cancellationToken);

            if (message == null || message.SenderId != customerId)
                return JsonFailure("غير مصرح لك بتعديل هذه الرسالة.");

            if (message.IsDeletedForSender || message.IsDeletedForReceiver)
                return JsonFailure("لا يمكن تعديل رسالة محذوفة.");

            int ownerId = message.ReceiverId;
            int? resolvedPlaceId = await ResolveMessagePlaceIdAsync(
                message,
                customerId,
                ownerId,
                placeId,
                cancellationToken);

            if (!resolvedPlaceId.HasValue)
                return JsonFailure("تعذر تحديد منشأة هذه الرسالة.");

            ConversationContext? conversation = await ResolveConversationAsync(
                customerId,
                ownerId,
                resolvedPlaceId,
                requireSendingPermission: true,
                cancellationToken);

            if (conversation == null)
                return JsonFailure("لا يمكن تعديل الرسالة في هذه المحادثة.");

            if (message.SendTime.HasValue &&
                (DateTime.Now - message.SendTime.Value).TotalMinutes > 10)
            {
                return JsonFailure("انتهت مهلة التعديل المسموحة (10 دقائق).");
            }

            string normalizedText = NormalizeMessageText(newText);

            if (string.IsNullOrWhiteSpace(normalizedText))
                return JsonFailure("لا يمكن حفظ رسالة فارغة.");

            if (normalizedText.Length > MaximumMessageLength)
                return JsonFailure($"الحد الأقصى لنص الرسالة هو {MaximumMessageLength} حرف.");

            message.MessageText = ReplaceMessageBodyPreservingQuote(
                message.MessageText,
                normalizedText);

            message.IsEdited = true;

            if (!message.PlaceId.HasValue)
                message.PlaceId = resolvedPlaceId.Value;

            await _context.SaveChangesAsync(cancellationToken);

            await _hubContext.Clients.User(ownerId.ToString())
                .SendAsync(
                    "MessageEdited",
                    new
                    {
                        messageId = message.MessageId,
                        placeId = resolvedPlaceId.Value,
                        text = message.MessageText ?? string.Empty,
                        isEdited = true
                    },
                    cancellationToken);

            return Json(new
            {
                success = true,
                message = "تم تعديل الرسالة بنجاح.",
                placeId = resolvedPlaceId.Value
            });
        }

        // =====================================================================
        // حذف رسالة من شاشة العميل فقط
        // =====================================================================

        [HttpPost]
        public async Task<IActionResult> DeleteMessageForMe(
            int messageId,
            int? placeId,
            CancellationToken cancellationToken)
        {
            if (!TryGetCustomerId(out int customerId))
                return JsonFailure("انتهت الجلسة.");

            Chat? message = await _context.Chats
                .FirstOrDefaultAsync(
                    chat =>
                        chat.MessageId == messageId &&
                        !chat.IsReportMessage &&
                        chat.ReportId == null,
                    cancellationToken);

            if (message == null)
                return JsonFailure("الرسالة غير موجودة.");

            int ownerId;

            if (message.SenderId == customerId)
            {
                ownerId = message.ReceiverId;
                message.IsDeletedForSender = true;
            }
            else if (message.ReceiverId == customerId)
            {
                ownerId = message.SenderId;
                message.IsDeletedForReceiver = true;
            }
            else
            {
                return JsonFailure("لا يمكنك حذف هذه الرسالة.");
            }

            int? resolvedPlaceId = await ResolveMessagePlaceIdAsync(
                message,
                customerId,
                ownerId,
                placeId,
                cancellationToken);

            if (!resolvedPlaceId.HasValue)
                return JsonFailure("تعذر تحديد منشأة هذه الرسالة.");

            if (!message.PlaceId.HasValue)
                message.PlaceId = resolvedPlaceId.Value;

            await _context.SaveChangesAsync(cancellationToken);

            return Json(new
            {
                success = true,
                placeId = resolvedPlaceId.Value
            });
        }

        // =====================================================================
        // حذف رسالة لدى الطرفين
        // =====================================================================

        [HttpPost]
        public async Task<IActionResult> DeleteMessageForEveryone(
            int messageId,
            int? placeId,
            CancellationToken cancellationToken)
        {
            if (!TryGetCustomerId(out int customerId))
                return JsonFailure("انتهت الجلسة.");

            Chat? message = await _context.Chats
                .FirstOrDefaultAsync(
                    chat =>
                        chat.MessageId == messageId &&
                        !chat.IsReportMessage &&
                        chat.ReportId == null,
                    cancellationToken);

            if (message == null || message.SenderId != customerId)
                return JsonFailure("لا يمكنك حذف رسالة لم تقم بإرسالها.");

            if (message.IsDeletedForSender || message.IsDeletedForReceiver)
                return JsonFailure("الرسالة محذوفة بالفعل.");

            if (message.SendTime.HasValue &&
                (DateTime.Now - message.SendTime.Value).TotalHours > 24)
            {
                return JsonFailure("لا يمكن الحذف للجميع بعد مرور 24 ساعة.");
            }

            int ownerId = message.ReceiverId;
            int? resolvedPlaceId = await ResolveMessagePlaceIdAsync(
                message,
                customerId,
                ownerId,
                placeId,
                cancellationToken);

            if (!resolvedPlaceId.HasValue)
                return JsonFailure("تعذر تحديد منشأة هذه الرسالة.");

            bool hasConfirmedBooking = await _context.Bookings
                .AsNoTracking()
                .AnyAsync(booking =>
                    booking.CustomerId == customerId &&
                    booking.PlaceId == resolvedPlaceId.Value &&
                    booking.BookingState != "Cancelled" &&
                    booking.BookingState != "Canceled" &&
                    booking.BookingState != "Pending" &&
                    booking.BookingState != "Rejected",
                    cancellationToken);

            if (hasConfirmedBooking)
            {
                return JsonFailure(
                    "لا يمكن الحذف للجميع لأن المحادثة مرتبطة بحجز مؤكد.");
            }

            message.IsDeletedForSender = true;
            message.IsDeletedForReceiver = true;

            if (!message.PlaceId.HasValue)
                message.PlaceId = resolvedPlaceId.Value;

            await _context.SaveChangesAsync(cancellationToken);

            await _hubContext.Clients.User(ownerId.ToString())
                .SendAsync(
                    "MessageDeleted",
                    new
                    {
                        messageId = message.MessageId,
                        placeId = resolvedPlaceId.Value
                    },
                    cancellationToken);

            return Json(new
            {
                success = true,
                placeId = resolvedPlaceId.Value
            });
        }

        // =====================================================================
        // مسح محادثة منشأة واحدة من شاشة العميل
        // =====================================================================

        [HttpPost]
        public async Task<IActionResult> ClearChatForMe(
            int ownerId,
            int? placeId,
            CancellationToken cancellationToken)
        {
            if (!TryGetCustomerId(out int customerId))
                return JsonFailure("انتهت الجلسة.");

            ConversationContext? conversation = await ResolveConversationAsync(
                customerId,
                ownerId,
                placeId,
                requireSendingPermission: false,
                cancellationToken);

            if (conversation == null)
                return JsonFailure("تعذر تحديد المحادثة المطلوبة.");

            List<Chat> messages = await ScopedConversationQuery(
                    customerId,
                    ownerId,
                    conversation.Place.PlaceId,
                    conversation.IncludeLegacyMessages,
                    includeDeletedForCustomer: true)
                .ToListAsync(cancellationToken);

            foreach (Chat message in messages)
            {
                if (message.SenderId == customerId)
                    message.IsDeletedForSender = true;
                else if (message.ReceiverId == customerId)
                    message.IsDeletedForReceiver = true;

                if (!message.PlaceId.HasValue &&
                    conversation.IncludeLegacyMessages)
                {
                    message.PlaceId = conversation.Place.PlaceId;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Json(new
            {
                success = true,
                placeId = conversation.Place.PlaceId,
                message = "تم مسح محادثة هذه المنشأة من شاشتك فقط."
            });
        }

        // =====================================================================
        // عداد المحادثات غير المقروءة للشريط السفلي
        // =====================================================================

        [HttpGet]
        public async Task<IActionResult> GetUnreadConversationCount(
            CancellationToken cancellationToken)
        {
            if (!TryGetCustomerId(out int customerId))
                return JsonFailure("انتهت الجلسة.");

            List<CustomerChatContactDto> contacts = await BuildContactsAsync(
                customerId,
                requestedPlaceId: null,
                requestedOwnerId: null,
                cancellationToken: cancellationToken);

            return Json(new
            {
                success = true,
                count = contacts.Count(contact => contact.UnreadCount > 0),
                totalMessages = contacts.Sum(contact => contact.UnreadCount)
            });
        }

        // =====================================================================
        // التحقق وبناء نطاق المحادثة
        // =====================================================================

        private async Task<ConversationContext?> ResolveConversationAsync(
            int customerId,
            int ownerId,
            int? requestedPlaceId,
            bool requireSendingPermission,
            CancellationToken cancellationToken)
        {
            if (customerId <= 0 || ownerId <= 0 || customerId == ownerId)
                return null;

            int? resolvedPlaceId = requestedPlaceId;

            if (!resolvedPlaceId.HasValue || resolvedPlaceId.Value <= 0)
            {
                resolvedPlaceId = await ResolvePreferredPlaceIdAsync(
                    customerId,
                    ownerId,
                    cancellationToken);
            }

            if (!resolvedPlaceId.HasValue)
                return null;

            Place? place = await _context.Places
                .Include(item => item.Owner)
                .Include(item => item.PlacePolicy)
                .FirstOrDefaultAsync(
                    item =>
                        item.PlaceId == resolvedPlaceId.Value &&
                        item.OwnerId == ownerId,
                    cancellationToken);

            if (place == null ||
                place.Owner == null ||
                !string.Equals(place.Owner.Role, "Owner", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            bool hasBooking = await _context.Bookings
                .AsNoTracking()
                .AnyAsync(booking =>
                    booking.CustomerId == customerId &&
                    booking.PlaceId == place.PlaceId,
                    cancellationToken);

            bool hasPreviousChat = await _context.Chats
                .AsNoTracking()
                .AnyAsync(chat =>
                    chat.PlaceId == place.PlaceId &&
                    !chat.IsReportMessage &&
                    chat.ReportId == null &&
                    (
                        (chat.SenderId == customerId && chat.ReceiverId == ownerId) ||
                        (chat.SenderId == ownerId && chat.ReceiverId == customerId)
                    ),
                    cancellationToken);

            bool includeLegacy = await CanIncludeLegacyMessagesAsync(
                customerId,
                ownerId,
                place.PlaceId,
                cancellationToken);

            bool hasLegacyChat = includeLegacy && await _context.Chats
                .AsNoTracking()
                .AnyAsync(chat =>
                    chat.PlaceId == null &&
                    !chat.IsReportMessage &&
                    chat.ReportId == null &&
                    (
                        (chat.SenderId == customerId && chat.ReceiverId == ownerId) ||
                        (chat.SenderId == ownerId && chat.ReceiverId == customerId)
                    ),
                    cancellationToken);

            hasPreviousChat = hasPreviousChat || hasLegacyChat;

            bool canStartNewConversation =
                IsPublicPlaceAvailableForNewConversation(place) &&
                place.PlacePolicy?.IsChatEnabled != false &&
                !IsSystemAccountUnavailable(place.Owner.AccountState);

            bool canAccess =
                hasBooking ||
                hasPreviousChat ||
                canStartNewConversation;

            if (!canAccess)
                return null;

            if (requireSendingPermission)
            {
                if (place.PlacePolicy?.IsChatEnabled == false)
                    return null;

                if (IsSystemAccountUnavailable(place.Owner.AccountState))
                    return null;

                if (!hasBooking && !hasPreviousChat && !canStartNewConversation)
                    return null;
            }

            return new ConversationContext(
                place,
                place.Owner,
                place.PlacePolicy,
                hasBooking,
                hasPreviousChat,
                includeLegacy);
        }

        private async Task<int?> ResolvePreferredPlaceIdAsync(
            int customerId,
            int ownerId,
            CancellationToken cancellationToken)
        {
            int? latestChatPlaceId = await _context.Chats
                .AsNoTracking()
                .Where(chat =>
                    chat.PlaceId != null &&
                    !chat.IsReportMessage &&
                    chat.ReportId == null &&
                    chat.Place!.OwnerId == ownerId &&
                    (
                        (chat.SenderId == customerId && chat.ReceiverId == ownerId) ||
                        (chat.SenderId == ownerId && chat.ReceiverId == customerId)
                    ))
                .OrderByDescending(chat => chat.SendTime)
                .ThenByDescending(chat => chat.MessageId)
                .Select(chat => chat.PlaceId)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestChatPlaceId.HasValue)
                return latestChatPlaceId.Value;

            int? latestBookingPlaceId = await _context.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.CustomerId == customerId &&
                    booking.Place.OwnerId == ownerId)
                .OrderByDescending(booking => booking.CreatedAt)
                .ThenByDescending(booking => booking.BookingId)
                .Select(booking => (int?)booking.PlaceId)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestBookingPlaceId.HasValue)
                return latestBookingPlaceId.Value;

            List<int> ownerPlaceIds = await _context.Places
                .AsNoTracking()
                .Where(place => place.OwnerId == ownerId)
                .Select(place => place.PlaceId)
                .Take(2)
                .ToListAsync(cancellationToken);

            return ownerPlaceIds.Count == 1
                ? ownerPlaceIds[0]
                : null;
        }

        private async Task<bool> CanIncludeLegacyMessagesAsync(
            int customerId,
            int ownerId,
            int activePlaceId,
            CancellationToken cancellationToken)
        {
            List<int> relationshipPlaceIds = await _context.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.CustomerId == customerId &&
                    booking.Place.OwnerId == ownerId)
                .Select(booking => booking.PlaceId)
                .Distinct()
                .ToListAsync(cancellationToken);

            List<int> chatPlaceIds = await _context.Chats
                .AsNoTracking()
                .Where(chat =>
                    chat.PlaceId != null &&
                    !chat.IsReportMessage &&
                    chat.ReportId == null &&
                    chat.Place!.OwnerId == ownerId &&
                    (
                        (chat.SenderId == customerId && chat.ReceiverId == ownerId) ||
                        (chat.SenderId == ownerId && chat.ReceiverId == customerId)
                    ))
                .Select(chat => chat.PlaceId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            HashSet<int> placeIds = relationshipPlaceIds.ToHashSet();
            placeIds.UnionWith(chatPlaceIds);

            if (placeIds.Count == 1)
                return placeIds.Contains(activePlaceId);

            if (placeIds.Count > 1)
                return false;

            List<int> ownerPlaces = await _context.Places
                .AsNoTracking()
                .Where(place => place.OwnerId == ownerId)
                .Select(place => place.PlaceId)
                .Take(2)
                .ToListAsync(cancellationToken);

            return ownerPlaces.Count == 1 && ownerPlaces[0] == activePlaceId;
        }

        private IQueryable<Chat> ScopedConversationQuery(
            int customerId,
            int ownerId,
            int placeId,
            bool includeLegacy,
            bool includeDeletedForCustomer)
        {
            IQueryable<Chat> query = _context.Chats.Where(chat =>
                !chat.IsReportMessage &&
                chat.ReportId == null &&
                (chat.PlaceId == placeId || (includeLegacy && chat.PlaceId == null)) &&
                (
                    (chat.SenderId == customerId && chat.ReceiverId == ownerId) ||
                    (chat.SenderId == ownerId && chat.ReceiverId == customerId)
                ));

            if (!includeDeletedForCustomer)
            {
                query = query.Where(chat =>
                    (chat.SenderId == customerId && !chat.IsDeletedForSender) ||
                    (chat.ReceiverId == customerId && !chat.IsDeletedForReceiver));
            }

            return query;
        }

        private async Task<int?> ResolveMessagePlaceIdAsync(
            Chat message,
            int customerId,
            int ownerId,
            int? requestedPlaceId,
            CancellationToken cancellationToken)
        {
            if (message.PlaceId.HasValue)
            {
                if (requestedPlaceId.HasValue &&
                    requestedPlaceId.Value != message.PlaceId.Value)
                {
                    return null;
                }

                return message.PlaceId.Value;
            }

            if (requestedPlaceId.HasValue && requestedPlaceId.Value > 0)
            {
                bool canInclude = await CanIncludeLegacyMessagesAsync(
                    customerId,
                    ownerId,
                    requestedPlaceId.Value,
                    cancellationToken);

                return canInclude
                    ? requestedPlaceId.Value
                    : null;
            }

            int? preferredPlaceId = await ResolvePreferredPlaceIdAsync(
                customerId,
                ownerId,
                cancellationToken);

            if (!preferredPlaceId.HasValue)
                return null;

            bool canIncludePreferredLegacy = await CanIncludeLegacyMessagesAsync(
                customerId,
                ownerId,
                preferredPlaceId.Value,
                cancellationToken);

            return canIncludePreferredLegacy
                ? preferredPlaceId.Value
                : null;
        }

        // =====================================================================
        // أدوات الرسائل والمرفقات
        // =====================================================================

        private ChatMessageDto BuildMessageDto(
            Chat message,
            int customerId,
            int fallbackPlaceId)
        {
            string attachmentName = GetAttachmentDisplayName(message.AttachmentUrl);
            string attachmentSize = GetAttachmentSize(message.AttachmentUrl);
            string extension = Path.GetExtension(message.AttachmentUrl ?? string.Empty);

            return new ChatMessageDto
            {
                MessageId = message.MessageId,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                PlaceId = message.PlaceId ?? fallbackPlaceId,
                Text = StripLegacyAutomaticReplyLabel(
                    NormalizeMessageText(message.MessageText)),
                Time = FormatChatTime(message.SendTime),
                IsSentByMe = message.SenderId == customerId,
                IsRead = message.IsRead,
                IsEdited = message.IsEdited,
                AttachmentUrl = message.AttachmentUrl ?? string.Empty,
                AttachmentName = attachmentName,
                AttachmentSize = attachmentSize,
                IsImage = IsImageExtension(extension),
                IsVideo = IsVideoExtension(extension),
                SendTimeRaw = message.SendTime
            };
        }

        private static string StripLegacyAutomaticReplyLabel(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            const string legacyLabel = "⚡ رد تلقائي من النظام:";
            string trimmed = text.TrimStart();

            if (!trimmed.StartsWith(legacyLabel, StringComparison.Ordinal))
                return text;

            return trimmed.Substring(legacyLabel.Length)
                .TrimStart('\r', '\n', ' ');
        }

        private static string NormalizeMessageText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string text = value
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');

            text = Regex.Replace(text, "<.*?>", string.Empty);
            text = Regex.Replace(text, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", string.Empty);
            text = Regex.Replace(text, @"\n{4,}", "\n\n\n");

            return text.Trim();
        }

        private static string BuildMessageWithQuote(
            string messageText,
            string? quotedText,
            string? quotedSender)
        {
            if (string.IsNullOrWhiteSpace(quotedText) ||
                string.IsNullOrWhiteSpace(quotedSender))
            {
                return messageText;
            }

            string cleanQuotedText = NormalizeMessageText(quotedText)
                .Replace("|", " ")
                .Replace("]", " ")
                .Trim();

            string cleanQuotedSender = NormalizeMessageText(quotedSender)
                .Replace("|", " ")
                .Replace("]", " ")
                .Replace("\n", " ")
                .Trim();

            if (cleanQuotedText.Length > MaximumQuotedTextLength)
                cleanQuotedText = cleanQuotedText.Substring(0, MaximumQuotedTextLength);

            if (cleanQuotedSender.Length > 80)
                cleanQuotedSender = cleanQuotedSender.Substring(0, 80);

            return $"[REPLY|{cleanQuotedSender}|{cleanQuotedText}] {messageText}";
        }

        private static string ReplaceMessageBodyPreservingQuote(
            string? originalText,
            string newBody)
        {
            if (string.IsNullOrEmpty(originalText) ||
                !originalText.StartsWith("[REPLY|", StringComparison.Ordinal))
            {
                return newBody;
            }

            int closingBracketIndex = originalText.IndexOf(']');
            if (closingBracketIndex < 0)
                return newBody;

            string quotePart = originalText.Substring(0, closingBracketIndex + 1);
            return $"{quotePart} {newBody}";
        }

        private static string? ValidateAttachments(IFormFileCollection files)
        {
            if (files.Count > MaximumFilesPerMessage)
                return $"الحد الأقصى هو {MaximumFilesPerMessage} ملفات في الرسالة الواحدة.";

            long totalSize = 0;

            foreach (IFormFile file in files)
            {
                if (file.Length <= 0)
                    return $"الملف {file.FileName} فارغ.";

                if (file.Length > MaximumFileSizeBytes)
                    return $"الملف {file.FileName} يتجاوز 10 ميجابايت.";

                string extension = Path.GetExtension(file.FileName);

                if (string.IsNullOrWhiteSpace(extension) ||
                    !AllowedFileExtensions.Contains(extension))
                {
                    return $"نوع الملف {file.FileName} غير مسموح.";
                }

                totalSize += file.Length;
            }

            if (totalSize > MaximumRequestFilesSizeBytes)
                return "الحجم الإجمالي للمرفقات يجب ألا يتجاوز 25 ميجابايت.";

            return null;
        }

        private static string BuildSafeStoredFileName(string originalFileName)
        {
            string extension = Path.GetExtension(originalFileName).ToLowerInvariant();
            string baseName = Path.GetFileNameWithoutExtension(originalFileName);
            baseName = Regex.Replace(baseName, @"[^\p{L}\p{N}\-_ ]", "_").Trim();

            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "file";

            if (baseName.Length > 60)
                baseName = baseName.Substring(0, 60);

            return $"{Guid.NewGuid():N}_{baseName}{extension}";
        }

        private string GetAttachmentSize(string? attachmentUrl)
        {
            if (string.IsNullOrWhiteSpace(attachmentUrl))
                return string.Empty;

            string physicalPath = Path.Combine(
                _environment.WebRootPath,
                attachmentUrl
                    .TrimStart('/')
                    .Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(physicalPath))
                return string.Empty;

            long bytes = new FileInfo(physicalPath).Length;

            return bytes >= 1024L * 1024L
                ? $"{bytes / 1048576.0:F2} MB"
                : $"{bytes / 1024.0:F2} KB";
        }

        private static string GetAttachmentDisplayName(string? attachmentUrl)
        {
            if (string.IsNullOrWhiteSpace(attachmentUrl))
                return string.Empty;

            string fileName = Path.GetFileName(attachmentUrl);
            int separatorIndex = fileName.IndexOf('_');

            return separatorIndex >= 0 && separatorIndex < fileName.Length - 1
                ? fileName.Substring(separatorIndex + 1)
                : fileName;
        }

        private static bool IsImageExtension(string? extension)
        {
            return string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVideoExtension(string? extension)
        {
            return string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".webm", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".mov", StringComparison.OrdinalIgnoreCase);
        }

        // =====================================================================
        // الأسئلة السريعة والردود الآلية للعميل
        // =====================================================================

        private static HashSet<string> GetEnabledCustomerTopics(string? jsonText)
        {
            HashSet<string> topics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool hasExplicitTopicConfiguration = false;

            if (!string.IsNullOrWhiteSpace(jsonText))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(jsonText);
                    if (document.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in document.RootElement.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.Object)
                                continue;

                            string type = GetJsonString(item, "type", "Type");

                            if (string.Equals(
                                type,
                                "customer-topic-config",
                                StringComparison.OrdinalIgnoreCase))
                            {
                                hasExplicitTopicConfiguration = true;
                                continue;
                            }

                            string key = GetJsonString(
                                item,
                                "key",
                                "Key",
                                "topicKey",
                                "TopicKey");

                            if (!string.Equals(
                                    type,
                                    "customer-topic",
                                    StringComparison.OrdinalIgnoreCase) &&
                                string.IsNullOrWhiteSpace(key))
                            {
                                continue;
                            }

                            key = key.Trim().ToLowerInvariant();
                            if (IsQuickQuestionTopic(key))
                                topics.Add(key);
                        }
                    }
                }
                catch (JsonException)
                {
                    // بيانات قديمة أو غير صالحة لا نستخدمها للتخمين.
                }
            }

            // واجهة المالك الجديدة تعرض هذه الخيارات مفعلة افتراضيًا.
            // لذلك ما لم يكن المالك قد حفظ إعدادًا صريحًا، نجعل السلوك الفعلي
            // مطابقًا لما يراه في الواجهة بدل أن تظهر الأزرار مفعلة ولا تصل للعميل.
            if (!hasExplicitTopicConfiguration && topics.Count == 0)
            {
                topics.UnionWith(new[]
                {
                    "price",
                    "availability",
                    "cancellation",
                    "checkinout",
                    "payment"
                });
            }

            return topics;
        }

        private static List<Dictionary<string, string>> BuildCustomerQuickQuestions(
            string? jsonText)
        {
            HashSet<string> enabledTopics = GetEnabledCustomerTopics(jsonText);
            List<Dictionary<string, string>> questions =
                new List<Dictionary<string, string>>();

            void Add(string key, string title, string question)
            {
                if (!enabledTopics.Contains(key))
                    return;

                questions.Add(new Dictionary<string, string>
                {
                    ["key"] = key,
                    ["title"] = title,
                    ["question"] = question
                });
            }

            Add("price", "الأسعار", "بكم الأسعار حاليًا؟");
            Add("availability", "التوفر", "هل توجد وحدات متاحة؟");
            Add("cancellation", "الإلغاء والاسترداد", "ما سياسة الإلغاء والاسترداد؟");
            Add("checkinout", "الدخول والخروج", "ما أوقات الدخول والخروج؟");
            Add("payment", "العربون والدفع", "ما شروط العربون والدفع؟");

            return questions;
        }

        private static bool IsQuickQuestionTopic(string key)
        {
            return key == "price" ||
                   key == "availability" ||
                   key == "cancellation" ||
                   key == "checkinout" ||
                   key == "payment";
        }

        private static string? DetectCustomerTopic(string messageText)
        {
            string text = NormalizeArabicIntentText(messageText);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            bool HasAny(params string[] values) =>
                values.Any(value => text.Contains(value, StringComparison.Ordinal));

            // الكتابة الحرة لا تعتمد على الأزرار التي اختار المالك إظهارها.
            // الأزرار مجرد اختصارات، أما أي سؤال واضح عن بيانات المنشأة أو سياساتها
            // فنحاول الإجابة عنه من البيانات الحالية فقط، بدون تخمين.

            // نبدأ بالمعاني الأكثر تحديدًا حتى لا تُصنّف عبارة مثل "يرجع العربون؟"
            // على أنها دفع بدل سياسة إلغاء واسترداد.
            if (HasAny(
                "الغاء", "الغي", "الغيه", "الغيت", "لغيت", "اللغاء",
                "استرداد", "استرجاع", "كنسل", "كنسلت", "بطلت الحجز",
                "يرجع العربون", "يرجع لي العربون", "ارجع العربون", "ارجاع العربون",
                "الفلوس ترجع", "المبلغ يرجع", "يرجع لي المبلغ", "لو بطلت الحجز",
                "عدم حضور", "ما حضرت", "ما جيت"))
            {
                return "cancellation";
            }

            if (HasAny(
                "دخول", "خروج", "ادخل", "اخرج", "اطلع", "استلام", "تسليم",
                "استلم", "اسلم", "شيك ان", "شيك اوت", "متى ندخل", "متى نطلع",
                "متى الدخول", "متى الخروج", "وقت الوصول", "وقت المغادره"))
            {
                return "checkinout";
            }

            if (HasAny(
                "متوفر", "متوفره", "متاح", "متاحه", "فاضي", "فاضيه", "فاضيين",
                "شاغر", "شاغره", "في غرف", "فيه غرف", "عندكم غرف", "معكم غرف",
                "في وحدات", "فيه وحدات", "عندكم وحدات", "توفر"))
            {
                return "availability";
            }

            if (HasAny(
                "عربون", "دفع", "ادفع", "الدفع", "مقدم", "مقدما",
                "مهله السداد", "مهلة السداد", "كاش", "نقد", "تحويل"))
            {
                return "payment";
            }

            if (HasAny(
                "سعر", "اسعار", "بكم", "كم حق", "كم يكلف", "التكلفه", "تكلفه",
                "قيمه", "قيمة", "حق الليله", "حق الليلة", "كم الليله", "كم الليلة",
                "كم الغرفه", "كم الغرفة", "سعر الغرفه", "سعر الغرفة"))
            {
                return "price";
            }

            if (HasAny(
                "سياسه التشغيل", "نظام التشغيل", "كيف التشغيل", "طريقه التشغيل",
                "الية التشغيل", "اليه التشغيل", "نظام المواعيد", "نظام الفترات",
                "فترات التشغيل", "فترات الحجز", "ورديات", "ورديه", "شفت", "شفتات",
                "قبل كم احجز", "متى اقدر احجز", "اقدر احجز قبل", "كم شهر احجز",
                "ايام الاغلاق", "ايام مغلقه"))
            {
                return "operation";
            }

            if (HasAny(
                "ضمان", "ضمانات", "الضمانات", "ضمانه", "تأمين", "تامين",
                "متطلبات الضمان", "الضمان المطلوب", "الضمانات المطلوبه",
                "اثبات الهويه", "اثبات هوية", "بطاقه شخصيه", "بطاقة شخصية"))
            {
                return "security";
            }

            if (HasAny(
                "تدخين", "دخان", "سيجاره", "سيجارة", "شيشه", "شيشة",
                "مدخنين", "مسموح التدخين", "ممنوع التدخين"))
            {
                return "smoking";
            }

            if (HasAny(
                "عوائل", "عائلات", "عائله", "عائلة", "عزاب", "عزابي",
                "للعائلات", "للعوائل", "عائلي"))
            {
                return "families";
            }

            if (HasAny(
                "حفله", "حفلة", "حفلات", "مناسبه", "مناسبة", "مناسبات",
                "فعاليه", "فعالية", "فعاليات"))
            {
                return "parties";
            }

            if (HasAny(
                "قات", "مضغ القات", "القات مسموح", "القات ممنوع"))
            {
                return "qat";
            }

            if (HasAny(
                "القواعد", "قواعد المنشأه", "قواعد المنشاه", "سياسات المنشأه",
                "سياسات المنشاه", "قوانين المنشأه", "قوانين المنشاه"))
            {
                return "rules";
            }

            if (HasAny(
                "رسوم", "ضريبه", "ضريبة", "تنظيف", "رسوم التنظيف", "الضريبه",
                "تكاليف اضافيه", "تكلفة اضافية", "رسوم اضافيه"))
            {
                return "fees";
            }

            if (HasAny(
                "خدمات", "الخدمات", "مرافق", "المرافق", "مميزات", "ميزات",
                "واي فاي", "وايفاي", "انترنت", "مسبح", "موقف", "مواقف",
                "باقات", "الباقات"))
            {
                return "services";
            }

            // الموقع والاتجاهات: نلتقط الصيغ المباشرة والعامية، لكن لا نستخدم
            // كلمة "الوصول" وحدها حتى لا نخلطها مع وقت الوصول/الدخول.
            if (HasAny(
                "كيف اوصل لكم", "كيف اوصل عندكم", "كيف اصل لكم", "كيف اصل عندكم",
                "كيف اجيكم", "كيف اجي لكم", "كيف اجي عندكم", "كيف اروح لكم",
                "وين موقعكم", "فين موقعكم", "اين موقعكم", "وين مكانكم", "فين مكانكم",
                "وينكم", "فينكم", "موقعكم", "موقع المنشاه", "موقع المنشاه",
                "ماهو الموقع", "ما هو الموقع", "ما الموقع", "العنوان", "عنوانكم",
                "لوكيشن", "ارسل الموقع", "ارسل لي الموقع", "ابعث الموقع",
                "الخريطه", "خريطه الموقع", "الاتجاهات", "طريق الوصول"))
            {
                return "location";
            }

            if (HasAny(
                "رقم التواصل", "رقم الطوارئ", "هاتف", "جوال", "تواصل",
                "البريد", "ايميل", "الايميل", "البريد الالكتروني"))
            {
                return "contact";
            }

            return null;
        }

        private async Task<string?> BuildAutomaticTopicReplyAsync(
            string topic,
            ConversationContext conversation,
            CancellationToken cancellationToken)
        {
            switch (topic)
            {
                case "price":
                    return await BuildPriceReplyAsync(
                        conversation.Place,
                        cancellationToken);

                case "availability":
                    return BuildAvailabilityReply(conversation.Place);

                case "cancellation":
                    return BuildCancellationReply(conversation.Policy);

                case "checkinout":
                    return BuildCheckInOutReply(
                        conversation.Policy,
                        conversation.Place);

                case "payment":
                    return BuildPaymentReply(
                        conversation.Policy,
                        conversation.Place);

                case "operation":
                    return BuildOperationReply(conversation.Policy);

                case "security":
                    return BuildSecurityReply(conversation.Policy);

                case "smoking":
                    return BuildSmokingReply(conversation.Policy);

                case "families":
                    return BuildFamiliesReply(conversation.Policy);

                case "parties":
                    return BuildPartiesReply(conversation.Policy);

                case "qat":
                    return BuildQatReply(conversation.Policy);

                case "rules":
                    return BuildFacilityRulesReply(
                        conversation.Policy,
                        conversation.Place.Terms);

                case "fees":
                    return BuildFeesReply(conversation.Policy);

                case "services":
                    return await BuildServicesReplyAsync(
                        conversation.Place,
                        cancellationToken);

                case "location":
                    return BuildLocationReply(conversation.Place);

                case "contact":
                    return BuildContactReply(conversation.Policy, conversation.Place);

                default:
                    return null;
            }
        }

        private async Task<string> BuildPriceReplyAsync(
            Place place,
            CancellationToken cancellationToken)
        {
            List<decimal> roomPrices = await _context.Rooms
                .AsNoTracking()
                .Where(room =>
                    room.PlaceId == place.PlaceId &&
                    room.IsAvailable != false &&
                    room.Price > 0)
                .Select(room => room.Price)
                .ToListAsync(cancellationToken);

            List<decimal> packagePrices = await _context.PlacePackages
                .AsNoTracking()
                .Where(package =>
                    package.PlaceId == place.PlaceId &&
                    package.IsActive != false &&
                    package.Price > 0)
                .Select(package => package.Price)
                .ToListAsync(cancellationToken);

            List<decimal> prices = roomPrices
                .Concat(packagePrices)
                .Where(price => price > 0)
                .ToList();

            if (prices.Count == 0 && place.Price > 0)
                prices.Add(place.Price);

            if (prices.Count == 0)
            {
                return "لا يوجد سعر منشور يمكنني تأكيده الآن. افتح تفاصيل المنشأة أو اكتب للمالك وسيعطيك السعر المناسب دون تخمين.";
            }

            decimal minimum = prices.Min();
            decimal maximum = prices.Max();

            if (minimum == maximum)
            {
                return $"السعر الأساسي حاليًا هو {minimum:N0}. قد يتغير السعر النهائي حسب الوحدة أو الباقة والتاريخ والعروض، وسيظهر لك السعر النهائي قبل تأكيد الحجز.";
            }

            return $"الأسعار الأساسية حاليًا تبدأ من {minimum:N0} وتصل إلى {maximum:N0} حسب الوحدة أو الباقة. السعر النهائي قد يتغير حسب التاريخ والعروض وسيظهر لك قبل تأكيد الحجز.";
        }

        private static string BuildAvailabilityReply(Place place)
        {
            if (place.IsAvailable == false)
            {
                return "المنشأة لا تستقبل حجوزات جديدة حاليًا حسب حالة التوفر الحالية.";
            }

            return "التوفر يتغير حسب تاريخ بداية الحجز ونهايته والوحدة المطلوبة. حدّد التاريخ من صفحة الحجز ليتم فحص التوفر الفعلي؛ لا أؤكد وجود وحدة بدون فترة محددة حتى لا أعطيك معلومة غير دقيقة.";
        }

        private static string BuildCancellationReply(PlacePolicy? policy)
        {
            if (policy == null)
                return "لم تُحفظ سياسة إلغاء يمكنني تأكيدها لهذه المنشأة بعد. يمكنك إرسال سؤالك للمالك مباشرة.";

            List<string> parts = new List<string>();

            if (policy.FreeCancellationHours > 0)
            {
                parts.Add($"الإلغاء المجاني متاح قبل موعد الحجز بـ {policy.FreeCancellationHours} ساعة");
            }

            if (policy.ImmediateGracePeriod == true)
            {
                parts.Add("وتوجد فترة تراجع مجانية خلال أول 24 ساعة من الدفع وفق شروط السياسة");
            }

            string penalty = BuildCancellationPenaltyText(policy);
            if (!string.IsNullOrWhiteSpace(penalty))
                parts.Add(penalty);

            if (policy.ConfiscateOnNoShow == true)
            {
                parts.Add($"وفي حالة عدم الحضور يمكن تطبيق المصادرة بعد مهلة الحضور المحددة ({Math.Max(0, policy.NoShowGraceMinutes)} دقيقة)");
            }

            if (policy.RefundProcessingDays > 0)
            {
                parts.Add($"ومدة معالجة الاسترداد المتوقعة {policy.RefundProcessingDays} يوم عمل");
            }

            if (string.Equals(
                policy.RefundTransferFee,
                "CustomerPays",
                StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("ورسوم التحويل يتحملها الزبون");
            }
            else if (string.Equals(
                policy.RefundTransferFee,
                "FacilityPays",
                StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("ورسوم التحويل تتحملها المنشأة");
            }

            return parts.Count > 0
                ? string.Join("، ", parts) + "."
                : "سياسة الإلغاء محفوظة، لكن لا توجد تفاصيل كافية لإعطاء جواب دقيق تلقائيًا. يمكنك إرسال سؤالك للمالك.";
        }

        private static string BuildCancellationPenaltyText(PlacePolicy policy)
        {
            string type = policy.PenaltyType?.Trim() ?? string.Empty;

            if (string.Equals(type, "LoseDeposit", StringComparison.OrdinalIgnoreCase))
                return "وبعد انتهاء السماح قد تتم مصادرة العربون بالكامل";

            if (string.Equals(type, "LoseFirstNight", StringComparison.OrdinalIgnoreCase))
                return "وبعد انتهاء السماح قد تُخصم قيمة الليلة الأولى";

            if (string.Equals(type, "DeductPercentage", StringComparison.OrdinalIgnoreCase) &&
                policy.PenaltyValue > 0)
            {
                return $"وبعد انتهاء السماح تُطبق غرامة بنسبة {policy.PenaltyValue:N0}%";
            }

            if (string.Equals(type, "DeductFixed", StringComparison.OrdinalIgnoreCase) &&
                policy.PenaltyValue > 0)
            {
                return $"وبعد انتهاء السماح تُطبق غرامة ثابتة بقيمة {policy.PenaltyValue:N0}";
            }

            return string.Empty;
        }

        private static string BuildCheckInOutReply(
            PlacePolicy? policy,
            Place place)
        {
            string checkIn = FirstNonEmpty(policy?.CheckInTime, place.CheckInTime) ?? string.Empty;
            string checkOut = FirstNonEmpty(policy?.CheckOutTime, place.CheckOutTime) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(checkIn) && string.IsNullOrWhiteSpace(checkOut))
            {
                return "لم تُحدد أوقات الدخول والخروج في النظام لهذه المنشأة بعد. يمكنك سؤال المالك مباشرة.";
            }

            if (!string.IsNullOrWhiteSpace(checkIn) && !string.IsNullOrWhiteSpace(checkOut))
                return $"وقت الدخول/الاستلام المعتمد حاليًا {checkIn}، ووقت الخروج/التسليم {checkOut}.";

            return !string.IsNullOrWhiteSpace(checkIn)
                ? $"وقت الدخول/الاستلام المعتمد حاليًا {checkIn}. وقت الخروج غير محدد في السياسة الحالية."
                : $"وقت الخروج/التسليم المعتمد حاليًا {checkOut}. وقت الدخول غير محدد في السياسة الحالية.";
        }

        private static string BuildPaymentReply(
            PlacePolicy? policy,
            Place place)
        {
            List<string> parts = new List<string>();

            if (policy != null)
            {
                List<string> depositRules = ParseDepositPolicyDescriptions(
                    policy.DepositPoliciesJson);

                if (depositRules.Count > 0)
                    parts.Add(string.Join("، ", depositRules.Take(2)));

                if (policy.PaymentTimeout > 0)
                    parts.Add($"مهلة السداد الحالية {policy.PaymentTimeout} ساعة");
            }

            if (parts.Count == 0 && place.IsDepositRequired && place.DepositAmount > 0)
            {
                string currency = string.IsNullOrWhiteSpace(place.DepositCurrency)
                    ? string.Empty
                    : $" {place.DepositCurrency.Trim()}";

                parts.Add($"العربون المطلوب حاليًا {place.DepositAmount:N0}{currency}");
            }

            if (place.AllowCashOnArrival == true || place.IsCashOnArrival == true)
                parts.Add("والدفع النقدي عند الوصول متاح حسب إعدادات المنشأة");

            return parts.Count > 0
                ? string.Join("، ", parts) + "."
                : "لا توجد تفاصيل دفع مؤكدة يمكنني عرضها تلقائيًا الآن. ستظهر لك طريقة الدفع المطلوبة ضمن خطوات الحجز، ويمكنك سؤال المالك عند الحاجة.";
        }

        private static string BuildOperationReply(PlacePolicy? policy)
        {
            if (policy == null)
                return "لم تُحفظ إعدادات تشغيل يمكنني تأكيدها لهذه المنشأة بعد. يمكنك سؤال المالك مباشرة.";

            List<string> parts = new List<string>();
            string mode = (policy.OperationSystemMode ?? string.Empty).Trim();

            if (string.Equals(mode, "FullDay", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("نظام التشغيل الحالي هو اليوم الكامل");

                if (!string.IsNullOrWhiteSpace(policy.CheckInTime))
                    parts.Add($"وقت الدخول/الاستلام {policy.CheckInTime}");

                if (!string.IsNullOrWhiteSpace(policy.CheckOutTime))
                    parts.Add($"وقت الخروج/التسليم {policy.CheckOutTime}");
            }
            else if (string.Equals(mode, "Shifts", StringComparison.OrdinalIgnoreCase))
            {
                List<string> shifts = ParseShiftDescriptions(policy.ShiftsJson);
                parts.Add(shifts.Count > 0
                    ? "نظام التشغيل الحالي يعتمد على الفترات: " + string.Join("، ", shifts.Take(5))
                    : "نظام التشغيل الحالي يعتمد على الفترات، لكن تفاصيل الفترات غير مكتملة");
            }
            else if (!string.IsNullOrWhiteSpace(mode))
            {
                parts.Add($"نظام التشغيل الحالي هو {mode}");
            }

            if (policy.BufferTimeHours > 0)
                parts.Add($"الفاصل التشغيلي بين الحجوزات {policy.BufferTimeHours} ساعة");

            if (policy.MinimumNoticeHours > 0)
                parts.Add($"أقل مهلة قبل الحجز {policy.MinimumNoticeHours} ساعة");

            if (policy.MaximumHorizonMonths > 0)
                parts.Add($"ويمكن الحجز مقدمًا حتى {policy.MaximumHorizonMonths} شهر");

            List<string> closedDays = ParseClosedDays(policy.ClosedDaysCsv);
            if (closedDays.Count > 0)
                parts.Add("أيام الإغلاق الحالية: " + string.Join("، ", closedDays));

            return parts.Count > 0
                ? string.Join("، ", parts) + "."
                : "إعدادات التشغيل موجودة، لكن لا توجد تفاصيل كافية لإعطاء إجابة دقيقة حاليًا. يمكنك سؤال المالك.";
        }

        private static string BuildSecurityReply(PlacePolicy? policy)
        {
            if (policy == null)
                return "لم تُحفظ متطلبات ضمان يمكنني تأكيدها لهذه المنشأة بعد. يمكنك سؤال المالك مباشرة.";

            List<string> guarantees = ParseSimpleStringList(policy.SecurityPoliciesJson);

            if (guarantees.Count == 0)
                return "لا توجد ضمانات أو متطلبات سلامة مفصلة حاليًا في سياسة المنشأة.";

            return "الضمانات والمتطلبات الحالية: " +
                   string.Join("، ", guarantees.Take(8)) + ".";
        }

        private static string BuildSmokingReply(PlacePolicy? policy)
        {
            if (policy == null)
                return "لا أستطيع تأكيد سياسة التدخين الآن. يمكنك سؤال المالك مباشرة.";

            return policy.AllowSmoking
                ? "نعم، المنشأة تسمح بالتدخين."
                : "لا، المنشأة لا تسمح بالتدخين.";
        }

        private static string BuildFamiliesReply(PlacePolicy? policy)
        {
            if (policy == null)
                return "لا أستطيع تأكيد سياسة العائلات الآن. يمكنك سؤال المالك مباشرة.";

            return policy.ForFamiliesOnly
                ? "المنشأة مخصصة للعائلات فقط."
                : "المنشأة ليست مخصصة للعائلات فقط.";
        }

        private static string BuildPartiesReply(PlacePolicy? policy)
        {
            if (policy == null)
                return "لا أستطيع تأكيد سياسة الحفلات والمناسبات الآن. يمكنك سؤال المالك مباشرة.";

            return policy.AllowParties
                ? "نعم، المنشأة تسمح بالحفلات والمناسبات."
                : "لا، المنشأة لا تسمح بالحفلات والمناسبات.";
        }

        private static string BuildQatReply(PlacePolicy? policy)
        {
            if (policy == null)
                return "لا أستطيع تأكيد سياسة القات الآن. يمكنك سؤال المالك مباشرة.";

            return policy.AllowQatInside
                ? "نعم، المنشأة تسمح بالقات داخل المنشأة."
                : "لا، المنشأة لا تسمح بالقات داخل المنشأة.";
        }

        private const string CustomHouseRulePrefix = "قاعدة إضافية:";

        private static string? BuildCustomHouseRuleReply(
            string question,
            string? terms)
        {
            List<string> rules = ExtractCustomHouseRules(terms);
            if (rules.Count == 0)
                return null;

            HashSet<string> questionTokens = BuildRuleMatchTokens(question);
            if (questionTokens.Count == 0)
                return null;

            var matches = rules
                .Select(rule => new
                {
                    Rule = rule,
                    Tokens = BuildRuleMatchTokens(rule)
                })
                .Select(item => new
                {
                    item.Rule,
                    Shared = item.Tokens.Count(token => questionTokens.Contains(token)),
                    HasStrongSharedToken = item.Tokens.Any(token =>
                        token.Length >= 5 && questionTokens.Contains(token))
                })
                .Where(item => item.Shared >= 2 || item.HasStrongSharedToken)
                .OrderByDescending(item => item.Shared)
                .ThenByDescending(item => item.Rule.Length)
                .Take(3)
                .Select(item => item.Rule)
                .ToList();

            if (matches.Count == 0)
                return null;

            return matches.Count == 1
                ? matches[0].Trim().TrimEnd('،', '.', '؛') + "."
                : string.Join("، ", matches.Select(rule =>
                    rule.Trim().TrimEnd('،', '.', '؛'))) + ".";
        }

        private static List<string> ExtractCustomHouseRules(string? terms)
        {
            if (string.IsNullOrWhiteSpace(terms))
                return new List<string>();

            return terms
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith(CustomHouseRulePrefix, StringComparison.Ordinal))
                .Select(line => line.Substring(CustomHouseRulePrefix.Length).Trim())
                .Where(rule => !string.IsNullOrWhiteSpace(rule))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();
        }

        private static HashSet<string> BuildRuleMatchTokens(string? value)
        {
            string normalized = NormalizeArabicIntentText(value);
            if (string.IsNullOrWhiteSpace(normalized))
                return new HashSet<string>(StringComparer.Ordinal);

            HashSet<string> ignored = new HashSet<string>(StringComparer.Ordinal)
            {
                "هل", "ما", "ماذا", "ماهي", "ماهو", "ايش", "كيف", "في", "فيه",
                "عندكم", "معكم", "المنشاه", "منشاه", "داخل", "خارج", "قاعده",
                "قواعد", "سياسه", "سياسات", "يسمح", "مسموح", "ممنوع", "ممنوعه",
                "مسموحه", "عادي", "يمكن", "اقدر", "نقدر", "ممكن", "او", "و"
            };

            return normalized
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeRuleMatchToken)
                .Where(token => token.Length >= 3 && !ignored.Contains(token))
                .ToHashSet(StringComparer.Ordinal);
        }

        private static string NormalizeRuleMatchToken(string token)
        {
            string value = token?.Trim() ?? string.Empty;

            // نفصل بعض حروف الجر/العطف الشائعة المتصلة بأل التعريف:
            // بالحيوانات / للحيوانات / والحيوانات -> حيوانات.
            foreach (string prefix in new[] { "بال", "وال", "فال" })
            {
                if (value.Length > prefix.Length + 3 &&
                    value.StartsWith(prefix, StringComparison.Ordinal))
                {
                    value = value.Substring(prefix.Length);
                    break;
                }
            }

            if (value.Length > 4 && value.StartsWith("لل", StringComparison.Ordinal))
                value = value.Substring(2);

            if (value.Length > 4 && value.StartsWith("ال", StringComparison.Ordinal))
                value = value.Substring(2);

            // توحيد صيغ شائعة بسيطة حتى تتطابق العامية مع صياغة القاعدة.
            if (value.EndsWith("ات", StringComparison.Ordinal) && value.Length > 5)
                value = value.Substring(0, value.Length - 2);

            return value;
        }

        private static string BuildFacilityRulesReply(
            PlacePolicy? policy,
            string? terms)
        {
            List<string> rules = new List<string>();

            if (policy != null)
            {
                rules.Add(policy.ForFamiliesOnly
                    ? "المنشأة مخصصة للعائلات فقط"
                    : "المنشأة ليست مقيدة بالعائلات فقط");
                rules.Add(policy.AllowSmoking
                    ? "التدخين مسموح"
                    : "التدخين غير مسموح");
                rules.Add(policy.AllowParties
                    ? "الحفلات والمناسبات مسموحة"
                    : "الحفلات والمناسبات غير مسموحة");
                rules.Add(policy.AllowQatInside
                    ? "القات مسموح داخل المنشأة"
                    : "القات غير مسموح داخل المنشأة");
            }

            rules.AddRange(ExtractCustomHouseRules(terms));

            return rules.Count > 0
                ? string.Join("، ", rules.Distinct(StringComparer.OrdinalIgnoreCase)) + "."
                : "لا توجد قواعد واضحة يمكنني تأكيدها الآن. يمكنك سؤال المالك مباشرة.";
        }

        private static string BuildFeesReply(PlacePolicy? policy)
        {
            if (policy == null)
                return "لا توجد تفاصيل رسوم مؤكدة يمكنني عرضها حاليًا. يمكنك سؤال المالك مباشرة.";

            List<string> parts = new List<string>();

            if (policy.CleaningFee > 0)
                parts.Add($"رسوم التنظيف حاليًا {policy.CleaningFee:N0}");

            if (policy.TaxPercentage > 0)
                parts.Add($"نسبة الضريبة حاليًا {policy.TaxPercentage:N0}%");

            return parts.Count > 0
                ? string.Join("، ", parts) + ". السعر النهائي والرسوم المستحقة تظهر لك قبل تأكيد الحجز."
                : "لا توجد رسوم تنظيف أو نسبة ضريبة إضافية حاليًا في سياسة المنشأة.";
        }

        private async Task<string> BuildServicesReplyAsync(
            Place place,
            CancellationToken cancellationToken)
        {
            List<string> services = await _context.PlaceAdditionalServices
                .AsNoTracking()
                .Where(item => item.PlaceId == place.PlaceId && item.IsActive != false)
                .OrderBy(item => item.ServiceName)
                .Select(item => item.ServiceName)
                .Take(8)
                .ToListAsync(cancellationToken);

            List<string> amenities = await _context.PlaceAmenities
                .AsNoTracking()
                .Where(item => item.PlaceId == place.PlaceId && item.IsActive != false)
                .OrderBy(item => item.AmenityName)
                .Select(item => item.AmenityName)
                .Take(8)
                .ToListAsync(cancellationToken);

            List<string> packages = await _context.PlacePackages
                .AsNoTracking()
                .Where(item => item.PlaceId == place.PlaceId && item.IsActive != false)
                .OrderBy(item => item.PackageName)
                .Select(item => item.PackageName)
                .Take(6)
                .ToListAsync(cancellationToken);

            List<string> parts = new List<string>();
            if (amenities.Count > 0)
                parts.Add("المرافق: " + string.Join("، ", amenities));
            if (services.Count > 0)
                parts.Add("الخدمات: " + string.Join("، ", services));
            if (packages.Count > 0)
                parts.Add("الباقات: " + string.Join("، ", packages));

            return parts.Count > 0
                ? string.Join(". ", parts) + "."
                : "لا توجد خدمات أو مرافق أو باقات مفعلة يمكنني تأكيدها حاليًا في النظام.";
        }

        private static string BuildLocationReply(Place place)
        {
            string address = place.AddressDetail?.Trim() ?? string.Empty;
            string coordinates = place.Coordinates?.Trim() ?? string.Empty;

            string mapQuery = string.Empty;

            if (!string.IsNullOrWhiteSpace(coordinates))
            {
                // يدعم الصيغة الشائعة "lat,lng" وكذلك أي نص إحداثيات محفوظ سابقًا.
                // إذا كانت القيمة رابطًا جاهزًا نستخدمه كما هو.
                if (Uri.TryCreate(coordinates, UriKind.Absolute, out Uri? existingUrl) &&
                    (existingUrl.Scheme == Uri.UriSchemeHttp ||
                     existingUrl.Scheme == Uri.UriSchemeHttps))
                {
                    List<string> directParts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(address))
                        directParts.Add($"📍 {address}");
                    directParts.Add(coordinates);
                    return string.Join(Environment.NewLine, directParts);
                }

                mapQuery = coordinates;
            }
            else if (!string.IsNullOrWhiteSpace(address))
            {
                mapQuery = address;
            }

            if (string.IsNullOrWhiteSpace(mapQuery))
            {
                return "لم يتم تحديد موقع المنشأة على الخريطة بعد. يمكنك سؤال المالك مباشرة عن الاتجاهات.";
            }

            string mapUrl =
                "https://www.google.com/maps/search/?api=1&query=" +
                Uri.EscapeDataString(mapQuery);

            List<string> parts = new List<string>
            {
                "📍 موقع المنشأة"
            };

            if (!string.IsNullOrWhiteSpace(address))
                parts.Add(address);

            parts.Add(mapUrl);
            return string.Join(Environment.NewLine, parts);
        }

        private static string BuildContactReply(PlacePolicy? policy, Place place)
        {
            string phone = FirstNonEmpty(policy?.EmergencyContact, place.PhoneNumber) ?? string.Empty;
            string email = FirstNonEmpty(policy?.ContactEmail, place.EmailAddress) ?? string.Empty;

            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(phone))
                parts.Add($"رقم التواصل {phone}");
            if (!string.IsNullOrWhiteSpace(email))
                parts.Add($"البريد الإلكتروني {email}");

            return parts.Count > 0
                ? string.Join("، ", parts) + "."
                : "لا توجد وسيلة تواصل إضافية منشورة يمكنني تأكيدها حاليًا.";
        }

        private static List<string> ParseSimpleStringList(string? jsonText)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrWhiteSpace(jsonText))
                return result;

            try
            {
                using JsonDocument document = JsonDocument.Parse(jsonText);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    return result;

                foreach (JsonElement item in document.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                        continue;

                    string value = item.GetString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(value))
                        result.Add(value);
                }
            }
            catch (JsonException)
            {
                // لا نخمن عند تعذر قراءة البيانات.
            }

            return result;
        }

        private static List<string> ParseShiftDescriptions(string? jsonText)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrWhiteSpace(jsonText))
                return result;

            try
            {
                using JsonDocument document = JsonDocument.Parse(jsonText);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    return result;

                foreach (JsonElement shift in document.RootElement.EnumerateArray())
                {
                    if (shift.ValueKind != JsonValueKind.Object)
                        continue;

                    string name = GetJsonString(shift, "name", "Name");
                    string from = GetJsonString(shift, "from", "From");
                    string to = GetJsonString(shift, "to", "To");

                    if (string.IsNullOrWhiteSpace(name) &&
                        string.IsNullOrWhiteSpace(from) &&
                        string.IsNullOrWhiteSpace(to))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
                        result.Add($"{(string.IsNullOrWhiteSpace(name) ? "فترة" : name)} من {from} إلى {to}");
                    else if (!string.IsNullOrWhiteSpace(name))
                        result.Add(name);
                }
            }
            catch (JsonException)
            {
                // لا نخمن عند تعذر قراءة البيانات.
            }

            return result;
        }

        private static List<string> ParseClosedDays(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return new List<string>();

            Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Sunday"] = "الأحد",
                ["Monday"] = "الاثنين",
                ["Tuesday"] = "الثلاثاء",
                ["Wednesday"] = "الأربعاء",
                ["Thursday"] = "الخميس",
                ["Friday"] = "الجمعة",
                ["Saturday"] = "السبت"
            };

            return csv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(day => day.Trim())
                .Where(day => day.Length > 0)
                .Select(day => labels.TryGetValue(day, out string? label) ? label : day)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> ParseDepositPolicyDescriptions(string? jsonText)
        {
            List<string> result = new List<string>();

            if (string.IsNullOrWhiteSpace(jsonText))
                return result;

            try
            {
                using JsonDocument document = JsonDocument.Parse(jsonText);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    return result;

                foreach (JsonElement item in document.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    string value = GetJsonString(item, "val", "Val");
                    string type = GetJsonString(item, "type", "Type");
                    string description = GetJsonString(item, "desc", "Desc", "description", "Description");

                    if (string.IsNullOrWhiteSpace(description))
                        continue;

                    string prefix = string.IsNullOrWhiteSpace(value)
                        ? string.Empty
                        : type == "%"
                            ? $"{value}% "
                            : $"{value} ";

                    result.Add((prefix + description).Trim());
                }
            }
            catch (JsonException)
            {
                // لا نعيد نصًا غير موثوق عند فشل القراءة.
            }

            return result;
        }

        private static bool IsWithinWorkingHours(PlacePolicy? policy, DateTime now)
        {
            if (policy == null)
                return true;

            string closedDays = policy.ClosedDaysCsv ?? string.Empty;
            if (closedDays
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(day => day.Trim())
                .Any(day => string.Equals(
                    day,
                    now.DayOfWeek.ToString(),
                    StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // في نظام اليوم الكامل لا توجد لدينا ساعات دعم مستقلة في قاعدة البيانات،
            // لذلك لا نخمن باستخدام وقت Check-in/Check-out كساعات خدمة عملاء.
            if (!string.Equals(
                policy.OperationSystemMode,
                "Shifts",
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(policy.ShiftsJson))
                return true;

            try
            {
                using JsonDocument document = JsonDocument.Parse(policy.ShiftsJson);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    return true;

                TimeSpan current = now.TimeOfDay;
                bool foundValidShift = false;

                foreach (JsonElement shift in document.RootElement.EnumerateArray())
                {
                    if (shift.ValueKind != JsonValueKind.Object)
                        continue;

                    string fromText = GetJsonString(shift, "from", "From");
                    string toText = GetJsonString(shift, "to", "To");

                    if (!TimeSpan.TryParse(fromText, out TimeSpan from) ||
                        !TimeSpan.TryParse(toText, out TimeSpan to))
                    {
                        continue;
                    }

                    foundValidShift = true;

                    bool inside = from <= to
                        ? current >= from && current <= to
                        : current >= from || current <= to;

                    if (inside)
                        return true;
                }

                return !foundValidShift;
            }
            catch (JsonException)
            {
                return true;
            }
        }

        private static string GetJsonString(
            JsonElement element,
            params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (element.TryGetProperty(propertyName, out JsonElement property) &&
                    property.ValueKind == JsonValueKind.String)
                {
                    return property.GetString()?.Trim() ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static string NormalizeArabicIntentText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string text = value
                .Trim()
                .ToLowerInvariant()
                .Replace("ـ", string.Empty)
                .Replace('أ', 'ا')
                .Replace('إ', 'ا')
                .Replace('آ', 'ا')
                .Replace('ى', 'ي')
                .Replace('ة', 'ه');

            text = Regex.Replace(text, @"[\u064B-\u065F\u0670]", string.Empty);
            text = Regex.Replace(text, @"[^\p{L}\p{Nd}]+", " ");
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        // =====================================================================
        // أدوات العرض والتحقق
        // =====================================================================

        private static string GetMessagePreview(Chat message)
        {
            string text = NormalizeMessageText(message.MessageText);

            if (text.StartsWith("[REPLY|", StringComparison.Ordinal))
            {
                int closingBracketIndex = text.IndexOf(']');
                if (closingBracketIndex >= 0)
                    text = text.Substring(closingBracketIndex + 1).Trim();
            }

            if (string.IsNullOrWhiteSpace(text) &&
                !string.IsNullOrWhiteSpace(message.AttachmentUrl))
            {
                return "📎 مرفق";
            }

            text = Regex.Replace(text, @"\s+", " ").Trim();

            return text.Length > 90
                ? text.Substring(0, 90) + "…"
                : text;
        }

        private static string GetDisplayName(User user, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(user.FullName))
                return user.FullName.Trim();

            if (!string.IsNullOrWhiteSpace(user.Username))
                return user.Username.Trim();

            return fallback;
        }

        private static string GetInitial(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim().Substring(0, 1).ToUpperInvariant();
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static string FormatChatTime(DateTime? value)
        {
            return value.HasValue
                ? value.Value
                    .ToString("hh:mm tt")
                    .Replace("AM", "ص")
                    .Replace("PM", "م")
                : string.Empty;
        }

        private static string BuildPresenceText(User owner)
        {
            if (owner.IsOnline)
                return "متصل الآن";

            if (!owner.LastSeen.HasValue)
                return "آخر ظهور: غير معروف";

            TimeSpan difference = DateTime.Now - owner.LastSeen.Value;

            if (difference.TotalMinutes < 1)
                return "الآن";
            if (difference.TotalMinutes < 60)
                return $"منذ {Math.Max(1, difference.Minutes)} دقيقة";
            if (difference.TotalHours < 24)
                return $"منذ {Math.Max(1, difference.Hours)} ساعة";
            if (difference.TotalDays < 7)
                return $"منذ {Math.Max(1, difference.Days)} يوم";

            return owner.LastSeen.Value.ToString("yyyy/MM/dd HH:mm");
        }

        private static bool IsPublicPlaceAvailableForNewConversation(Place place)
        {
            /*
             * الحالة المعتمدة في مشروع المنشآت الحالي هي Active.
             * أبقينا Approved للتوافق مع أي بيانات قديمة فقط.
             * IsAvailable يخص استقبال حجوزات جديدة ولا يمنع الاستفسارات.
             */
            return string.Equals(
                       place.Status,
                       "Active",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       place.Status,
                       "Approved",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSystemAccountUnavailable(string? accountState)
        {
            return string.Equals(accountState, "Blocked", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(accountState, "Suspended", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(accountState, "Inactive", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(accountState, "Disabled", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(accountState, "Deleted", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddOwnerPlaceLink(
            IDictionary<int, HashSet<int>> destination,
            int ownerId,
            int placeId)
        {
            if (!destination.TryGetValue(ownerId, out HashSet<int>? placeIds))
            {
                placeIds = new HashSet<int>();
                destination[ownerId] = placeIds;
            }

            placeIds.Add(placeId);
        }

        private static int? ResolveLegacyPlaceId(
            int ownerId,
            IReadOnlyDictionary<int, HashSet<int>> relationshipPlacesByOwner,
            IReadOnlyCollection<Place> ownerPlaces)
        {
            if (relationshipPlacesByOwner.TryGetValue(ownerId, out HashSet<int>? relatedPlaceIds) &&
                relatedPlaceIds.Count == 1)
            {
                return relatedPlaceIds.First();
            }

            List<int> ownedPlaceIds = ownerPlaces
                .Where(place => place.OwnerId == ownerId)
                .Select(place => place.PlaceId)
                .Distinct()
                .Take(2)
                .ToList();

            return ownedPlaceIds.Count == 1
                ? ownedPlaceIds[0]
                : null;
        }

        private static void TryDeletePhysicalFile(string physicalPath)
        {
            try
            {
                if (System.IO.File.Exists(physicalPath))
                    System.IO.File.Delete(physicalPath);
            }
            catch
            {
                // لا نخفي الخطأ الأساسي بسبب تعذر تنظيف ملف مؤقت.
            }
        }

        private bool TryGetCustomerId(out int customerId)
        {
            string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out customerId) && customerId > 0;
        }

        private IActionResult JsonFailure(string message)
        {
            return Json(new
            {
                success = false,
                message
            });
        }

        private sealed class ConversationContext
        {
            public ConversationContext(
                Place place,
                User owner,
                PlacePolicy? policy,
                bool hasBooking,
                bool hasPreviousChat,
                bool includeLegacyMessages)
            {
                Place = place;
                Owner = owner;
                Policy = policy;
                HasBooking = hasBooking;
                HasPreviousChat = hasPreviousChat;
                IncludeLegacyMessages = includeLegacyMessages;
            }

            public Place Place { get; }
            public User Owner { get; }
            public PlacePolicy? Policy { get; }
            public bool HasBooking { get; }
            public bool HasPreviousChat { get; }
            public bool IncludeLegacyMessages { get; }
        }
    }
}
