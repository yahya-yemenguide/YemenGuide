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
                welcomeMessage = conversation.Policy?.WelcomeMessage ?? string.Empty,
                offlineReplyEnabled = conversation.Policy?.OfflineReplyToggle ?? false,
                offlineMessage = conversation.Policy?.OfflineMessage ?? string.Empty,
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
                        IsRead = false,
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
                            IsRead = false,
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

            return Json(new
            {
                success = true,
                placeId = conversation.Place.PlaceId,
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
                Text = NormalizeMessageText(message.MessageText),
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
