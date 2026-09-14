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
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Hubs;
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    [Area("Owner")]
    [Authorize(Roles = "Owner,Admin")]
    [AuthorizeActiveOwner]
    public class ChatController : Controller
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

        public ChatController(
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
        // الصفحة الرئيسية للدردشة
        // =====================================================================

        [HttpGet]
        public async Task<IActionResult> ChatInterface(CancellationToken cancellationToken)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized();

            Place? activePlace = await ResolveActivePlaceAsync(ownerId, cancellationToken);

            List<ChatContactDto> contacts = activePlace == null
                ? new List<ChatContactDto>()
                : await BuildContactsAsync(ownerId, activePlace, cancellationToken);

            PlacePolicy? policy = activePlace == null
                ? null
                : await _context.PlacePolicies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        item => item.PlaceId == activePlace.PlaceId,
                        cancellationToken);

            ViewBag.ActiveChatPlaceId = activePlace?.PlaceId ?? 0;
            ViewBag.ActiveChatPlaceName = activePlace?.PlaceNameAndDesc ?? string.Empty;
            ViewBag.HasActiveChatPlace = activePlace != null;
            ViewBag.IsChatEnabled = activePlace != null && policy?.IsChatEnabled != false;
            ViewBag.WelcomeMessage = policy?.WelcomeMessage ?? string.Empty;
            ViewBag.OfflineReplyEnabled = policy?.OfflineReplyToggle ?? false;
            ViewBag.OfflineMessage = policy?.OfflineMessage ?? string.Empty;
            ViewBag.QuickRepliesJson = policy?.QuickRepliesJson ?? "[]";
            ViewBag.EmergencyContact = policy?.EmergencyContact ?? string.Empty;

            // OwnerChatPageModel ما زال مودل الصفحة الجامعة حتى لا تتعطل
            // تبويبة التقييمات المستقلة التي تُحمّل لاحقًا عبر AJAX.
            OwnerChatPageModel pageModel = new OwnerChatPageModel
            {
                ChatContacts = contacts
            };

            return View(pageModel);
        }

        private async Task<List<ChatContactDto>> BuildContactsAsync(
            int ownerId,
            Place activePlace,
            CancellationToken cancellationToken)
        {
            int activePlaceId = activePlace.PlaceId;

            var bookingRows = await _context.Bookings
                .AsNoTracking()
                .Where(booking => booking.PlaceId == activePlaceId)
                .Select(booking => new
                {
                    booking.BookingId,
                    booking.CustomerId,
                    booking.BookingState,
                    booking.StartDate,
                    booking.EndDate,
                    booking.CreatedAt
                })
                .ToListAsync(cancellationToken);

            Dictionary<int, HashSet<int>> relatedPlacesByCustomer =
                await BuildCustomerPlaceRelationshipsAsync(ownerId, cancellationToken);

            HashSet<int> legacyAllowedCustomerIds = relatedPlacesByCustomer
                .Where(item =>
                    item.Value.Count == 1 &&
                    item.Value.Contains(activePlaceId))
                .Select(item => item.Key)
                .ToHashSet();

            List<Chat> scopedChats = await _context.Chats
                .AsNoTracking()
                .Where(chat =>
                    !chat.IsReportMessage &&
                    chat.ReportId == null &&
                    (
                        (
                            chat.PlaceId == activePlaceId &&
                            (
                                (chat.SenderId == ownerId && !chat.IsDeletedForSender) ||
                                (chat.ReceiverId == ownerId && !chat.IsDeletedForReceiver)
                            )
                        )
                        ||
                        (
                            chat.PlaceId == null &&
                            (
                                (chat.SenderId == ownerId &&
                                 !chat.IsDeletedForSender &&
                                 legacyAllowedCustomerIds.Contains(chat.ReceiverId))
                                ||
                                (chat.ReceiverId == ownerId &&
                                 !chat.IsDeletedForReceiver &&
                                 legacyAllowedCustomerIds.Contains(chat.SenderId))
                            )
                        )
                    ))
                .OrderByDescending(chat => chat.SendTime)
                .ThenByDescending(chat => chat.MessageId)
                .ToListAsync(cancellationToken);

            HashSet<int> contactIds = bookingRows
                .Where(booking => booking.CustomerId.HasValue)
                .Select(booking => booking.CustomerId!.Value)
                .ToHashSet();

            foreach (Chat chat in scopedChats)
            {
                int customerId = chat.SenderId == ownerId
                    ? chat.ReceiverId
                    : chat.SenderId;

                if (customerId > 0 && customerId != ownerId)
                    contactIds.Add(customerId);
            }

            List<User> customers = await _context.Users
                .AsNoTracking()
                .Where(user =>
                    contactIds.Contains(user.UserId) &&
                    user.Role == "Customer")
                .ToListAsync(cancellationToken);

            var latestBookingByCustomer = bookingRows
                .Where(booking => booking.CustomerId.HasValue)
                .GroupBy(booking => booking.CustomerId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(booking => booking.CreatedAt ?? DateTime.MinValue)
                        .ThenByDescending(booking => booking.BookingId)
                        .First());

            var chatsByCustomer = scopedChats
                .GroupBy(chat => chat.SenderId == ownerId ? chat.ReceiverId : chat.SenderId)
                .ToDictionary(group => group.Key, group => group.ToList());

            List<ChatContactDto> result = new List<ChatContactDto>();

            foreach (User customer in customers)
            {
                chatsByCustomer.TryGetValue(customer.UserId, out List<Chat>? customerChats);
                latestBookingByCustomer.TryGetValue(customer.UserId, out var latestBooking);

                customerChats ??= new List<Chat>();
                Chat? lastMessage = customerChats.FirstOrDefault();

                int unreadCount = customerChats.Count(chat =>
                    chat.SenderId == customer.UserId &&
                    chat.ReceiverId == ownerId &&
                    !chat.IsRead);

                string customerName = GetDisplayName(customer, "عميل");
                bool hasBooking = latestBooking != null;
                bool hasConversation = lastMessage != null;

                result.Add(new ChatContactDto
                {
                    CustomerId = customer.UserId,
                    CustomerName = customerName,
                    ProfileImage = customer.ProfileImage,
                    InitialLetter = GetInitial(customerName, "ع"),
                    PlaceId = activePlaceId,
                    PlaceName = activePlace.PlaceNameAndDesc,
                    LastMessage = hasConversation
                        ? GetMessagePreview(lastMessage!)
                        : "ابدأ محادثة مع عميل الحجز",
                    LastMessageTime = lastMessage?.SendTime,
                    UnreadCount = unreadCount,
                    HasPreviousConversation = hasConversation,
                    HasBooking = hasBooking,
                    CanStartConversation = hasConversation || hasBooking,
                    LatestBookingId = latestBooking?.BookingId,
                    LatestBookingState = latestBooking?.BookingState,
                    LatestBookingStartDate = latestBooking?.StartDate,
                    LatestBookingEndDate = latestBooking?.EndDate,
                    IsOnline = customer.IsOnline,
                    LastSeen = customer.LastSeen,
                    AccountState = string.IsNullOrWhiteSpace(customer.AccountState)
                        ? "Active"
                        : customer.AccountState
                });
            }

            return result
                .OrderByDescending(contact => contact.UnreadCount > 0)
                .ThenByDescending(contact => contact.LastMessageTime ?? DateTime.MinValue)
                .ThenByDescending(contact => contact.LatestBookingId ?? 0)
                .ThenBy(contact => contact.CustomerName)
                .ToList();
        }

        // =====================================================================
        // قراءة رسائل محادثة واحدة
        // =====================================================================

        [HttpGet]
        public async Task<IActionResult> GetChatMessages(
            int customerId,
            CancellationToken cancellationToken)
        {
            if (!TryGetOwnerId(out int ownerId))
                return JsonFailure("انتهت الجلسة، يرجى تسجيل الدخول مجددًا.");

            Place? activePlace = await ResolveActivePlaceAsync(ownerId, cancellationToken);
            if (activePlace == null)
                return JsonFailure("لا توجد منشأة مرتبطة بحسابك.");

            if (!await CanOwnerContactCustomerAsync(
                    ownerId,
                    activePlace.PlaceId,
                    customerId,
                    cancellationToken))
            {
                return JsonFailure("هذا العميل غير مرتبط بالمنشأة المحددة.");
            }

            User? customer = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    user => user.UserId == customerId && user.Role == "Customer",
                    cancellationToken);

            if (customer == null)
                return JsonFailure("العميل غير موجود.");

            bool includeLegacy = await CanIncludeLegacyMessagesAsync(
                ownerId,
                customerId,
                activePlace.PlaceId,
                cancellationToken);

            List<Chat> rawMessages = await ScopedConversationQuery(
                    ownerId,
                    customerId,
                    activePlace.PlaceId,
                    includeLegacy,
                    includeDeletedForOwner: false)
                .OrderBy(chat => chat.SendTime)
                .ThenBy(chat => chat.MessageId)
                .ToListAsync(cancellationToken);

            List<Chat> unreadMessages = await ScopedConversationQuery(
                    ownerId,
                    customerId,
                    activePlace.PlaceId,
                    includeLegacy,
                    includeDeletedForOwner: true)
                .Where(chat =>
                    chat.SenderId == customerId &&
                    chat.ReceiverId == ownerId &&
                    !chat.IsRead &&
                    !chat.IsDeletedForReceiver)
                .ToListAsync(cancellationToken);

            if (unreadMessages.Count > 0)
            {
                foreach (Chat unreadMessage in unreadMessages)
                {
                    unreadMessage.IsRead = true;

                    if (!unreadMessage.PlaceId.HasValue && includeLegacy)
                        unreadMessage.PlaceId = activePlace.PlaceId;
                }

                await _context.SaveChangesAsync(cancellationToken);

                await _hubContext.Clients.User(customerId.ToString())
                    .SendAsync(
                        "MessagesRead",
                        new
                        {
                            placeId = activePlace.PlaceId,
                            readerId = ownerId,
                            messageIds = unreadMessages.Select(message => message.MessageId).ToArray()
                        },
                        cancellationToken);
            }

            bool hasConfirmedBooking = await _context.Bookings
                .AsNoTracking()
                .AnyAsync(booking =>
                    booking.CustomerId == customerId &&
                    booking.PlaceId == activePlace.PlaceId &&
                    booking.BookingState != "Cancelled" &&
                    booking.BookingState != "Pending" &&
                    booking.BookingState != "Rejected",
                    cancellationToken);

            var latestBooking = await _context.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.CustomerId == customerId &&
                    booking.PlaceId == activePlace.PlaceId)
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

            PlacePolicy? policy = await _context.PlacePolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.PlaceId == activePlace.PlaceId,
                    cancellationToken);

            var messages = rawMessages.Select(chat =>
            {
                string attachmentName = GetAttachmentDisplayName(chat.AttachmentUrl);
                string attachmentSize = GetAttachmentSize(chat.AttachmentUrl);
                string extension = Path.GetExtension(chat.AttachmentUrl ?? string.Empty);

                return new
                {
                    messageId = chat.MessageId,
                    senderId = chat.SenderId,
                    receiverId = chat.ReceiverId,
                    placeId = chat.PlaceId ?? activePlace.PlaceId,
                    text = chat.MessageText ?? string.Empty,
                    time = FormatChatTime(chat.SendTime),
                    isSentByMe = chat.SenderId == ownerId,
                    isRead = chat.IsRead,
                    isEdited = chat.IsEdited,
                    attachmentUrl = chat.AttachmentUrl ?? string.Empty,
                    attachmentName,
                    attachmentSize,
                    isImage = IsImageExtension(extension),
                    isVideo = IsVideoExtension(extension),
                    sendTimeRaw = chat.SendTime
                };
            }).ToList();

            return Json(new
            {
                success = true,
                data = messages,
                statusText = BuildPresenceText(customer),
                isOnline = customer.IsOnline,
                hasBooking = hasConfirmedBooking,
                activePlaceId = activePlace.PlaceId,
                chatEnabled = policy?.IsChatEnabled != false,
                quickRepliesJson = policy?.QuickRepliesJson ?? "[]",
                emergencyContact = policy?.EmergencyContact ?? string.Empty,
                latestBooking
            });
        }

        // =====================================================================
        // إرسال رسالة
        // =====================================================================

        [HttpPost]
        public async Task<IActionResult> SendMessage(
            [FromForm] int receiverId,
            [FromForm] string? messageText,
            [FromForm] string? quotedText,
            [FromForm] string? quotedSender,
            CancellationToken cancellationToken)
        {
            if (!TryGetOwnerId(out int ownerId))
                return JsonFailure("انتهت الجلسة، يرجى تسجيل الدخول مجددًا.");

            Place? activePlace = await ResolveActivePlaceAsync(ownerId, cancellationToken);
            if (activePlace == null)
                return JsonFailure("لا توجد منشأة مرتبطة بحسابك.");

            PlacePolicy? policy = await _context.PlacePolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.PlaceId == activePlace.PlaceId,
                    cancellationToken);

            if (policy?.IsChatEnabled == false)
                return JsonFailure("الدردشة متوقفة حاليًا في إعدادات المنشأة.");

            User? receiver = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    user => user.UserId == receiverId && user.Role == "Customer",
                    cancellationToken);

            if (receiver == null)
                return JsonFailure("العميل غير موجود.");

            if (IsSystemAccountUnavailable(receiver.AccountState))
                return JsonFailure("لا يمكن مراسلة هذا الحساب حاليًا.");

            if (!await CanOwnerContactCustomerAsync(
                    ownerId,
                    activePlace.PlaceId,
                    receiverId,
                    cancellationToken))
            {
                return JsonFailure("لا يمكنك مراسلة عميل غير مرتبط بالمنشأة المحددة.");
            }

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
                        SenderId = ownerId,
                        ReceiverId = receiverId,
                        PlaceId = activePlace.PlaceId,
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
                        string safeFileName = BuildSafeStoredFileName(file.FileName);
                        string physicalPath = Path.Combine(uploadsFolder, safeFileName);

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
                            SenderId = ownerId,
                            ReceiverId = receiverId,
                            PlaceId = activePlace.PlaceId,
                            MessageText = index == 0 ? finalMessage : string.Empty,
                            AttachmentUrl = $"/uploads/chats/{safeFileName}",
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
                {
                    TryDeletePhysicalFile(physicalPath);
                }

                throw;
            }

            // نفس أسلوب إشعار الدردشة الذي يصل للمالك عند رسالة العميل:
            // ننشئ الإشعار ونحفظه فعليًا، ونربطه بنفس المنشأة والمحادثة.
            try
            {
                string messagePreview;

                if (!string.IsNullOrWhiteSpace(normalizedText))
                {
                    messagePreview = normalizedText.Trim();
                    if (messagePreview.Length > 120)
                    {
                        messagePreview = messagePreview[..120] + "…";
                    }
                }
                else
                {
                    messagePreview = attachments.Count == 1
                        ? "أرسل لك صاحب المنشأة مرفقًا جديدًا."
                        : $"أرسل لك صاحب المنشأة {attachments.Count} مرفقات جديدة.";
                }

                await _notificationService.CreateAsync(
                    new CreateNotificationDto
                    {
                        UserId = receiverId,
                        NotificationTitle = "رسالة جديدة من المنشأة",
                        NotificationText = $"{activePlace.PlaceNameAndDesc}: {messagePreview}",
                        NotificationType = "Message",
                        TargetUrl = $"/Messages/MyMessages?placeId={activePlace.PlaceId}&withOwnerId={ownerId}",
                        ReferenceType = "Chat",
                        ReferenceId = messagesToSave.FirstOrDefault()?.MessageId
                    },
                    cancellationToken);
            }
            catch (Exception notificationException)
            {
                // فشل الإشعار لا يلغي الرسالة التي تم إرسالها وحفظها بنجاح.
                Console.WriteLine(
                    $"Customer chat notification error: {notificationException.Message}");
            }

            foreach (Chat chat in messagesToSave)
            {
                string attachmentName = GetAttachmentDisplayName(chat.AttachmentUrl);
                string attachmentSize = GetAttachmentSize(chat.AttachmentUrl);
                string extension = Path.GetExtension(chat.AttachmentUrl ?? string.Empty);

                await _hubContext.Clients.User(receiverId.ToString())
                    .SendAsync(
                        "ReceiveMessage",
                        new
                        {
                            messageId = chat.MessageId,
                            senderId = ownerId,
                            receiverId,
                            placeId = activePlace.PlaceId,
                            text = chat.MessageText ?? string.Empty,
                            time = FormatChatTime(chat.SendTime),
                            attachmentUrl = chat.AttachmentUrl ?? string.Empty,
                            attachmentName,
                            attachmentSize,
                            isImage = IsImageExtension(extension),
                            isVideo = IsVideoExtension(extension),
                            sendTimeRaw = chat.SendTime
                        },
                        cancellationToken);
            }

            return Json(new
            {
                success = true,
                placeId = activePlace.PlaceId,
                messageIds = messagesToSave.Select(message => message.MessageId).ToArray()
            });
        }

        // =====================================================================
        // تعديل وحذف الرسائل
        // =====================================================================

        [HttpPost]
        public async Task<IActionResult> EditMessage(
            int messageId,
            string? newText,
            CancellationToken cancellationToken)
        {
            if (!TryGetOwnerId(out int ownerId))
                return JsonFailure("انتهت الجلسة.");

            Place? activePlace = await ResolveActivePlaceAsync(ownerId, cancellationToken);
            if (activePlace == null)
                return JsonFailure("لا توجد منشأة مرتبطة بحسابك.");

            Chat? message = await _context.Chats
                .FirstOrDefaultAsync(
                    item =>
                        item.MessageId == messageId &&
                        item.SenderId == ownerId &&
                        !item.IsReportMessage &&
                        item.ReportId == null,
                    cancellationToken);

            if (message == null)
                return JsonFailure("الرسالة غير موجودة أو لا تملك صلاحية تعديلها.");

            if (!await IsMessageInsideActivePlaceAsync(
                    message,
                    ownerId,
                    activePlace.PlaceId,
                    cancellationToken))
            {
                return JsonFailure("الرسالة لا تخص المنشأة المحددة.");
            }

            if (message.SendTime.HasValue &&
                (DateTime.Now - message.SendTime.Value).TotalMinutes > 10)
            {
                return JsonFailure("انتهت مهلة التعديل المسموحة وهي 10 دقائق.");
            }

            string normalizedText = NormalizeMessageText(newText);
            if (string.IsNullOrWhiteSpace(normalizedText))
                return JsonFailure("لا يمكن حفظ نص فارغ.");

            if (normalizedText.Length > MaximumMessageLength)
                return JsonFailure($"الحد الأقصى لنص الرسالة هو {MaximumMessageLength} حرف.");

            message.MessageText = ReplaceMessageBodyPreservingQuote(
                message.MessageText,
                normalizedText);
            message.IsEdited = true;

            if (!message.PlaceId.HasValue)
                message.PlaceId = activePlace.PlaceId;

            await _context.SaveChangesAsync(cancellationToken);

            await _hubContext.Clients.User(message.ReceiverId.ToString())
                .SendAsync(
                    "MessageEdited",
                    new
                    {
                        messageId = message.MessageId,
                        placeId = activePlace.PlaceId,
                        text = message.MessageText
                    },
                    cancellationToken);

            return Json(new
            {
                success = true,
                message = "تم تعديل الرسالة بنجاح.",
                text = message.MessageText
            });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMessageForMe(
            int messageId,
            CancellationToken cancellationToken)
        {
            if (!TryGetOwnerId(out int ownerId))
                return JsonFailure("انتهت الجلسة.");

            Place? activePlace = await ResolveActivePlaceAsync(ownerId, cancellationToken);
            if (activePlace == null)
                return JsonFailure("لا توجد منشأة مرتبطة بحسابك.");

            Chat? message = await _context.Chats
                .FirstOrDefaultAsync(
                    item =>
                        item.MessageId == messageId &&
                        !item.IsReportMessage &&
                        item.ReportId == null &&
                        (item.SenderId == ownerId || item.ReceiverId == ownerId),
                    cancellationToken);

            if (message == null)
                return JsonFailure("الرسالة غير موجودة.");

            if (!await IsMessageInsideActivePlaceAsync(
                    message,
                    ownerId,
                    activePlace.PlaceId,
                    cancellationToken))
            {
                return JsonFailure("الرسالة لا تخص المنشأة المحددة.");
            }

            if (message.SenderId == ownerId)
                message.IsDeletedForSender = true;
            else
                message.IsDeletedForReceiver = true;

            await _context.SaveChangesAsync(cancellationToken);

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMessageForEveryone(
            int messageId,
            CancellationToken cancellationToken)
        {
            if (!TryGetOwnerId(out int ownerId))
                return JsonFailure("انتهت الجلسة.");

            Place? activePlace = await ResolveActivePlaceAsync(ownerId, cancellationToken);
            if (activePlace == null)
                return JsonFailure("لا توجد منشأة مرتبطة بحسابك.");

            Chat? message = await _context.Chats
                .FirstOrDefaultAsync(
                    item =>
                        item.MessageId == messageId &&
                        item.SenderId == ownerId &&
                        !item.IsReportMessage &&
                        item.ReportId == null,
                    cancellationToken);

            if (message == null)
                return JsonFailure("لا يمكنك حذف رسالة لم تقم بإرسالها.");

            if (!await IsMessageInsideActivePlaceAsync(
                    message,
                    ownerId,
                    activePlace.PlaceId,
                    cancellationToken))
            {
                return JsonFailure("الرسالة لا تخص المنشأة المحددة.");
            }

            if (message.SendTime.HasValue &&
                (DateTime.Now - message.SendTime.Value).TotalHours > 24)
            {
                return JsonFailure("لا يمكن الحذف للجميع بعد مرور 24 ساعة.");
            }

            message.IsDeletedForSender = true;
            message.IsDeletedForReceiver = true;

            if (!message.PlaceId.HasValue)
                message.PlaceId = activePlace.PlaceId;

            await _context.SaveChangesAsync(cancellationToken);

            await _hubContext.Clients.User(message.ReceiverId.ToString())
                .SendAsync(
                    "MessageDeleted",
                    new
                    {
                        messageId = message.MessageId,
                        placeId = activePlace.PlaceId
                    },
                    cancellationToken);

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ClearChatForMe(
            int customerId,
            CancellationToken cancellationToken)
        {
            if (!TryGetOwnerId(out int ownerId))
                return JsonFailure("انتهت الجلسة.");

            Place? activePlace = await ResolveActivePlaceAsync(ownerId, cancellationToken);
            if (activePlace == null)
                return JsonFailure("لا توجد منشأة مرتبطة بحسابك.");

            if (!await CanOwnerContactCustomerAsync(
                    ownerId,
                    activePlace.PlaceId,
                    customerId,
                    cancellationToken))
            {
                return JsonFailure("هذا العميل غير مرتبط بالمنشأة المحددة.");
            }

            bool includeLegacy = await CanIncludeLegacyMessagesAsync(
                ownerId,
                customerId,
                activePlace.PlaceId,
                cancellationToken);

            List<Chat> messages = await ScopedConversationQuery(
                    ownerId,
                    customerId,
                    activePlace.PlaceId,
                    includeLegacy,
                    includeDeletedForOwner: true)
                .ToListAsync(cancellationToken);

            foreach (Chat message in messages)
            {
                if (message.SenderId == ownerId)
                    message.IsDeletedForSender = true;
                else if (message.ReceiverId == ownerId)
                    message.IsDeletedForReceiver = true;

                if (!message.PlaceId.HasValue && includeLegacy)
                    message.PlaceId = activePlace.PlaceId;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Json(new
            {
                success = true,
                message = "تم مسح محادثة هذه المنشأة من شاشتك فقط."
            });
        }

        // =====================================================================
        // الحظر
        // =====================================================================

        [HttpPost]
        public IActionResult BlockUser(int customerId)
        {
            // لا نغيّر Users.AccountState هنا؛ ذلك سيوقف حساب العميل في النظام كله.
            // الحظر الخاص بمحادثة واحدة يحتاج تخزينًا خاصًا بالعلاقة
            // (المالك + العميل + المنشأة)، وسيُفعل بعد اعتماد تصميمه.
            return JsonFailure(
                "الحظر الخاص بالمحادثة غير مفعل بعد، ولم يتم إيقاف حساب العميل.");
        }

        [HttpPost]
        public IActionResult UnblockUser(int customerId)
        {
            return JsonFailure(
                "لا يوجد حظر خاص بهذه المحادثة لإلغائه.");
        }

        [HttpGet]
        public async Task<IActionResult> CheckUserBlockStatus(
            int customerId,
            CancellationToken cancellationToken)
        {
            string? accountState = await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == customerId)
                .Select(user => user.AccountState)
                .FirstOrDefaultAsync(cancellationToken);

            return Json(new
            {
                success = true,
                isBlocked = IsSystemAccountUnavailable(accountState),
                isConversationBlockSupported = false,
                isSystemAccountState = true
            });
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private async Task<Place?> ResolveActivePlaceAsync(
            int ownerId,
            CancellationToken cancellationToken)
        {
            string cookieName = $"ownerActivePlace_{ownerId}";
            int.TryParse(Request.Cookies[cookieName], out int requestedPlaceId);

            if (requestedPlaceId > 0)
            {
                Place? requestedPlace = await _context.Places
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        place =>
                            place.PlaceId == requestedPlaceId &&
                            place.OwnerId == ownerId,
                        cancellationToken);

                if (requestedPlace != null)
                    return requestedPlace;
            }

            return await _context.Places
                .AsNoTracking()
                .Where(place => place.OwnerId == ownerId)
                .OrderBy(place => place.PlaceId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<bool> CanOwnerContactCustomerAsync(
            int ownerId,
            int placeId,
            int customerId,
            CancellationToken cancellationToken)
        {
            bool validCustomer = await _context.Users
                .AsNoTracking()
                .AnyAsync(user =>
                    user.UserId == customerId &&
                    user.Role == "Customer",
                    cancellationToken);

            if (!validCustomer)
                return false;

            bool hasBooking = await _context.Bookings
                .AsNoTracking()
                .AnyAsync(booking =>
                    booking.PlaceId == placeId &&
                    booking.CustomerId == customerId &&
                    booking.Place.OwnerId == ownerId,
                    cancellationToken);

            if (hasBooking)
                return true;

            return await _context.Chats
                .AsNoTracking()
                .AnyAsync(chat =>
                    chat.PlaceId == placeId &&
                    !chat.IsReportMessage &&
                    chat.ReportId == null &&
                    ((chat.SenderId == ownerId && chat.ReceiverId == customerId) ||
                     (chat.SenderId == customerId && chat.ReceiverId == ownerId)),
                    cancellationToken);
        }

        private async Task<Dictionary<int, HashSet<int>>> BuildCustomerPlaceRelationshipsAsync(
            int ownerId,
            CancellationToken cancellationToken)
        {
            var bookingLinks = await _context.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.Place.OwnerId == ownerId &&
                    booking.CustomerId.HasValue)
                .Select(booking => new
                {
                    CustomerId = booking.CustomerId!.Value,
                    booking.PlaceId
                })
                .Distinct()
                .ToListAsync(cancellationToken);

            var chatLinks = await _context.Chats
                .AsNoTracking()
                .Where(chat =>
                    chat.PlaceId != null &&
                    chat.Place!.OwnerId == ownerId &&
                    !chat.IsReportMessage &&
                    chat.ReportId == null &&
                    (chat.SenderId == ownerId || chat.ReceiverId == ownerId))
                .Select(chat => new
                {
                    CustomerId = chat.SenderId == ownerId
                        ? chat.ReceiverId
                        : chat.SenderId,
                    PlaceId = chat.PlaceId!.Value
                })
                .Distinct()
                .ToListAsync(cancellationToken);

            Dictionary<int, HashSet<int>> result =
                new Dictionary<int, HashSet<int>>();

            foreach (var link in bookingLinks)
            {
                AddCustomerPlaceLink(result, link.CustomerId, link.PlaceId);
            }

            foreach (var link in chatLinks)
            {
                AddCustomerPlaceLink(result, link.CustomerId, link.PlaceId);
            }

            return result;
        }

        private async Task<bool> CanIncludeLegacyMessagesAsync(
            int ownerId,
            int customerId,
            int activePlaceId,
            CancellationToken cancellationToken)
        {
            Dictionary<int, HashSet<int>> relationships =
                await BuildCustomerPlaceRelationshipsAsync(ownerId, cancellationToken);

            return relationships.TryGetValue(customerId, out HashSet<int>? placeIds) &&
                   placeIds.Count == 1 &&
                   placeIds.Contains(activePlaceId);
        }

        private IQueryable<Chat> ScopedConversationQuery(
            int ownerId,
            int customerId,
            int placeId,
            bool includeLegacy,
            bool includeDeletedForOwner)
        {
            IQueryable<Chat> query = _context.Chats.Where(chat =>
                !chat.IsReportMessage &&
                chat.ReportId == null &&
                (chat.PlaceId == placeId || (includeLegacy && chat.PlaceId == null)) &&
                ((chat.SenderId == ownerId && chat.ReceiverId == customerId) ||
                 (chat.SenderId == customerId && chat.ReceiverId == ownerId)));

            if (!includeDeletedForOwner)
            {
                query = query.Where(chat =>
                    (chat.SenderId == ownerId && !chat.IsDeletedForSender) ||
                    (chat.ReceiverId == ownerId && !chat.IsDeletedForReceiver));
            }

            return query;
        }

        private async Task<bool> IsMessageInsideActivePlaceAsync(
            Chat message,
            int ownerId,
            int activePlaceId,
            CancellationToken cancellationToken)
        {
            if (message.PlaceId == activePlaceId)
                return true;

            if (message.PlaceId.HasValue)
                return false;

            int customerId = message.SenderId == ownerId
                ? message.ReceiverId
                : message.SenderId;

            return await CanIncludeLegacyMessagesAsync(
                ownerId,
                customerId,
                activePlaceId,
                cancellationToken);
        }

        private static void AddCustomerPlaceLink(
            IDictionary<int, HashSet<int>> destination,
            int customerId,
            int placeId)
        {
            if (!destination.TryGetValue(customerId, out HashSet<int>? places))
            {
                places = new HashSet<int>();
                destination[customerId] = places;
            }

            places.Add(placeId);
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

        private static string GetMessagePreview(Chat message)
        {
            string text = message.MessageText ?? string.Empty;

            if (text.StartsWith("[REPLY|", StringComparison.Ordinal))
            {
                int closingBracketIndex = text.IndexOf(']');
                if (closingBracketIndex >= 0 && closingBracketIndex + 1 < text.Length)
                    text = text.Substring(closingBracketIndex + 1).Trim();
            }

            if (!string.IsNullOrWhiteSpace(text))
                return text.Length <= 90 ? text : text.Substring(0, 90) + "…";

            return string.IsNullOrWhiteSpace(message.AttachmentUrl)
                ? "رسالة"
                : "📎 مرفق";
        }

        private static string NormalizeMessageText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            return normalized;
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

            string cleanQuotedText = Regex.Replace(quotedText, "<.*?>", string.Empty)
                .Replace("|", " ")
                .Replace("]", " ")
                .Trim();

            string cleanQuotedSender = Regex.Replace(quotedSender, "<.*?>", string.Empty)
                .Replace("|", " ")
                .Replace("]", " ")
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
                attachmentUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

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

            string storedName = Path.GetFileName(attachmentUrl);
            int separatorIndex = storedName.IndexOf('_');

            return separatorIndex >= 0 && separatorIndex + 1 < storedName.Length
                ? storedName.Substring(separatorIndex + 1)
                : storedName;
        }

        private static bool IsImageExtension(string? extension)
        {
            return extension != null &&
                   new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }
                       .Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsVideoExtension(string? extension)
        {
            return extension != null &&
                   new[] { ".mp4", ".webm", ".mov" }
                       .Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private static string FormatChatTime(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("hh:mm tt")
                    .Replace("AM", "ص")
                    .Replace("PM", "م")
                : string.Empty;
        }

        private static string BuildPresenceText(User customer)
        {
            if (customer.IsOnline)
                return "متصل الآن";

            if (!customer.LastSeen.HasValue)
                return "آخر ظهور: غير معروف";

            TimeSpan difference = DateTime.Now - customer.LastSeen.Value;

            if (difference.TotalMinutes < 1)
                return "الآن";
            if (difference.TotalMinutes < 60)
                return $"منذ {Math.Max(1, difference.Minutes)} دقيقة";
            if (difference.TotalHours < 24)
                return $"منذ {Math.Max(1, difference.Hours)} ساعة";
            if (difference.TotalDays < 7)
                return $"منذ {Math.Max(1, difference.Days)} يوم";

            return customer.LastSeen.Value.ToString("yyyy/MM/dd HH:mm");
        }

        private static bool IsSystemAccountUnavailable(string? accountState)
        {
            return string.Equals(accountState, "Blocked", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(accountState, "Suspended", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(accountState, "Inactive", StringComparison.OrdinalIgnoreCase);
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

        private bool TryGetOwnerId(out int ownerId)
        {
            string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out ownerId) && ownerId > 0;
        }

        private IActionResult JsonFailure(string message)
        {
            return Json(new
            {
                success = false,
                message
            });
        }
    }
}
