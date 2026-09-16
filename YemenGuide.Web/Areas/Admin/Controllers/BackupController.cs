using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YemenGuide.Core.Constants;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Backups;

namespace YemenGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [Route("Admin/Backup")]
    public sealed class BackupController : Controller
    {
        private const long MaxRestoreUploadBytes =
            2L * 1024L * 1024L * 1024L; // 2 GB

        private readonly IBackupService _backupService;

        public BackupController(IBackupService backupService)
        {
            _backupService = backupService;
        }

        [HttpGet("Dashboard")]
        [RequireAdminPermission(AdminPermissionKeys.Settings.View)]
        public async Task<IActionResult> Dashboard(
            CancellationToken cancellationToken)
        {
            var data =
                await _backupService.GetDashboardAsync(
                    cancellationToken);

            return Json(new
            {
                success = true,
                capabilities = new
                {
                    data.Capabilities.DatabaseReachable,
                    data.Capabilities.DatabaseName,
                    data.Capabilities.ServerName,
                    data.Capabilities.RecoveryModel,
                    data.Capabilities.CanBackup,
                    data.Capabilities.CanRestore,
                    data.Capabilities.CanDownloadBackupFiles,
                    data.Capabilities.Error
                },
                settings = new
                {
                    data.Settings.AutoBackupEnabled,
                    data.Settings.DailyTime,
                    data.Settings.AutomaticRetentionCount,
                    data.Settings.SafetyRetentionCount
                },
                backups = data.Backups.Select(item => new
                {
                    item.Id,
                    item.FileName,
                    item.DisplayName,
                    item.Type,
                    item.Source,
                    item.Status,
                    createdAtUtc = item.CreatedAtUtc,
                    item.SizeBytes,
                    item.Verified,
                    verifiedAtUtc = item.VerifiedAtUtc,
                    liveRestoredAtUtc = item.LiveRestoredAtUtc,
                    item.LastError,
                    canDownload =
                        data.Capabilities.CanDownloadBackupFiles
                })
            });
        }

        [HttpPost("CreateFull")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Settings.ManageGeneral)]
        public async Task<IActionResult> CreateFull(
            [FromForm] string? displayName,
            CancellationToken cancellationToken)
        {
            var result =
                await _backupService.CreateFullBackupAsync(
                    displayName,
                    cancellationToken);

            string? downloadUrl = null;

            if (result.Success &&
                result.Backup != null)
            {
                downloadUrl =
                    Url.Action(
                        nameof(Download),
                        "Backup",
                        new
                        {
                            area = "Admin",
                            backupId = result.Backup.Id
                        });
            }

            return Json(new
            {
                success = result.Success,
                message = result.Message,
                backupId = result.Backup?.Id,
                fileName = result.Backup?.FileName,
                displayName = result.Backup?.DisplayName,
                downloadUrl
            });
        }

        [HttpPost("SaveSettings")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Settings.ManageGeneral)]
        public async Task<IActionResult> SaveSettings(
            [FromForm] bool autoBackupEnabled,
            [FromForm] string dailyTime,
            CancellationToken cancellationToken)
        {
            try
            {
                var settings =
                    await _backupService.SaveRuntimeSettingsAsync(
                        autoBackupEnabled,
                        dailyTime,
                        cancellationToken);

                return Json(new
                {
                    success = true,
                    message = settings.AutoBackupEnabled
                        ? $"تم تفعيل النسخ التلقائي يوميًا الساعة {settings.DailyTime}."
                        : "تم إيقاف النسخ التلقائي.",
                    settings = new
                    {
                        settings.AutoBackupEnabled,
                        settings.DailyTime,
                        settings.AutomaticRetentionCount,
                        settings.SafetyRetentionCount
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.GetBaseException().Message
                });
            }
        }

        [HttpGet("Download/{backupId}")]
        [RequireAdminPermission(AdminPermissionKeys.Settings.View)]
        public async Task<IActionResult> Download(
            string backupId,
            CancellationToken cancellationToken)
        {
            var file =
                await _backupService.GetDownloadAsync(
                    backupId,
                    cancellationToken);

            if (file == null)
            {
                return NotFound();
            }

            Response.ContentType =
                "application/octet-stream";

            Response.Headers["Content-Disposition"] =
                "attachment; filename*=UTF-8''" +
                Uri.EscapeDataString(
                    file.DownloadName);

            try
            {
                bool copied =
                    await _backupService.CopyBackupToAsync(
                        backupId,
                        Response.Body,
                        cancellationToken);

                if (!copied)
                {
                    return NotFound();
                }

                await Response.Body.FlushAsync(
                    cancellationToken);

                return new EmptyResult();
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
        }

        [HttpPost("RestoreStored")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Settings.ManageGeneral)]
        public async Task<IActionResult> RestoreStored(
            [FromForm] string backupId,
            [FromForm] string confirmation,
            CancellationToken cancellationToken)
        {
            var result =
                await _backupService.RestoreStoredBackupAsync(
                    backupId,
                    confirmation,
                    cancellationToken);

            return Json(new
            {
                success = result.Success,
                message = result.Message
            });
        }

        [HttpPost("RestoreUploaded")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxRestoreUploadBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRestoreUploadBytes)]
        [RequireAdminPermission(AdminPermissionKeys.Settings.ManageGeneral)]
        public async Task<IActionResult> RestoreUploaded(
            IFormFile? backupFile,
            [FromForm] string confirmation,
            CancellationToken cancellationToken)
        {
            if (backupFile == null ||
                backupFile.Length <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "اختر ملف نسخة احتياطية بصيغة .bak أولاً."
                });
            }

            if (backupFile.Length > MaxRestoreUploadBytes)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "حجم ملف النسخة أكبر من الحد المسموح حالياً."
                });
            }

            if (!string.Equals(
                    Path.GetExtension(backupFile.FileName),
                    ".bak",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "الملف المختار يجب أن يكون نسخة SQL Server بصيغة .bak."
                });
            }

            await using var stream =
                backupFile.OpenReadStream();

            var result =
                await _backupService.RestoreUploadedBackupAsync(
                    stream,
                    backupFile.FileName,
                    confirmation,
                    cancellationToken);

            return Json(new
            {
                success = result.Success,
                message = result.Message
            });
        }

        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(AdminPermissionKeys.Settings.ManageGeneral)]
        public async Task<IActionResult> Delete(
            [FromForm] string backupId,
            CancellationToken cancellationToken)
        {
            var result =
                await _backupService.DeleteBackupAsync(
                    backupId,
                    cancellationToken);

            return Json(new
            {
                success = result.Success,
                message = result.Message
            });
        }
    }
}
