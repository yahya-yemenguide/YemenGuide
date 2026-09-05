using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.Entities;
using YemenGuide.Data;

namespace YemenGuide.Web.Services.Employees
{
    public sealed class AdminAuditService : IAdminAuditService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AdminAuditService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public AdminAuditService(
            AppDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AdminAuditService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogAsync(
            int? userId,
            string actionType,
            string? entityType = null,
            string? entityId = null,
            string? description = null,
            object? oldValues = null,
            object? newValues = null,
            bool isSuccessful = true,
            string? failureReason = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(actionType))
            {
                throw new ArgumentException(
                    "Action type cannot be empty.",
                    nameof(actionType));
            }

            var httpContext = _httpContextAccessor.HttpContext;

            var log = new AdminAuditLog
            {
                UserId = userId,
                ActionType = TrimToLength(actionType, 100)!,
                EntityType = TrimToLength(entityType, 100),
                EntityId = TrimToLength(entityId, 100),
                Description = TrimToLength(description, 1000),
                OldValuesJson = SerializeForAudit(oldValues),
                NewValuesJson = SerializeForAudit(newValues),
                Ipaddress = TrimToLength(
                    httpContext?.Connection.RemoteIpAddress?.ToString(),
                    50),
                UserAgent = TrimToLength(
                    httpContext?.Request.Headers.UserAgent.ToString(),
                    500),
                OccurredAt = DateTime.Now,
                IsSuccessful = isSuccessful,
                FailureReason = TrimToLength(failureReason, 500)
            };

            try
            {
                _context.AdminAuditLogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "تعذر حفظ سجل العملية الإدارية {ActionType} للمستخدم {UserId}.",
                    actionType,
                    userId);

                throw;
            }
        }

        private static string? SerializeForAudit(object? value)
        {
            if (value == null)
            {
                return null;
            }

            return JsonSerializer.Serialize(value, JsonOptions);
        }

        private static string? TrimToLength(string? value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim();

            return normalized.Length <= maximumLength
                ? normalized
                : normalized[..maximumLength];
        }
    }
}
