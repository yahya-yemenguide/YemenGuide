using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using YemenGuide.Data;

namespace YemenGuide.Web.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private const string ActiveConversationGroupKey = "ActiveConversationGroup";
        private const string ActiveConversationIdentityKey = "ActiveConversationIdentity";

        private static readonly ConcurrentDictionary<string, int> ConnectionUsers =
            new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

        private static readonly ConcurrentDictionary<int, int> UserConnectionCounts =
            new ConcurrentDictionary<int, int>();

        private readonly AppDbContext _context;

        public ChatHub(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// يدخل اتصال المتصفح إلى مجموعة محادثة منشأة واحدة.
        /// هوية المجموعة: PlaceId + OwnerId + CustomerId.
        /// </summary>
        public async Task<bool> JoinConversation(int placeId, int otherUserId)
        {
            if (!TryGetCurrentUserId(out int currentUserId) ||
                placeId <= 0 ||
                otherUserId <= 0 ||
                currentUserId == otherUserId)
            {
                return false;
            }

            ConversationIdentity? identity = await ResolveConversationIdentityAsync(
                currentUserId,
                otherUserId,
                placeId);

            if (identity == null)
                return false;

            string groupName = BuildConversationGroupName(identity);

            if (Context.Items.TryGetValue(ActiveConversationGroupKey, out object? previousValue) &&
                previousValue is string previousGroup &&
                !string.Equals(previousGroup, groupName, StringComparison.Ordinal))
            {
                await Groups.RemoveFromGroupAsync(
                    Context.ConnectionId,
                    previousGroup);
            }

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                groupName);

            Context.Items[ActiveConversationGroupKey] = groupName;
            Context.Items[ActiveConversationIdentityKey] = identity;

            return true;
        }

        /// <summary>
        /// يخرج الاتصال من المحادثة النشطة فقط.
        /// </summary>
        public async Task LeaveConversation()
        {
            if (Context.Items.TryGetValue(ActiveConversationGroupKey, out object? value) &&
                value is string groupName &&
                !string.IsNullOrWhiteSpace(groupName))
            {
                await Groups.RemoveFromGroupAsync(
                    Context.ConnectionId,
                    groupName);
            }

            Context.Items.Remove(ActiveConversationGroupKey);
            Context.Items.Remove(ActiveConversationIdentityKey);
        }

        /// <summary>
        /// إشعار الكتابة داخل المنشأة والمحادثة الصحيحة فقط.
        /// بقي توقيع الدالة كما هو حتى لا ينكسر JavaScript الحالي،
        /// وبعد JoinConversation يتم الاعتماد على هوية المجموعة المحفوظة.
        /// </summary>
        public async Task NotifyTyping(
            int receiverId,
            bool isTyping)
        {
            if (!TryGetCurrentUserId(out int senderId) ||
                receiverId <= 0 ||
                receiverId == senderId)
            {
                return;
            }

            ConversationIdentity? activeIdentity = GetActiveConversationIdentity();

            if (activeIdentity != null &&
                activeIdentity.ContainsUsers(senderId, receiverId))
            {
                if (!activeIdentity.IsChatEnabled)
                    return;

                string activeGroup = BuildConversationGroupName(activeIdentity);

                string[] senderConnectionIds = ConnectionUsers
                    .Where(item => item.Value == senderId)
                    .Select(item => item.Key)
                    .ToArray();

                await Clients.GroupExcept(activeGroup, senderConnectionIds)
                    .SendAsync(
                        "UserIsTyping",
                        senderId,
                        isTyping,
                        activeIdentity.PlaceId);

                return;
            }

            // توافق مع الواجهات القديمة قبل استدعاء JoinConversation:
            // لا نرسل مؤشر الكتابة إلا عندما توجد منشأة واحدة مؤكدة للعلاقة.
            ConversationIdentity? resolvedIdentity =
                await ResolveUniqueConversationIdentityAsync(
                    senderId,
                    receiverId);

            if (resolvedIdentity == null || !resolvedIdentity.IsChatEnabled)
                return;

            await Clients.User(receiverId.ToString())
                .SendAsync(
                    "UserIsTyping",
                    senderId,
                    isTyping,
                    resolvedIdentity.PlaceId);
        }

        /// <summary>
        /// يسمح للواجهة بإعادة التحقق من أنها داخل المحادثة المطلوبة
        /// بعد إعادة اتصال SignalR.
        /// </summary>
        public Task<object?> GetActiveConversation()
        {
            ConversationIdentity? identity = GetActiveConversationIdentity();

            if (identity == null)
                return Task.FromResult<object?>(null);

            return Task.FromResult<object?>(new
            {
                placeId = identity.PlaceId,
                ownerId = identity.OwnerId,
                customerId = identity.CustomerId
            });
        }

        public override async Task OnConnectedAsync()
        {
            if (TryGetCurrentUserId(out int userId))
            {
                ConnectionUsers[Context.ConnectionId] = userId;

                int connectionCount = UserConnectionCounts.AddOrUpdate(
                    userId,
                    1,
                    (_, currentCount) => currentCount + 1);

                if (connectionCount == 1)
                {
                    DateTime now = DateTime.Now;
                    await SetUserPresenceAsync(userId, isOnline: true, now);
                    await NotifyRelatedUsersAsync(userId, isOnline: true, now);
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (ConnectionUsers.TryRemove(Context.ConnectionId, out int userId))
            {
                int remainingConnections = UserConnectionCounts.AddOrUpdate(
                    userId,
                    0,
                    (_, currentCount) => Math.Max(0, currentCount - 1));

                if (remainingConnections == 0)
                {
                    UserConnectionCounts.TryRemove(userId, out _);

                    DateTime lastSeen = DateTime.Now;
                    await SetUserPresenceAsync(userId, isOnline: false, lastSeen);
                    await NotifyRelatedUsersAsync(userId, isOnline: false, lastSeen);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        // =====================================================================
        // التحقق من هوية المحادثة
        // =====================================================================

        private async Task<ConversationIdentity?> ResolveConversationIdentityAsync(
            int currentUserId,
            int otherUserId,
            int placeId)
        {
            var users = await _context.Users
                .AsNoTracking()
                .Where(user =>
                    user.UserId == currentUserId ||
                    user.UserId == otherUserId)
                .Select(user => new
                {
                    user.UserId,
                    user.Role,
                    user.AccountState
                })
                .ToListAsync();

            if (users.Count != 2)
                return null;

            var place = await _context.Places
                .AsNoTracking()
                .Where(item => item.PlaceId == placeId)
                .Select(item => new
                {
                    item.PlaceId,
                    item.OwnerId,
                    item.Status,
                    item.IsAvailable,
                    IsChatEnabled =
                        item.PlacePolicy == null ||
                        item.PlacePolicy.IsChatEnabled != false
                })
                .FirstOrDefaultAsync();

            if (place == null || !place.OwnerId.HasValue)
                return null;

            int ownerId = place.OwnerId.Value;
            int customerId;

            if (currentUserId == ownerId)
                customerId = otherUserId;
            else if (otherUserId == ownerId)
                customerId = currentUserId;
            else
                return null;

            var owner = users.FirstOrDefault(user => user.UserId == ownerId);
            var customer = users.FirstOrDefault(user => user.UserId == customerId);

            if (owner == null || customer == null ||
                !string.Equals(owner.Role, "Owner", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(customer.Role, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (IsSystemAccountUnavailable(owner.AccountState) ||
                IsSystemAccountUnavailable(customer.AccountState))
            {
                return null;
            }

            bool hasBooking = await _context.Bookings
                .AsNoTracking()
                .AnyAsync(booking =>
                    booking.PlaceId == placeId &&
                    booking.CustomerId == customerId);

            bool hasPreviousChat = await _context.Chats
                .AsNoTracking()
                .AnyAsync(chat =>
                    chat.PlaceId == placeId &&
                    !chat.IsReportMessage &&
                    chat.ReportId == null &&
                    (
                        (chat.SenderId == ownerId && chat.ReceiverId == customerId) ||
                        (chat.SenderId == customerId && chat.ReceiverId == ownerId)
                    ));

            bool hasLegacyChat = false;

            if (!hasPreviousChat &&
                await CanIncludeLegacyMessagesAsync(customerId, ownerId, placeId))
            {
                hasLegacyChat = await _context.Chats
                    .AsNoTracking()
                    .AnyAsync(chat =>
                        chat.PlaceId == null &&
                        !chat.IsReportMessage &&
                        chat.ReportId == null &&
                        (
                            (chat.SenderId == ownerId && chat.ReceiverId == customerId) ||
                            (chat.SenderId == customerId && chat.ReceiverId == ownerId)
                        ));
            }

            hasPreviousChat = hasPreviousChat || hasLegacyChat;

            bool currentUserIsOwner = currentUserId == ownerId;
            bool canCustomerStartNew =
                !currentUserIsOwner &&
                string.Equals(place.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
                place.IsAvailable != false &&
                place.IsChatEnabled;

            bool allowed = currentUserIsOwner
                ? hasBooking || hasPreviousChat
                : hasBooking || hasPreviousChat || canCustomerStartNew;

            return allowed
                ? new ConversationIdentity(
                    placeId,
                    ownerId,
                    customerId,
                    place.IsChatEnabled)
                : null;
        }

        private async Task<ConversationIdentity?> ResolveUniqueConversationIdentityAsync(
            int firstUserId,
            int secondUserId)
        {
            var users = await _context.Users
                .AsNoTracking()
                .Where(user =>
                    user.UserId == firstUserId ||
                    user.UserId == secondUserId)
                .Select(user => new
                {
                    user.UserId,
                    user.Role
                })
                .ToListAsync();

            if (users.Count != 2)
                return null;

            int ownerId;
            int customerId;

            var firstUser = users.First(user => user.UserId == firstUserId);
            var secondUser = users.First(user => user.UserId == secondUserId);

            if (string.Equals(firstUser.Role, "Owner", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(secondUser.Role, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                ownerId = firstUserId;
                customerId = secondUserId;
            }
            else if (string.Equals(secondUser.Role, "Owner", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(firstUser.Role, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                ownerId = secondUserId;
                customerId = firstUserId;
            }
            else
            {
                return null;
            }

            List<int> placeIds = await _context.Chats
                .AsNoTracking()
                .Where(chat =>
                    chat.PlaceId != null &&
                    !chat.IsReportMessage &&
                    chat.ReportId == null &&
                    (
                        (chat.SenderId == ownerId && chat.ReceiverId == customerId) ||
                        (chat.SenderId == customerId && chat.ReceiverId == ownerId)
                    ))
                .Select(chat => chat.PlaceId!.Value)
                .Distinct()
                .ToListAsync();

            List<int> bookingPlaceIds = await _context.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.CustomerId == customerId &&
                    booking.Place.OwnerId == ownerId)
                .Select(booking => booking.PlaceId)
                .Distinct()
                .ToListAsync();

            HashSet<int> candidates = placeIds.ToHashSet();
            candidates.UnionWith(bookingPlaceIds);

            if (candidates.Count == 0)
            {
                List<int> ownerPlaces = await _context.Places
                    .AsNoTracking()
                    .Where(place => place.OwnerId == ownerId)
                    .Select(place => place.PlaceId)
                    .Take(2)
                    .ToListAsync();

                if (ownerPlaces.Count == 1)
                    candidates.Add(ownerPlaces[0]);
            }

            if (candidates.Count != 1)
                return null;

            return await ResolveConversationIdentityAsync(
                firstUserId,
                secondUserId,
                candidates.First());
        }

        private async Task<bool> CanIncludeLegacyMessagesAsync(
            int customerId,
            int ownerId,
            int activePlaceId)
        {
            List<int> bookingPlaceIds = await _context.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.CustomerId == customerId &&
                    booking.Place.OwnerId == ownerId)
                .Select(booking => booking.PlaceId)
                .Distinct()
                .ToListAsync();

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
                .ToListAsync();

            HashSet<int> placeIds = bookingPlaceIds.ToHashSet();
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
                .ToListAsync();

            return ownerPlaces.Count == 1 && ownerPlaces[0] == activePlaceId;
        }

        // =====================================================================
        // حالة الاتصال
        // =====================================================================

        private async Task SetUserPresenceAsync(
            int userId,
            bool isOnline,
            DateTime lastSeen)
        {
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return;

            user.IsOnline = isOnline;
            user.LastSeen = lastSeen;

            await _context.SaveChangesAsync();
        }

        private async Task NotifyRelatedUsersAsync(
            int userId,
            bool isOnline,
            DateTime lastSeen)
        {
            HashSet<int> relatedUserIds = new HashSet<int>();

            List<int> chatUsers = await _context.Chats
                .AsNoTracking()
                .Where(chat =>
                    !chat.IsReportMessage &&
                    chat.ReportId == null &&
                    (chat.SenderId == userId || chat.ReceiverId == userId))
                .Select(chat =>
                    chat.SenderId == userId
                        ? chat.ReceiverId
                        : chat.SenderId)
                .Distinct()
                .ToListAsync();

            relatedUserIds.UnionWith(chatUsers);

            string? role = await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == userId)
                .Select(user => user.Role)
                .FirstOrDefaultAsync();

            if (string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                List<int> ownerIds = await _context.Bookings
                    .AsNoTracking()
                    .Where(booking =>
                        booking.CustomerId == userId &&
                        booking.Place.OwnerId != null)
                    .Select(booking => booking.Place.OwnerId!.Value)
                    .Distinct()
                    .ToListAsync();

                relatedUserIds.UnionWith(ownerIds);
            }
            else if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase))
            {
                List<int> customerIds = await _context.Bookings
                    .AsNoTracking()
                    .Where(booking =>
                        booking.Place.OwnerId == userId &&
                        booking.CustomerId.HasValue)
                    .Select(booking => booking.CustomerId!.Value)
                    .Distinct()
                    .ToListAsync();

                relatedUserIds.UnionWith(customerIds);
            }

            relatedUserIds.Remove(userId);

            object lastSeenValue = isOnline
                ? "متصل الآن"
                : lastSeen.ToString("o");

            foreach (int relatedUserId in relatedUserIds)
            {
                await Clients.User(relatedUserId.ToString())
                    .SendAsync(
                        "UserStatusChanged",
                        userId,
                        isOnline,
                        lastSeenValue);
            }
        }

        // =====================================================================
        // أدوات عامة
        // =====================================================================

        private ConversationIdentity? GetActiveConversationIdentity()
        {
            return Context.Items.TryGetValue(ActiveConversationIdentityKey, out object? value) &&
                   value is ConversationIdentity identity
                ? identity
                : null;
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            string? value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out userId) && userId > 0;
        }

        public static string BuildConversationGroupName(
            int placeId,
            int ownerId,
            int customerId)
        {
            return $"chat_place_{placeId}_owner_{ownerId}_customer_{customerId}";
        }

        private static string BuildConversationGroupName(
            ConversationIdentity identity)
        {
            return BuildConversationGroupName(
                identity.PlaceId,
                identity.OwnerId,
                identity.CustomerId);
        }

        private static bool IsSystemAccountUnavailable(string? accountState)
        {
            return string.Equals(accountState, "Blocked", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(accountState, "Suspended", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(accountState, "Inactive", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(accountState, "Disabled", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(accountState, "Deleted", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class ConversationIdentity
        {
            public ConversationIdentity(
                int placeId,
                int ownerId,
                int customerId,
                bool isChatEnabled)
            {
                PlaceId = placeId;
                OwnerId = ownerId;
                CustomerId = customerId;
                IsChatEnabled = isChatEnabled;
            }

            public int PlaceId { get; }
            public int OwnerId { get; }
            public int CustomerId { get; }
            public bool IsChatEnabled { get; }

            public bool ContainsUsers(int firstUserId, int secondUserId)
            {
                return
                    (firstUserId == OwnerId && secondUserId == CustomerId) ||
                    (firstUserId == CustomerId && secondUserId == OwnerId);
            }
        }
    }
}
