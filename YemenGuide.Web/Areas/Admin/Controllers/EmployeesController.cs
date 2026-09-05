using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Core.Constants;
using YemenGuide.Web.Services.Employees;

namespace YemenGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class EmployeesController : Controller
    {
        private readonly IAdminEmployeeService _employeeService;

        public EmployeesController(IAdminEmployeeService employeeService)
        {
            _employeeService = employeeService;
        }

        [HttpGet]
        public async Task<IActionResult> EmployeeManagement(
            string? tab = null,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0)
            {
                return Challenge();
            }

            var model = await _employeeService.GetManagementPageAsync(
                userId,
                tab,
                cancellationToken);

            if (!model.CanViewEmployees &&
                !model.CanViewRoles &&
                !model.CanViewAuditLogs)
            {
                return Forbid();
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EmployeesTab(
            [FromQuery] AdminEmployeeFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (!await CanAsync(userId, cancellationToken, AdminPermissionKeys.Employees.View))
            {
                return Forbid();
            }

            var model = await _employeeService.GetEmployeesAsync(
                filter,
                userId,
                cancellationToken);

            return PartialView("_EmployeesTab", model);
        }

        [HttpGet]
        public async Task<IActionResult> RolesTab(
            string? search = null,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (!await CanAsync(
                    userId,
                    cancellationToken,
                    AdminPermissionKeys.Roles.View))
            {
                return Forbid();
            }

            var model = await _employeeService.GetRolesAsync(
                search,
                userId,
                cancellationToken);

            return PartialView("_RolesTab", model);
        }

        [HttpGet]
        public async Task<IActionResult> AuditLogsTab(
            [FromQuery] AdminAuditLogFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (!await CanAsync(
                    userId,
                    cancellationToken,
                    AdminPermissionKeys.AuditLogs.View))
            {
                return Forbid();
            }

            var model = await _employeeService.GetAuditLogsAsync(
                filter,
                cancellationToken);

            return PartialView("_AuditLogsTab", model);
        }

        [HttpGet]
        public async Task<IActionResult> EmployeeForm(
            int? id = null,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            var permission = id.HasValue
                ? new[] { AdminPermissionKeys.Employees.Edit }
                : new[] { AdminPermissionKeys.Employees.Create };

            if (!await CanAsync(userId, cancellationToken, permission))
            {
                return Forbid();
            }

            var model = await _employeeService.GetEmployeeFormAsync(
                id,
                cancellationToken);

            if (model == null)
            {
                return NotFound();
            }

            return PartialView("_EmployeeForm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveEmployee(
            AdminEmployeeSaveDto request,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            var isEdit = request.UserId.HasValue && request.UserId.Value > 0;
            var permission = isEdit
                ? new[] { AdminPermissionKeys.Employees.Edit }
                : new[] { AdminPermissionKeys.Employees.Create };

            if (!await CanAsync(userId, cancellationToken, permission))
            {
                return StatusCode(403, new
                {
                    success = false,
                    message = "ليس لديك صلاحية تنفيذ هذه العملية."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = FirstModelError()
                });
            }

            var result = isEdit
                ? await _employeeService.UpdateEmployeeAsync(
                    request,
                    userId,
                    GetIpAddress(),
                    Request.Headers.UserAgent.ToString(),
                    cancellationToken)
                : await _employeeService.CreateEmployeeAsync(
                    request,
                    userId,
                    GetIpAddress(),
                    Request.Headers.UserAgent.ToString(),
                    cancellationToken);

            return StatusCode(result.Success ? 200 : 400, new
            {
                success = result.Success,
                message = result.Message,
                entityId = result.EntityId,
                temporaryPassword = result.TemporaryPassword,
                username = result.Username,
                email = result.Email,
                emailSent = result.EmailSent
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeEmployeeStatus(
            AdminEmployeeStatusDto request,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (!await CanAsync(
                    userId,
                    cancellationToken,
                    AdminPermissionKeys.Employees.ChangeStatus))
            {
                return StatusCode(403, new
                {
                    success = false,
                    message = "ليس لديك صلاحية تغيير حالة الموظف."
                });
            }

            var result = await _employeeService.ChangeEmployeeStatusAsync(
                request,
                userId,
                GetIpAddress(),
                Request.Headers.UserAgent.ToString(),
                cancellationToken);

            return StatusCode(result.Success ? 200 : 400, new
            {
                success = result.Success,
                message = result.Message
            });
        }

        [HttpGet]
        public async Task<IActionResult> RoleForm(
            int? id = null,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (!await CanAsync(
                    userId,
                    cancellationToken,
                    AdminPermissionKeys.Roles.Manage))
            {
                return Forbid();
            }

            var model = await _employeeService.GetRoleFormAsync(
                id,
                cancellationToken);

            if (model == null)
            {
                return NotFound();
            }

            return PartialView("_RoleForm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRole(
            AdminRoleSaveDto request,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (!await CanAsync(
                    userId,
                    cancellationToken,
                    AdminPermissionKeys.Roles.Manage))
            {
                return StatusCode(403, new
                {
                    success = false,
                    message = "ليس لديك صلاحية إدارة الأدوار."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = FirstModelError()
                });
            }

            var result = await _employeeService.SaveRoleAsync(
                request,
                userId,
                GetIpAddress(),
                Request.Headers.UserAgent.ToString(),
                cancellationToken);

            return StatusCode(result.Success ? 200 : 400, new
            {
                success = result.Success,
                message = result.Message,
                entityId = result.EntityId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRoleStatus(
            AdminRoleStatusDto request,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (!await CanAsync(
                    userId,
                    cancellationToken,
                    AdminPermissionKeys.Roles.Manage))
            {
                return StatusCode(403, new
                {
                    success = false,
                    message = "ليس لديك صلاحية إدارة الأدوار."
                });
            }

            var result = await _employeeService.ChangeRoleStatusAsync(
                request,
                userId,
                GetIpAddress(),
                Request.Headers.UserAgent.ToString(),
                cancellationToken);

            return StatusCode(result.Success ? 200 : 400, new
            {
                success = result.Success,
                message = result.Message
            });
        }

        [HttpGet]
        public async Task<IActionResult> CheckAccountAvailability(
            string? username,
            string? email,
            int? excludeUserId = null,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            if (!await CanAsync(
                    userId,
                    cancellationToken,
                    AdminPermissionKeys.Employees.Create,
                    AdminPermissionKeys.Employees.Edit))
            {
                return Forbid();
            }

            var result = await _employeeService.CheckAccountAvailabilityAsync(
                username,
                email,
                excludeUserId,
                cancellationToken);

            return Json(new
            {
                usernameAvailable = result.usernameAvailable,
                emailAvailable = result.emailAvailable
            });
        }

        private async Task<bool> CanAsync(
            int userId,
            CancellationToken cancellationToken,
            params string[] permissionKeys)
        {
            return await _employeeService.HasAnyPermissionAsync(
                userId,
                cancellationToken,
                permissionKeys);
        }

        private int GetCurrentUserId()
        {
            return int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId)
                ? userId
                : 0;
        }

        private string? GetIpAddress()
        {
            var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                return forwarded.Split(',')[0].Trim();
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        private string FirstModelError()
        {
            return ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                ?? "راجع البيانات المدخلة وحاول مرة أخرى.";
        }
    }
}
