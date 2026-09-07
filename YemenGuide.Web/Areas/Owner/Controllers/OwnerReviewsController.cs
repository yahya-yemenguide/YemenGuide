using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Filters;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    [Area("Owner")]
    [Authorize(Roles = "Owner,Admin")]
    [AuthorizeActiveOwner]
    [Route("Owner/Reviews")]
    public class OwnerReviewsController : Controller
    {
        private const int DefaultPageSize = 10;
        private const int MaximumPageSize = 50;
        private const int MaximumReplyLength = 2000;

        private static readonly HashSet<string> AllowedSentiments =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "all",
                "positive",
                "neutral",
                "negative"
            };

        private static readonly HashSet<string> AllowedReplyStates =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "all",
                "replied",
                "unanswered"
            };

        private static readonly HashSet<string> AllowedSorts =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "newest",
                "oldest",
                "highest",
                "lowest",
                "most-liked"
            };

        private readonly AppDbContext _context;

        public OwnerReviewsController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// يعيد محتوى تبويبة التقييمات فقط. سيتم استدعاؤه عند فتح التبويبة
        /// أو تغيير فلتر دون إعادة تحميل صفحة الدردشة كاملة.
        /// </summary>
        [HttpGet("Panel")]
        public async Task<IActionResult> Panel(
            [FromQuery] OwnerReviewFilterDto? filter,
            CancellationToken cancellationToken)
        {
            if (!TryGetOwnerId(out int ownerId))
                return Unauthorized();

            OwnerReviewFilterDto normalizedFilter = NormalizeFilter(filter);
            Place? activePlace = await ResolveActivePlaceAsync(ownerId, cancellationToken);

            ReviewFeedbackDto model = await BuildReviewsModelAsync(
                ownerId,
                activePlace,
                normalizedFilter,
                cancellationToken);

            return PartialView(
                "~/Areas/Owner/Views/Chat/_ReviewsFeedback.cshtml",
                model);
        }

        /// <summary>
        /// إعجاب المالك بتقييم أو رد داخل المنشأة المحددة.
        /// </summary>
        [HttpPost("ToggleLike")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLike(
            [FromForm] int reviewId,
            CancellationToken cancellationToken)
        {
            if (!TryGetOwnerId(out int ownerId))
                return JsonFailure("انتهت الجلسة، يرجى تسجيل الدخول من جديد.");

            Place? activePlace = await ResolveActivePlaceAsync(ownerId, cancellationToken);
            if (activePlace == null)
                return JsonFailure("لا توجد منشأة متاحة على حسابك.");

            bool reviewExists = await _context.Reviews
                .AsNoTracking()
                .AnyAsync(
                    review =>
                        review.ReviewId == reviewId &&
                        review.PlaceId == activePlace.PlaceId,
                    cancellationToken);

            if (!reviewExists)
                return JsonFailure("التقييم غير موجود ضمن المنشأة المحددة.");

            ReviewLike? existingLike = await _context.ReviewLikes
                .FirstOrDefaultAsync(
                    like =>
                        like.ReviewId == reviewId &&
                        like.UserId == ownerId,
                    cancellationToken);

            bool isLiked;

            if (existingLike == null)
            {
                _context.ReviewLikes.Add(
                    new ReviewLike
                    {
                        ReviewId = reviewId,
                        UserId = ownerId,
                        CreatedAt = DateTime.Now
                    });

                isLiked = true;
            }
            else
            {
                _context.ReviewLikes.Remove(existingLike);
                isLiked = false;
            }

            await _context.SaveChangesAsync(cancellationToken);

            int likesCount = await _context.ReviewLikes
                .AsNoTracking()
                .CountAsync(
                    like => like.ReviewId == reviewId,
                    cancellationToken);

            return Json(
                new
                {
                    success = true,
                    isLiked,
                    likesCount
                });
        }

        /// <summary>
        /// إضافة رد من المالك على تقييم أو على رد موجود.
        /// </summary>
        [HttpPost("AddReply")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReply(
            [FromForm] int parentReviewId,
            [FromForm] string? replyText,
            CancellationToken cancellationToken)
        {
            if (!TryGetOwnerId(out int ownerId))
                return JsonFailure("انتهت الجلسة، يرجى تسجيل الدخول من جديد.");

            string normalizedText = NormalizeReplyText(replyText);
            if (normalizedText.Length == 0)
                return JsonFailure("لا يمكن إرسال رد فارغ.");

            if (normalizedText.Length > MaximumReplyLength)
                return JsonFailure($"الحد الأقصى للرد هو {MaximumReplyLength} حرف.");

            Place? activePlace = await ResolveActivePlaceAsync(ownerId, cancellationToken);
            if (activePlace == null)
                return JsonFailure("لا توجد منشأة متاحة على حسابك.");

            Review? parentReview = await _context.Reviews
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    review =>
                        review.ReviewId == parentReviewId &&
                        review.PlaceId == activePlace.PlaceId,
                    cancellationToken);

            if (parentReview == null)
                return JsonFailure("التعليق غير موجود ضمن المنشأة المحددة.");

            Review newReply = new Review
            {
                UserId = ownerId,
                PlaceId = activePlace.PlaceId,
                Stars = null,
                ReviewText = normalizedText,
                CreatedAt = DateTime.Now,
                ParentReviewId = parentReview.ReviewId,
                IsEdited = false
            };

            _context.Reviews.Add(newReply);
            await _context.SaveChangesAsync(cancellationToken);

            return Json(
                new
                {
                    success = true,
                    message = "تم نشر الرد بنجاح.",
                    reviewId = newReply.ReviewId
                });
        }

        /// <summary>
        /// يسمح للمالك بتعديل رده هو فقط، ولا يسمح بتعديل تقييم العميل.
        /// </summary>
        [HttpPost("EditReply")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditReply(
            [FromForm] int reviewId,
            [FromForm] string? newText,
            CancellationToken cancellationToken)
        {
            if (!TryGetOwnerId(out int ownerId))
                return JsonFailure("انتهت الجلسة، يرجى تسجيل الدخول من جديد.");

            string normalizedText = NormalizeReplyText(newText);
            if (normalizedText.Length == 0)
                return JsonFailure("لا يمكن أن يكون الرد فارغًا.");

            if (normalizedText.Length > MaximumReplyLength)
                return JsonFailure($"الحد الأقصى للرد هو {MaximumReplyLength} حرف.");

            Place? activePlace = await ResolveActivePlaceAsync(ownerId, cancellationToken);
            if (activePlace == null)
                return JsonFailure("لا توجد منشأة متاحة على حسابك.");

            Review? review = await _context.Reviews
                .FirstOrDefaultAsync(
                    item =>
                        item.ReviewId == reviewId &&
                        item.PlaceId == activePlace.PlaceId &&
                        item.UserId == ownerId &&
                        item.ParentReviewId.HasValue &&
                        !item.Stars.HasValue,
                    cancellationToken);

            if (review == null)
                return JsonFailure("لا تملك صلاحية تعديل هذا التعليق.");

            review.ReviewText = normalizedText;
            review.IsEdited = true;

            await _context.SaveChangesAsync(cancellationToken);

            return Json(
                new
                {
                    success = true,
                    message = "تم تعديل الرد بنجاح."
                });
        }

        /// <summary>
        /// حذف تقييم أو رد داخل المنشأة المحددة مع جميع الردود المتفرعة منه وإعجاباتها.
        /// </summary>
        [HttpPost("DeleteReply")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReply(
            [FromForm] int reviewId,
            CancellationToken cancellationToken)
        {
            if (!TryGetOwnerId(out int ownerId))
                return JsonFailure("انتهت الجلسة، يرجى تسجيل الدخول من جديد.");

            Place? activePlace = await ResolveActivePlaceAsync(ownerId, cancellationToken);
            if (activePlace == null)
                return JsonFailure("لا توجد منشأة متاحة على حسابك.");

            Review? targetReview = await _context.Reviews
                .FirstOrDefaultAsync(
                    review =>
                        review.ReviewId == reviewId &&
                        review.PlaceId == activePlace.PlaceId,
                    cancellationToken);

            if (targetReview == null)
                return JsonFailure("التقييم أو الرد غير موجود ضمن المنشأة المحددة.");

            List<Review> placeReviews = await _context.Reviews
                .Where(review => review.PlaceId == activePlace.PlaceId)
                .ToListAsync(cancellationToken);

            List<Review> descendants = CollectDescendants(placeReviews, targetReview.ReviewId);

            List<Review> reviewsToDelete = new List<Review> { targetReview };
            reviewsToDelete.AddRange(descendants);

            List<int> reviewIds = reviewsToDelete
                .Select(review => review.ReviewId)
                .Distinct()
                .ToList();

            List<ReviewLike> likesToDelete = await _context.ReviewLikes
                .Where(like => reviewIds.Contains(like.ReviewId))
                .ToListAsync(cancellationToken);

            if (likesToDelete.Count > 0)
                _context.ReviewLikes.RemoveRange(likesToDelete);

            _context.Reviews.RemoveRange(reviewsToDelete);
            await _context.SaveChangesAsync(cancellationToken);

            return Json(
                new
                {
                    success = true,
                    deletedReviewsCount = reviewIds.Count,
                    deletedRepliesCount = descendants.Count,
                    message = descendants.Count > 0
                        ? $"تم حذف التعليق و{descendants.Count} من الردود التابعة له بنجاح."
                        : "تم حذف التعليق بنجاح."
                });
        }

        private async Task<ReviewFeedbackDto> BuildReviewsModelAsync(
            int ownerId,
            Place? activePlace,
            OwnerReviewFilterDto filter,
            CancellationToken cancellationToken)
        {
            User? owner = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.UserId == ownerId, cancellationToken);

            string ownerName = GetDisplayName(owner, "المالك");

            ReviewFeedbackDto model = new ReviewFeedbackDto
            {
                HasActivePlace = activePlace != null,
                ActivePlaceId = activePlace?.PlaceId ?? 0,
                OwnerName = ownerName,
                OwnerProfileImage = owner?.ProfileImage,
                OwnerInitial = GetInitial(ownerName, "م"),
                Filter = filter,
                CurrentPage = filter.Page,
                PageSize = filter.PageSize
            };

            if (activePlace == null)
            {
                model.ErrorMessage = "لا توجد منشأة مرتبطة بحسابك حتى الآن.";
                return model;
            }

            List<Review> allReviews = await _context.Reviews
                .AsNoTracking()
                .Include(review => review.User)
                .Where(review => review.PlaceId == activePlace.PlaceId)
                .OrderBy(review => review.CreatedAt)
                .ThenBy(review => review.ReviewId)
                .ToListAsync(cancellationToken);

            if (allReviews.Count == 0)
            {
                model.TotalPages = 0;
                model.CurrentPage = 1;
                return model;
            }

            List<int> reviewIds = allReviews
                .Select(review => review.ReviewId)
                .ToList();

            List<ReviewLike> allLikes = await _context.ReviewLikes
                .AsNoTracking()
                .Where(like => reviewIds.Contains(like.ReviewId))
                .ToListAsync(cancellationToken);

            Dictionary<int, int> likesCountByReview = allLikes
                .GroupBy(like => like.ReviewId)
                .ToDictionary(group => group.Key, group => group.Count());

            HashSet<int> likedByOwner = allLikes
                .Where(like => like.UserId == ownerId)
                .Select(like => like.ReviewId)
                .ToHashSet();

            Dictionary<int, ReviewItemDto> reviewDictionary = allReviews
                .ToDictionary(
                    review => review.ReviewId,
                    review =>
                    {
                        string userName = GetDisplayName(review.User, "مستخدم");
                        bool isOwnerReply =
                            review.UserId == ownerId &&
                            review.ParentReviewId.HasValue &&
                            !review.Stars.HasValue;

                        return new ReviewItemDto
                        {
                            ReviewId = review.ReviewId,
                            UserId = review.UserId,
                            UserName = userName,
                            ProfileImage = review.User?.ProfileImage,
                            InitialLetter = GetInitial(userName, "م"),
                            Stars = review.Stars,
                            ReviewText = review.ReviewText ?? string.Empty,
                            CreatedAt = review.CreatedAt ?? DateTime.Now,
                            IsEdited = review.IsEdited,
                            ParentReviewId = review.ParentReviewId,
                            LikesCount = likesCountByReview.TryGetValue(
                                review.ReviewId,
                                out int likesCount)
                                    ? likesCount
                                    : 0,
                            IsLikedByMe = likedByOwner.Contains(review.ReviewId),
                            IsOwnerReply = isOwnerReply,
                            CanEdit = isOwnerReply,
                            // جميع العناصر المعروضة تخص المنشأة المحددة، لذلك يستطيع
                            // المالك حذف التقييم الأصلي أو أي رد مع شجرته التابعة.
                            CanDelete = true,
                            Replies = new List<ReviewItemDto>()
                        };
                    });

            List<ReviewItemDto> rootReviews = new List<ReviewItemDto>();

            foreach (Review review in allReviews)
            {
                ReviewItemDto item = reviewDictionary[review.ReviewId];

                if (review.ParentReviewId.HasValue &&
                    reviewDictionary.TryGetValue(
                        review.ParentReviewId.Value,
                        out ReviewItemDto? parent))
                {
                    item.RepliedToName = parent.UserName;
                    parent.Replies.Add(item);
                }
                else if (!review.ParentReviewId.HasValue &&
                         review.Stars.HasValue &&
                         review.Stars.Value >= 1 &&
                         review.Stars.Value <= 5)
                {
                    rootReviews.Add(item);
                }
            }

            foreach (ReviewItemDto rootReview in rootReviews)
            {
                SortRepliesByDate(rootReview);
                PopulateTreeSummary(rootReview, rootReview.ReviewId);
            }

            model.TotalReviews = rootReviews.Count;
            model.AverageRating = model.TotalReviews > 0
                ? Math.Round(rootReviews.Average(review => review.Stars ?? 0), 1)
                : 0d;

            for (int star = 1; star <= 5; star++)
            {
                model.StarDistribution[star] =
                    rootReviews.Count(review => review.Stars == star);
            }

            model.RepliedReviews = rootReviews.Count(review => review.HasOwnerReply);
            model.UnansweredReviews = model.TotalReviews - model.RepliedReviews;
            model.OwnerRepliesCount = rootReviews.Sum(review => review.OwnerRepliesCount);
            model.ReplyRate = model.TotalReviews > 0
                ? Math.Round((double)model.RepliedReviews / model.TotalReviews * 100d, 1)
                : 0d;

            IEnumerable<ReviewItemDto> filteredReviews = ApplyFilters(rootReviews, filter);
            filteredReviews = ApplySorting(filteredReviews, filter.SortBy);

            List<ReviewItemDto> filteredList = filteredReviews.ToList();
            model.FilteredReviewsCount = filteredList.Count;
            model.TotalPages = model.FilteredReviewsCount == 0
                ? 0
                : (int)Math.Ceiling((double)model.FilteredReviewsCount / filter.PageSize);

            int validPage = model.TotalPages == 0
                ? 1
                : Math.Min(filter.Page, model.TotalPages);

            model.CurrentPage = validPage;
            model.Filter.Page = validPage;
            model.Reviews = filteredList
                .Skip((validPage - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            return model;
        }

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

        private static OwnerReviewFilterDto NormalizeFilter(OwnerReviewFilterDto? filter)
        {
            OwnerReviewFilterDto normalized = filter ?? new OwnerReviewFilterDto();

            normalized.Search = string.IsNullOrWhiteSpace(normalized.Search)
                ? null
                : normalized.Search.Trim();

            if (!normalized.Rating.HasValue ||
                normalized.Rating.Value < 1 ||
                normalized.Rating.Value > 5)
            {
                normalized.Rating = null;
            }

            normalized.Sentiment = NormalizeAllowedValue(
                normalized.Sentiment,
                AllowedSentiments,
                "all");

            normalized.ReplyState = NormalizeAllowedValue(
                normalized.ReplyState,
                AllowedReplyStates,
                "all");

            normalized.SortBy = NormalizeAllowedValue(
                normalized.SortBy,
                AllowedSorts,
                "newest");

            normalized.Page = Math.Max(1, normalized.Page);

            if (normalized.PageSize <= 0)
                normalized.PageSize = DefaultPageSize;

            normalized.PageSize = Math.Min(normalized.PageSize, MaximumPageSize);

            if (normalized.DateFrom.HasValue)
                normalized.DateFrom = normalized.DateFrom.Value.Date;

            if (normalized.DateTo.HasValue)
                normalized.DateTo = normalized.DateTo.Value.Date;

            if (normalized.DateFrom.HasValue &&
                normalized.DateTo.HasValue &&
                normalized.DateFrom.Value > normalized.DateTo.Value)
            {
                DateTime oldFrom = normalized.DateFrom.Value;
                normalized.DateFrom = normalized.DateTo.Value;
                normalized.DateTo = oldFrom;
            }

            return normalized;
        }

        private static IEnumerable<ReviewItemDto> ApplyFilters(
            IEnumerable<ReviewItemDto> reviews,
            OwnerReviewFilterDto filter)
        {
            IEnumerable<ReviewItemDto> result = reviews;

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                string search = filter.Search.Trim();
                result = result.Where(review => TreeContainsSearch(review, search));
            }

            if (filter.Rating.HasValue)
                result = result.Where(review => review.Stars == filter.Rating.Value);

            result = filter.Sentiment switch
            {
                "positive" => result.Where(review => (review.Stars ?? 0) >= 4),
                "neutral" => result.Where(review => review.Stars == 3),
                "negative" => result.Where(review => (review.Stars ?? 0) <= 2),
                _ => result
            };

            result = filter.ReplyState switch
            {
                "replied" => result.Where(review => review.HasOwnerReply),
                "unanswered" => result.Where(review => !review.HasOwnerReply),
                _ => result
            };

            if (filter.DateFrom.HasValue)
            {
                DateTime dateFrom = filter.DateFrom.Value.Date;
                result = result.Where(review => review.CreatedAt.Date >= dateFrom);
            }

            if (filter.DateTo.HasValue)
            {
                DateTime dateTo = filter.DateTo.Value.Date;
                result = result.Where(review => review.CreatedAt.Date <= dateTo);
            }

            return result;
        }

        private static IEnumerable<ReviewItemDto> ApplySorting(
            IEnumerable<ReviewItemDto> reviews,
            string sortBy)
        {
            return sortBy switch
            {
                "oldest" => reviews
                    .OrderBy(review => review.CreatedAt)
                    .ThenBy(review => review.ReviewId),

                "highest" => reviews
                    .OrderByDescending(review => review.Stars)
                    .ThenByDescending(review => review.CreatedAt),

                "lowest" => reviews
                    .OrderBy(review => review.Stars)
                    .ThenByDescending(review => review.CreatedAt),

                "most-liked" => reviews
                    .OrderByDescending(review => review.LikesCount)
                    .ThenByDescending(review => review.CreatedAt),

                _ => reviews
                    .OrderByDescending(review => review.CreatedAt)
                    .ThenByDescending(review => review.ReviewId)
            };
        }

        private static void PopulateTreeSummary(ReviewItemDto node, int rootReviewId)
        {
            node.RootReviewId = rootReviewId;
            node.RepliesCount = 0;
            node.OwnerRepliesCount = 0;

            foreach (ReviewItemDto reply in node.Replies)
            {
                PopulateTreeSummary(reply, rootReviewId);

                node.RepliesCount += 1 + reply.RepliesCount;
                node.OwnerRepliesCount +=
                    (reply.IsOwnerReply ? 1 : 0) +
                    reply.OwnerRepliesCount;
            }

            node.HasOwnerReply = node.OwnerRepliesCount > 0;
        }

        private static void SortRepliesByDate(ReviewItemDto node)
        {
            node.Replies = node.Replies
                .OrderBy(reply => reply.CreatedAt)
                .ThenBy(reply => reply.ReviewId)
                .ToList();

            foreach (ReviewItemDto reply in node.Replies)
                SortRepliesByDate(reply);
        }

        private static bool TreeContainsSearch(ReviewItemDto node, string search)
        {
            if (node.UserName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                node.ReviewText.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return node.Replies.Any(reply => TreeContainsSearch(reply, search));
        }

        private static List<Review> CollectDescendants(
            IReadOnlyCollection<Review> allReviews,
            int parentReviewId)
        {
            List<Review> descendants = new List<Review>();
            Queue<int> pendingParents = new Queue<int>();
            pendingParents.Enqueue(parentReviewId);

            while (pendingParents.Count > 0)
            {
                int currentParentId = pendingParents.Dequeue();

                List<Review> children = allReviews
                    .Where(review => review.ParentReviewId == currentParentId)
                    .ToList();

                foreach (Review child in children)
                {
                    descendants.Add(child);
                    pendingParents.Enqueue(child.ReviewId);
                }
            }

            return descendants;
        }

        private static string NormalizeAllowedValue(
            string? value,
            HashSet<string> allowedValues,
            string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            string normalized = value.Trim().ToLowerInvariant();
            return allowedValues.Contains(normalized) ? normalized : fallback;
        }

        private static string NormalizeReplyText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        private static string GetDisplayName(User? user, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(user?.FullName))
                return user.FullName.Trim();

            if (!string.IsNullOrWhiteSpace(user?.Username))
                return user.Username.Trim();

            return fallback;
        }

        private static string GetInitial(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            string normalized = value.Trim();
            return normalized.Substring(0, 1).ToUpperInvariant();
        }

        private bool TryGetOwnerId(out int ownerId)
        {
            string? ownerIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(ownerIdValue, out ownerId) && ownerId > 0;
        }

        private IActionResult JsonFailure(string message)
        {
            return Json(
                new
                {
                    success = false,
                    message
                });
        }
    }
}
