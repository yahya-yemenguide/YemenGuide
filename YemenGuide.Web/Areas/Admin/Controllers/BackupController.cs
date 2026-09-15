using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        private readonly IBackupService _backupService;

        public BackupController(
            IBackupService backupService)
        {
            _backupService = backupService;
        }

        [HttpGet("Dashboard")]
        [RequireAdminPermission(
            AdminPermissionKeys.Settings.View)]
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
                    data.Capabilities.ProductVersion,
                    data.Capabilities.Edition,
                    data.Capabilities.RecoveryModel,
                    data.Capabilities.CanBackup,
                    data.Capabilities.CanCreateTestDatabase,
                    data.Capabilities.CanDownloadBackupFiles,
                    data.Capabilities.AllowLiveRestore,
                    data.Capabilities.CanLiveRestore,
                    backupDirectory =
                        data.Capabilities.BackupDirectoryLabel,
                    data.Capabilities.Error
                },
                settings = new
                {
                    data.Settings.AutoBackupEnabled,
                    data.Settings.DailyTime,
                    data.Settings.RetentionCount,
                    data.Settings.VerifyAfterBackup
                },
                backups = data.Backups.Select(item => new
                {
                    item.Id,
                    item.FileName,
                    item.Type,
                    item.Source,
                    item.Status,
                    createdAtUtc = item.CreatedAtUtc,
                    item.SizeBytes,
                    item.Verified,
                    verifiedAtUtc = item.VerifiedAtUtc,
                    item.TestRestorePassed,
                    testRestoreAtUtc =
                        item.TestRestoreAtUtc,
                    liveRestoredAtUtc =
                        item.LiveRestoredAtUtc,
                    item.LastError,
                    canDownload =
                        data.Capabilities
                            .CanDownloadBackupFiles
                })
            });
        }

        [HttpPost("CreateFull")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(
            AdminPermissionKeys.Settings.ManageGeneral)]
        public async Task<IActionResult> CreateFull(
            CancellationToken cancellationToken)
        {
            var result =
                await _backupService.CreateFullBackupAsync(
                    "Manual",
                    cancellationToken);

            return Json(new
            {
                success = result.Success,
                message = result.Message
            });
        }

        [HttpPost("Verify")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(
            AdminPermissionKeys.Settings.ManageGeneral)]
        public async Task<IActionResult> Verify(
            [FromForm] string backupId,
            CancellationToken cancellationToken)
        {
            var result =
                await _backupService.VerifyBackupAsync(
                    backupId,
                    cancellationToken);

            return Json(new
            {
                success = result.Success,
                message = result.Message
            });
        }

        [HttpPost("TestRestore")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(
            AdminPermissionKeys.Settings.ManageGeneral)]
        public async Task<IActionResult> TestRestore(
            [FromForm] string backupId,
            CancellationToken cancellationToken)
        {
            var result =
                await _backupService.TestRestoreAsync(
                    backupId,
                    cancellationToken);

            return Json(new
            {
                success = result.Success,
                message = result.Message
            });
        }

        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(
            AdminPermissionKeys.Settings.ManageGeneral)]
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

        [HttpPost("SaveSettings")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(
            AdminPermissionKeys.Settings.ManageGeneral)]
        public async Task<IActionResult> SaveSettings(
            [FromForm] bool autoBackupEnabled,
            [FromForm] string dailyTime,
            [FromForm] int retentionCount,
            [FromForm] bool verifyAfterBackup,
            CancellationToken cancellationToken)
        {
            var settings =
                await _backupService.SaveRuntimeSettingsAsync(
                    new BackupRuntimeSettings
                    {
                        AutoBackupEnabled =
                            autoBackupEnabled,
                        DailyTime =
                            dailyTime ?? "02:30",
                        RetentionCount =
                            retentionCount,
                        VerifyAfterBackup =
                            verifyAfterBackup
                    },
                    cancellationToken);

            return Json(new
            {
                success = true,
                message =
                    "تم حفظ إعدادات النسخ التلقائي.",
                settings = new
                {
                    settings.AutoBackupEnabled,
                    settings.DailyTime,
                    settings.RetentionCount,
                    settings.VerifyAfterBackup
                }
            });
        }

        [HttpGet("Download/{backupId}")]
        [RequireAdminPermission(
            AdminPermissionKeys.Settings.View)]
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

            return PhysicalFile(
                file.FullPath,
                "application/octet-stream",
                file.DownloadName,
                enableRangeProcessing: true);
        }

        [HttpPost("RestoreLive")]
        [ValidateAntiForgeryToken]
        [RequireAdminPermission(
            AdminPermissionKeys.Settings.ManageGeneral)]
        public async Task<IActionResult> RestoreLive(
            [FromForm] string backupId,
            [FromForm] string confirmation,
            CancellationToken cancellationToken)
        {
            var result =
                await _backupService.RestoreLiveAsync(
                    backupId,
                    confirmation,
                    cancellationToken);

            return Json(new
            {
                success = result.Success,
                message = result.Message
            });
        }
    }
}
