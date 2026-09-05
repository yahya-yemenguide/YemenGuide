using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Core.Constants;
using YemenGuide.Core.Entities;
using YemenGuide.Core.Helpers;
using YemenGuide.Data;

namespace YemenGuide.Web.Services.Employees
{
    public sealed class AdminEmployeeService : IAdminEmployeeService
    {
        private static readonly Regex UsernameRegex =
            new(@"^[a-zA-Z0-9_.-]+$", RegexOptions.Compiled);

        private static readonly Regex TemporaryPasswordRegex =
            new(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$", RegexOptions.Compiled);

        private readonly AppDbContext _context;
        private readonly ILogger<AdminEmployeeService> _logger;

        public AdminEmployeeService(
            AppDbContext context,
            ILogger<AdminEmployeeService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<AdminEmployeeManagementPageDto> GetManagementPageAsync(
            int currentAdminUserId,
            string? requestedTab,
            CancellationToken cancellationToken = default)
        {
            var employeesQuery = _context.Users
                .AsNoTracking()
                .Where(user => user.Role == "Admin");

            var employeesCount = await employeesQuery.CountAsync(cancellationToken);
            var activeCount = await employeesQuery.CountAsync(
                user => user.EmploymentStatus == AdminEmployeeStatuses.Active,
                cancellationToken);
            var suspendedCount = await employeesQuery.CountAsync(
                user => user.EmploymentStatus == AdminEmployeeStatuses.Suspended,
                cancellationToken);

            var rolesCount = await _context.AdminRoles
                .AsNoTracking()
                .CountAsync(cancellationToken);

            var auditLogsCount = await _context.AdminAuditLogs
                .AsNoTracking()
                .CountAsync(cancellationToken);

            var canViewEmployees = await HasAnyPermissionAsync(
                currentAdminUserId,
                cancellationToken,
                AdminPermissionKeys.Employees.View);

            var canCreateEmployees = await HasAnyPermissionAsync(
                currentAdminUserId,
                cancellationToken,
                AdminPermissionKeys.Employees.Create);

            var canEditEmployees = await HasAnyPermissionAsync(
                currentAdminUserId,
                cancellationToken,
                AdminPermissionKeys.Employees.Edit);

            var canChangeState = await HasAnyPermissionAsync(
                currentAdminUserId,
                cancellationToken,
                AdminPermissionKeys.Employees.ChangeStatus);

            var canViewRoles = await HasAnyPermissionAsync(
                currentAdminUserId,
                cancellationToken,
                AdminPermissionKeys.Roles.View);

            var canManageRoles = await HasAnyPermissionAsync(
                currentAdminUserId,
                cancellationToken,
                AdminPermissionKeys.Roles.Manage);

            var canViewAudit = await HasAnyPermissionAsync(
                currentAdminUserId,
                cancellationToken,
                AdminPermissionKeys.AuditLogs.View);

            var defaultTab = NormalizeTab(requestedTab);
            if (defaultTab == "employees" && !canViewEmployees)
            {
                defaultTab = canViewRoles
                    ? "roles"
                    : canViewAudit
                        ? "audit"
                        : "employees";
            }
            else if (defaultTab == "roles" && !canViewRoles)
            {
                defaultTab = canViewEmployees
                    ? "employees"
                    : canViewAudit
                        ? "audit"
                        : "roles";
            }
            else if (defaultTab == "audit" && !canViewAudit)
            {
                defaultTab = canViewEmployees
                    ? "employees"
                    : canViewRoles
                        ? "roles"
                        : "audit";
            }

            return new AdminEmployeeManagementPageDto
            {
                EmployeesCount = employeesCount,
                RolesCount = rolesCount,
                AuditLogsCount = auditLogsCount,
                ActiveEmployeesCount = activeCount,
                SuspendedEmployeesCount = suspendedCount,
                CanViewEmployees = canViewEmployees,
                CanCreateEmployees = canCreateEmployees,
                CanEditEmployees = canEditEmployees,
                CanChangeEmployeeState = canChangeState,
                CanResetPasswords = false,
                CanViewRoles = canViewRoles,
                CanManageRoles = canManageRoles,
                CanViewAuditLogs = canViewAudit,
                DefaultTab = defaultTab
            };
        }

        public async Task<AdminEmployeesTabDto> GetEmployeesAsync(
            AdminEmployeeFilterDto filter,
            int currentAdminUserId,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminEmployeeFilterDto();
            filter.PageNumber = Math.Max(1, filter.PageNumber);
            filter.PageSize = filter.PageSize is >= 5 and <= 100
                ? filter.PageSize
                : 12;
            filter.Search = Normalize(filter.Search, 120);
            filter.Department = Normalize(filter.Department, 100);
            filter.EmploymentStatus = Normalize(filter.EmploymentStatus, 20);

            var baseQuery = _context.Users
                .AsNoTracking()
                .Where(user => user.Role == "Admin");

            var activeCount = await baseQuery.CountAsync(
                user => user.EmploymentStatus == AdminEmployeeStatuses.Active,
                cancellationToken);
            var suspendedCount = await baseQuery.CountAsync(
                user => user.EmploymentStatus == AdminEmployeeStatuses.Suspended,
                cancellationToken);

            IQueryable<User> query = baseQuery;

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search;
                query = query.Where(user =>
                    user.FullName.Contains(search) ||
                    user.Username.Contains(search) ||
                    (user.ContactInfo != null && user.ContactInfo.Contains(search)) ||
                    (user.PhoneNumber != null && user.PhoneNumber.Contains(search)) ||
                    (user.EmployeeNumber != null && user.EmployeeNumber.Contains(search)));
            }

            if (filter.AdminRoleId.HasValue && filter.AdminRoleId.Value > 0)
            {
                query = query.Where(user => user.AdminRoleId == filter.AdminRoleId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Department))
            {
                query = query.Where(user => user.Department == filter.Department);
            }

            if (!string.IsNullOrWhiteSpace(filter.EmploymentStatus))
            {
                query = query.Where(user => user.EmploymentStatus == filter.EmploymentStatus);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var employees = await query
                .OrderByDescending(user => user.AdminRole != null && user.AdminRole.IsSystemRole)
                .ThenBy(user => user.FullName)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(user => new AdminEmployeeListItemDto
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    Username = user.Username,
                    ContactInfo = user.ContactInfo,
                    PhoneNumber = user.PhoneNumber,
                    EmployeeNumber = user.EmployeeNumber,
                    Department = user.Department,
                    JobTitle = user.JobTitle,
                    EmploymentStatus = user.EmploymentStatus ?? string.Empty,
                    HireDate = user.HireDate,
                    LastLoginAt = user.LastLoginAt,
                    MustChangePassword = user.MustChangePassword,
                    AdminRoleId = user.AdminRoleId,
                    AdminRoleName = user.AdminRole != null ? user.AdminRole.RoleName : null,
                    IsSystemAdmin = user.AdminRole != null && user.AdminRole.IsSystemRole,
                    IsRoleActive = user.AdminRole != null && user.AdminRole.IsActive == true
                })
                .ToListAsync(cancellationToken);

            var roles = await GetRoleOptionsAsync(
                includeSystemRole: true,
                selectedRoleId: null,
                includePermissionGroups: false,
                cancellationToken);

            var departments = await baseQuery
                .Where(user => user.Department != null && user.Department != string.Empty)
                .Select(user => user.Department!)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync(cancellationToken);

            return new AdminEmployeesTabDto
            {
                Employees = employees,
                Roles = roles,
                Departments = departments,
                Filter = filter,
                TotalCount = totalCount,
                ActiveCount = activeCount,
                SuspendedCount = suspendedCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                CanCreate = await HasAnyPermissionAsync(
                    currentAdminUserId,
                    cancellationToken,
                    AdminPermissionKeys.Employees.Create),
                CanEdit = await HasAnyPermissionAsync(
                    currentAdminUserId,
                    cancellationToken,
                    AdminPermissionKeys.Employees.Edit),
                CanChangeState = await HasAnyPermissionAsync(
                    currentAdminUserId,
                    cancellationToken,
                    AdminPermissionKeys.Employees.ChangeStatus),
                CanResetPassword = false
            };
        }

        public async Task<AdminEmployeeFormDto?> GetEmployeeFormAsync(
            int? userId,
            CancellationToken cancellationToken = default)
        {
            AdminEmployeeSaveDto employee = new()
            {
                HireDate = DateTime.Today
            };

            var isProtected = false;
            int? selectedRoleId = null;

            if (userId.HasValue && userId.Value > 0)
            {
                var entity = await _context.Users
                    .AsNoTracking()
                    .Include(user => user.AdminRole)
                    .FirstOrDefaultAsync(user =>
                        user.UserId == userId.Value &&
                        user.Role == "Admin",
                        cancellationToken);

                if (entity == null)
                {
                    return null;
                }

                var (firstName, lastName) = SplitName(entity.FullName);
                employee = new AdminEmployeeSaveDto
                {
                    UserId = entity.UserId,
                    FirstName = firstName,
                    LastName = lastName,
                    Username = entity.Username,
                    ContactInfo = entity.ContactInfo ?? string.Empty,
                    PhoneNumber = entity.PhoneNumber,
                    EmployeeNumber = entity.EmployeeNumber ?? string.Empty,
                    Department = entity.Department,
                    JobTitle = entity.JobTitle,
                    HireDate = entity.HireDate,
                    AdminRoleId = entity.AdminRoleId ?? 0
                };

                selectedRoleId = entity.AdminRoleId;
                isProtected = entity.AdminRole?.IsSystemRole == true;
            }

            var roles = await GetRoleOptionsAsync(
                includeSystemRole: isProtected,
                selectedRoleId,
                includePermissionGroups: true,
                cancellationToken);

            return new AdminEmployeeFormDto
            {
                IsEdit = userId.HasValue && userId.Value > 0,
                IsProtectedSystemAdmin = isProtected,
                Employee = employee,
                Roles = roles
            };
        }

        public async Task<AdminOperationResultDto> CreateEmployeeAsync(
            AdminEmployeeSaveDto request,
            int currentAdminUserId,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default)
        {
            NormalizeEmployeeRequest(request);

            var validation = await ValidateEmployeeRequestAsync(
                request,
                isCreate: true,
                cancellationToken);
            if (validation != null)
            {
                return AdminOperationResultDto.Fail(validation);
            }

            var role = await _context.AdminRoles
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.AdminRoleId == request.AdminRoleId &&
                    item.IsActive == true &&
                    !item.IsSystemRole,
                    cancellationToken);

            if (role == null)
            {
                return AdminOperationResultDto.Fail(
                    "الدور الوظيفي غير متاح. اختر دورًا نشطًا وغير محمي.");
            }

            var temporaryPassword = string.IsNullOrWhiteSpace(request.TemporaryPassword)
                ? GenerateTemporaryPassword()
                : request.TemporaryPassword!;

            if (!TemporaryPasswordRegex.IsMatch(temporaryPassword))
            {
                return AdminOperationResultDto.Fail(
                    "كلمة المرور المؤقتة يجب أن تكون 8 أحرف على الأقل وتحتوي على حرف إنجليزي ورقم.");
            }

            var employee = new User
            {
                FullName = $"{request.FirstName} {request.LastName}".Trim(),
                Username = request.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword, workFactor: 11),
                ContactInfo = request.ContactInfo,
                PhoneNumber = request.PhoneNumber,
                CreatedAt = DateTime.Now,
                Role = "Admin",
                AccountState = "Active",
                IsDarkMode = false,
                IsOnline = false,
                WarningCount = 0,
                IsEmailVerified = true,
                WalletBalance = 0,
                PreferredCurrency = "ريال يمني",
                ReceiveOffers = true,
                IsAgreedToTerms = true,
                AgreedToTermsDate = DateTime.Now,
                PreferredLanguage = "ar",
                AdminRoleId = role.AdminRoleId,
                // بيانات تنظيمية يولدها النظام تلقائيًا ولا نحمّل الأدمن تعبئتها.
                EmployeeNumber = null,
                Department = null,
                EmploymentStatus = AdminEmployeeStatuses.Active,
                HireDate = DateTime.Today,
                JobTitle = role.RoleName,
                MustChangePassword = true
            };

            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                _context.Users.Add(employee);
                await _context.SaveChangesAsync(cancellationToken);

                // رقم وظيفي داخلي ثابت وفريد مبني على UserID.
                employee.EmployeeNumber = $"EMP-{employee.UserId:D6}";
                await _context.SaveChangesAsync(cancellationToken);

                await AddAuditLogAsync(
                    currentAdminUserId,
                    "EmployeeCreated",
                    "User",
                    employee.UserId.ToString(),
                    $"تم إنشاء حساب الموظف {employee.FullName} وربطه بالدور {role.RoleName}.",
                    null,
                    new
                    {
                        employee.UserId,
                        employee.FullName,
                        employee.Username,
                        employee.ContactInfo,
                        employee.PhoneNumber,
                        employee.EmployeeNumber,
                        employee.Department,
                        employee.JobTitle,
                        employee.AdminRoleId,
                        RoleName = role.RoleName,
                        employee.EmploymentStatus,
                        employee.HireDate,
                        employee.MustChangePassword
                    },
                    ipAddress,
                    userAgent,
                    true,
                    null,
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                var emailSent =
                    await TrySendEmployeeCredentialsEmailAsync(
                        employee.ContactInfo,
                        employee.FullName,
                        employee.Username,
                        temporaryPassword,
                        role.RoleName,
                        isPasswordReset: false);

                return AdminOperationResultDto.Ok(
                    emailSent
                        ? "تم إنشاء الموظف وإرسال بيانات الدخول المؤقتة إلى بريده الإلكتروني."
                        : "تم إنشاء الموظف، لكن تعذر إرسال بيانات الدخول إلى البريد. يمكنك نسخها من النافذة الحالية.",
                    employee.UserId,
                    temporaryPassword,
                    employee.Username,
                    employee.ContactInfo,
                    emailSent);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(exception, "تعذر إنشاء موظف الإدارة {Username}.", request.Username);
                return AdminOperationResultDto.Fail(
                    "تعذر إنشاء الموظف. لم يتم حفظ أي بيانات.");
            }
        }

        public async Task<AdminOperationResultDto> UpdateEmployeeAsync(
            AdminEmployeeSaveDto request,
            int currentAdminUserId,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default)
        {
            if (!request.UserId.HasValue || request.UserId.Value <= 0)
            {
                return AdminOperationResultDto.Fail("رقم الموظف غير صحيح.");
            }

            NormalizeEmployeeRequest(request);

            var employee = await _context.Users
                .Include(user => user.AdminRole)
                .FirstOrDefaultAsync(user =>
                    user.UserId == request.UserId.Value &&
                    user.Role == "Admin",
                    cancellationToken);

            if (employee == null)
            {
                return AdminOperationResultDto.Fail("الموظف غير موجود.");
            }

            if (employee.AdminRole?.IsSystemRole == true)
            {
                return AdminOperationResultDto.Fail(
                    "حساب المدير الرئيسي محمي ولا يتم تعديله من إدارة الموظفين.");
            }

            var validation = await ValidateEmployeeRequestAsync(
                request,
                isCreate: false,
                cancellationToken);
            if (validation != null)
            {
                return AdminOperationResultDto.Fail(validation);
            }

            var role = await _context.AdminRoles
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.AdminRoleId == request.AdminRoleId &&
                    item.IsActive == true &&
                    !item.IsSystemRole,
                    cancellationToken);

            if (role == null)
            {
                return AdminOperationResultDto.Fail("الدور المحدد غير متاح.");
            }

            var oldValues = new
            {
                employee.FullName,
                employee.Username,
                employee.ContactInfo,
                employee.PhoneNumber,
                employee.EmployeeNumber,
                employee.Department,
                employee.JobTitle,
                employee.HireDate,
                employee.AdminRoleId,
                RoleName = employee.AdminRole?.RoleName
            };

            employee.FullName = $"{request.FirstName} {request.LastName}".Trim();
            employee.Username = request.Username;
            employee.ContactInfo = request.ContactInfo;
            employee.PhoneNumber = request.PhoneNumber;
            // الرقم الوظيفي وتاريخ التعيين يولدهما النظام ولا يتغيران من نموذج الحساب.
            employee.JobTitle = role.RoleName;
            employee.AdminRoleId = role.AdminRoleId;
            employee.IsEmailVerified = true;

            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);

                await AddAuditLogAsync(
                    currentAdminUserId,
                    "EmployeeUpdated",
                    "User",
                    employee.UserId.ToString(),
                    $"تم تعديل بيانات الموظف {employee.FullName}.",
                    oldValues,
                    new
                    {
                        employee.FullName,
                        employee.Username,
                        employee.ContactInfo,
                        employee.PhoneNumber,
                        employee.EmployeeNumber,
                        employee.Department,
                        employee.JobTitle,
                        employee.HireDate,
                        employee.AdminRoleId,
                        RoleName = role.RoleName
                    },
                    ipAddress,
                    userAgent,
                    true,
                    null,
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return AdminOperationResultDto.Ok(
                    "تم تحديث بيانات الموظف ودوره بنجاح.",
                    employee.UserId);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(exception, "تعذر تعديل الموظف {UserId}.", employee.UserId);
                return AdminOperationResultDto.Fail("تعذر حفظ تعديلات الموظف.");
            }
        }

        public async Task<AdminOperationResultDto> ChangeEmployeeStatusAsync(
            AdminEmployeeStatusDto request,
            int currentAdminUserId,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default)
        {
            if (request.UserId <= 0)
            {
                return AdminOperationResultDto.Fail("رقم الموظف غير صحيح.");
            }

            if (request.UserId == currentAdminUserId)
            {
                return AdminOperationResultDto.Fail(
                    "لا يمكنك إيقاف حسابك الإداري من هذه الصفحة.");
            }

            var allowedStatuses = new[]
            {
                AdminEmployeeStatuses.Active,
                AdminEmployeeStatuses.Suspended
            };

            if (!allowedStatuses.Contains(request.EmploymentStatus))
            {
                return AdminOperationResultDto.Fail("الحالة المطلوبة غير مدعومة هنا.");
            }

            var employee = await _context.Users
                .Include(user => user.AdminRole)
                .FirstOrDefaultAsync(user =>
                    user.UserId == request.UserId &&
                    user.Role == "Admin",
                    cancellationToken);

            if (employee == null)
            {
                return AdminOperationResultDto.Fail("الموظف غير موجود.");
            }

            if (employee.AdminRole?.IsSystemRole == true)
            {
                return AdminOperationResultDto.Fail(
                    "لا يمكن إيقاف حساب المدير الرئيسي النظامي.");
            }

            var oldStatus = employee.EmploymentStatus;
            employee.EmploymentStatus = request.EmploymentStatus;
            if (request.EmploymentStatus == AdminEmployeeStatuses.Active)
            {
                employee.EndDate = null;
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await AddAuditLogAsync(
                    currentAdminUserId,
                    "EmployeeStatusChanged",
                    "User",
                    employee.UserId.ToString(),
                    $"تم تغيير حالة الموظف {employee.FullName} إلى {AdminEmployeeStatuses.ArabicLabel(employee.EmploymentStatus)}.",
                    new { EmploymentStatus = oldStatus },
                    new { employee.EmploymentStatus },
                    ipAddress,
                    userAgent,
                    true,
                    null,
                    cancellationToken);

                return AdminOperationResultDto.Ok(
                    request.EmploymentStatus == AdminEmployeeStatuses.Active
                        ? "تم تفعيل الموظف بنجاح."
                        : "تم إيقاف الموظف بنجاح.",
                    employee.UserId);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "تعذر تغيير حالة الموظف {UserId}.", request.UserId);
                return AdminOperationResultDto.Fail("تعذر تغيير حالة الموظف.");
            }
        }

        public async Task<AdminRolesTabDto> GetRolesAsync(
            string? search,
            int currentAdminUserId,
            CancellationToken cancellationToken = default)
        {
            search = Normalize(search, 100);

            var query = _context.AdminRoles.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(role =>
                    role.RoleName.Contains(search) ||
                    (role.Description != null && role.Description.Contains(search)));
            }

            var roles = await query
                .OrderByDescending(role => role.IsSystemRole)
                .ThenBy(role => role.RoleName)
                .Select(role => new AdminRoleListItemDto
                {
                    AdminRoleId = role.AdminRoleId,
                    RoleName = role.RoleName,
                    Description = role.Description,
                    IsActive = role.IsActive == true,
                    IsSystemRole = role.IsSystemRole,
                    EmployeesCount = role.Users.Count(user => user.Role == "Admin"),
                    PermissionsCount = role.AdminRolePermissions.Count(),
                    CreatedAt = role.CreatedAt,
                    PermissionGroupNames = role.AdminRolePermissions
                        .Where(link => link.AdminPermission.IsActive == true)
                        .Select(link => link.AdminPermission.PermissionGroup)
                        .Distinct()
                        .ToList()
                })
                .ToListAsync(cancellationToken);

            return new AdminRolesTabDto
            {
                Roles = roles,
                Search = search,
                ActiveRolesCount = roles.Count(role => role.IsActive),
                TotalPermissionsCount = await _context.AdminPermissions
                    .AsNoTracking()
                    .CountAsync(permission => permission.IsActive == true, cancellationToken),
                CanManage = await HasAnyPermissionAsync(
                    currentAdminUserId,
                    cancellationToken,
                    AdminPermissionKeys.Roles.Manage)
            };
        }

        public async Task<AdminRoleFormDto?> GetRoleFormAsync(
            int? adminRoleId,
            CancellationToken cancellationToken = default)
        {
            var roleDto = new AdminRoleSaveDto
            {
                IsActive = true
            };
            var selectedPermissionIds = new HashSet<int>();
            var isSystemRole = false;

            if (adminRoleId.HasValue && adminRoleId.Value > 0)
            {
                var role = await _context.AdminRoles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.AdminRoleId == adminRoleId.Value, cancellationToken);

                if (role == null)
                {
                    return null;
                }

                selectedPermissionIds = (await _context.AdminRolePermissions
                    .AsNoTracking()
                    .Where(link => link.AdminRoleId == role.AdminRoleId)
                    .Select(link => link.AdminPermissionId)
                    .ToListAsync(cancellationToken))
                    .ToHashSet();

                selectedPermissionIds = (
                    await ExpandRequiredViewPermissionsAsync(
                        selectedPermissionIds,
                        cancellationToken))
                    .ToHashSet();

                roleDto = new AdminRoleSaveDto
                {
                    AdminRoleId = role.AdminRoleId,
                    RoleName = role.RoleName,
                    Description = role.Description,
                    IsActive = role.IsActive == true,
                    PermissionIds = selectedPermissionIds.ToList()
                };

                isSystemRole = role.IsSystemRole;
            }

            var permissions = await _context.AdminPermissions
                .AsNoTracking()
                .Where(permission => permission.IsActive == true)
                .OrderBy(permission => permission.PermissionGroup)
                .ThenBy(permission => permission.PermissionName)
                .Select(permission => new AdminPermissionItemDto
                {
                    AdminPermissionId = permission.AdminPermissionId,
                    PermissionKey = permission.PermissionKey,
                    PermissionName = permission.PermissionName,
                    PermissionGroup = permission.PermissionGroup,
                    Description = permission.Description,
                    IsSensitive = permission.IsSensitive,
                    IsActive = permission.IsActive == true,
                    IsSelected = selectedPermissionIds.Contains(permission.AdminPermissionId),
                    IsRequired = false
                })
                .ToListAsync(cancellationToken);

            var groups = permissions
                .GroupBy(permission => permission.PermissionGroup)
                .Select(group => new AdminPermissionGroupDto
                {
                    GroupName = group.Key,
                    Permissions = group.ToList()
                })
                .ToList();

            return new AdminRoleFormDto
            {
                IsEdit = adminRoleId.HasValue && adminRoleId.Value > 0,
                IsSystemRole = isSystemRole,
                Role = roleDto,
                PermissionGroups = groups
            };
        }

        public async Task<AdminOperationResultDto> SaveRoleAsync(
            AdminRoleSaveDto request,
            int currentAdminUserId,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default)
        {
            request.RoleName = Normalize(request.RoleName, 100) ?? string.Empty;
            request.Description = Normalize(request.Description, 500);
            request.PermissionIds = (request.PermissionIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (request.RoleName.Length < 2)
            {
                return AdminOperationResultDto.Fail("اسم الدور مطلوب.");
            }

            if (request.PermissionIds.Count == 0)
            {
                return AdminOperationResultDto.Fail(
                    "اختر صلاحية واحدة على الأقل للدور.");
            }

            var duplicateName = await _context.AdminRoles
                .AsNoTracking()
                .AnyAsync(role =>
                    role.RoleName == request.RoleName &&
                    (!request.AdminRoleId.HasValue || role.AdminRoleId != request.AdminRoleId.Value),
                    cancellationToken);

            if (duplicateName)
            {
                return AdminOperationResultDto.Fail("يوجد دور آخر بنفس الاسم.");
            }

            var validPermissionIds = await _context.AdminPermissions
                .AsNoTracking()
                .Where(permission =>
                    permission.IsActive == true &&
                    request.PermissionIds.Contains(permission.AdminPermissionId))
                .Select(permission => permission.AdminPermissionId)
                .ToListAsync(cancellationToken);

            if (validPermissionIds.Count != request.PermissionIds.Count)
            {
                return AdminOperationResultDto.Fail(
                    "تحتوي الصلاحيات المختارة على صلاحية غير موجودة أو غير نشطة.");
            }

            // الصلاحيات التنفيذية تعتمد على صلاحية المشاهدة الأساسية للقسم.
            // مثال: من يملك Users.ChangeState يجب أن يملك Users.View أيضًا حتى لا
            // يظهر له القسم في القائمة ثم يصطدم بمنع عند فتح الصفحة.
            validPermissionIds = await ExpandRequiredViewPermissionsAsync(
                validPermissionIds,
                cancellationToken);

            AdminRole role;
            object? oldValues = null;
            var actionType = "AdminRoleCreated";

            if (request.AdminRoleId.HasValue && request.AdminRoleId.Value > 0)
            {
                var existingRole = await _context.AdminRoles
                    .FirstOrDefaultAsync(
                        item => item.AdminRoleId == request.AdminRoleId.Value,
                        cancellationToken);

                if (existingRole == null)
                {
                    return AdminOperationResultDto.Fail("الدور غير موجود.");
                }

                role = existingRole;

                if (role.IsSystemRole)
                {
                    return AdminOperationResultDto.Fail(
                        "دور المدير الرئيسي محمي ولا يمكن تعديله.");
                }

                var oldPermissionIds = await _context.AdminRolePermissions
                    .AsNoTracking()
                    .Where(link => link.AdminRoleId == role.AdminRoleId)
                    .Select(link => link.AdminPermissionId)
                    .ToListAsync(cancellationToken);

                oldValues = new
                {
                    role.RoleName,
                    role.Description,
                    IsActive = role.IsActive == true,
                    PermissionIds = oldPermissionIds
                };

                role.RoleName = request.RoleName;
                role.Description = request.Description;
                role.IsActive = request.IsActive;
                actionType = "AdminRoleUpdated";
            }
            else
            {
                role = new AdminRole
                {
                    RoleName = request.RoleName,
                    Description = request.Description,
                    IsActive = request.IsActive,
                    IsSystemRole = false,
                    CreatedAt = DateTime.Now,
                    CreatedByUserId = currentAdminUserId
                };
                _context.AdminRoles.Add(role);
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);

                var oldLinks = await _context.AdminRolePermissions
                    .Where(link => link.AdminRoleId == role.AdminRoleId)
                    .ToListAsync(cancellationToken);

                if (oldLinks.Count > 0)
                {
                    _context.AdminRolePermissions.RemoveRange(oldLinks);
                }

                foreach (var permissionId in validPermissionIds)
                {
                    _context.AdminRolePermissions.Add(new AdminRolePermission
                    {
                        AdminRoleId = role.AdminRoleId,
                        AdminPermissionId = permissionId,
                        GrantedAt = DateTime.Now,
                        GrantedByUserId = currentAdminUserId
                    });
                }

                await _context.SaveChangesAsync(cancellationToken);

                await AddAuditLogAsync(
                    currentAdminUserId,
                    actionType,
                    "AdminRole",
                    role.AdminRoleId.ToString(),
                    $"تم {(actionType == "AdminRoleCreated" ? "إنشاء" : "تعديل")} الدور {role.RoleName}.",
                    oldValues,
                    new
                    {
                        role.RoleName,
                        role.Description,
                        IsActive = role.IsActive == true,
                        PermissionIds = validPermissionIds
                    },
                    ipAddress,
                    userAgent,
                    true,
                    null,
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return AdminOperationResultDto.Ok(
                    "تم حفظ الدور والصلاحيات بنجاح.",
                    role.AdminRoleId);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(exception, "تعذر حفظ الدور {RoleName}.", request.RoleName);
                return AdminOperationResultDto.Fail("تعذر حفظ الدور والصلاحيات.");
            }
        }

        public async Task<AdminOperationResultDto> ChangeRoleStatusAsync(
            AdminRoleStatusDto request,
            int currentAdminUserId,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken = default)
        {
            var role = await _context.AdminRoles
                .FirstOrDefaultAsync(item => item.AdminRoleId == request.AdminRoleId, cancellationToken);

            if (role == null)
            {
                return AdminOperationResultDto.Fail("الدور غير موجود.");
            }

            if (role.IsSystemRole)
            {
                return AdminOperationResultDto.Fail(
                    "دور المدير الرئيسي محمي ولا يمكن إيقافه.");
            }

            var oldStatus = role.IsActive == true;
            role.IsActive = request.IsActive;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await AddAuditLogAsync(
                    currentAdminUserId,
                    "AdminRoleStatusChanged",
                    "AdminRole",
                    role.AdminRoleId.ToString(),
                    $"تم {(request.IsActive ? "تفعيل" : "إيقاف")} الدور {role.RoleName}.",
                    new { IsActive = oldStatus },
                    new { IsActive = request.IsActive },
                    ipAddress,
                    userAgent,
                    true,
                    null,
                    cancellationToken);

                return AdminOperationResultDto.Ok(
                    request.IsActive
                        ? "تم تفعيل الدور بنجاح."
                        : "تم إيقاف الدور. الموظفون المرتبطون به لن تكون صلاحياته فعالة حتى إعادة تفعيله.",
                    role.AdminRoleId);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "تعذر تغيير حالة الدور {RoleId}.", request.AdminRoleId);
                return AdminOperationResultDto.Fail("تعذر تغيير حالة الدور.");
            }
        }

        public async Task<AdminAuditLogsTabDto> GetAuditLogsAsync(
            AdminAuditLogFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminAuditLogFilterDto();
            filter.PageNumber = Math.Max(1, filter.PageNumber);
            filter.PageSize = filter.PageSize is >= 5 and <= 100
                ? filter.PageSize
                : 15;
            filter.Search = Normalize(filter.Search, 120);
            filter.ActionType = Normalize(filter.ActionType, 100);

            var query = _context.AdminAuditLogs
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search;
                query = query.Where(log =>
                    log.ActionType.Contains(search) ||
                    (log.Description != null && log.Description.Contains(search)) ||
                    (log.EntityType != null && log.EntityType.Contains(search)) ||
                    (log.EntityId != null && log.EntityId.Contains(search)) ||
                    (log.User != null &&
                     (log.User.FullName.Contains(search) || log.User.Username.Contains(search))));
            }

            if (!string.IsNullOrWhiteSpace(filter.ActionType))
            {
                query = query.Where(log => log.ActionType == filter.ActionType);
            }

            if (filter.IsSuccessful.HasValue)
            {
                query = query.Where(log => log.IsSuccessful == filter.IsSuccessful.Value);
            }

            if (filter.FromDate.HasValue)
            {
                var from = filter.FromDate.Value.Date;
                query = query.Where(log => log.OccurredAt >= from);
            }

            if (filter.ToDate.HasValue)
            {
                var to = filter.ToDate.Value.Date.AddDays(1);
                query = query.Where(log => log.OccurredAt < to);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(log => log.OccurredAt)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(log => new AdminAuditLogItemDto
                {
                    AdminAuditLogId = log.AdminAuditLogId,
                    UserId = log.UserId,
                    UserName = log.User != null ? log.User.FullName : null,
                    Username = log.User != null ? log.User.Username : null,
                    ActionType = log.ActionType,
                    EntityType = log.EntityType,
                    EntityId = log.EntityId,
                    Description = log.Description,
                    IpAddress = log.Ipaddress,
                    OccurredAt = log.OccurredAt,
                    IsSuccessful = log.IsSuccessful == true,
                    FailureReason = log.FailureReason
                })
                .ToListAsync(cancellationToken);

            var actionTypes = await _context.AdminAuditLogs
                .AsNoTracking()
                .Select(log => log.ActionType)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync(cancellationToken);

            return new AdminAuditLogsTabDto
            {
                Items = items,
                ActionTypes = actionTypes,
                Filter = filter,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        private async Task<List<int>> ExpandRequiredViewPermissionsAsync(
            IReadOnlyCollection<int> permissionIds,
            CancellationToken cancellationToken)
        {
            var selectedPermissions = await _context.AdminPermissions
                .AsNoTracking()
                .Where(permission =>
                    permission.IsActive == true &&
                    permissionIds.Contains(permission.AdminPermissionId))
                .Select(permission => new
                {
                    permission.AdminPermissionId,
                    permission.PermissionKey
                })
                .ToListAsync(cancellationToken);

            var selectedKeys = new HashSet<string>(
                selectedPermissions.Select(permission => permission.PermissionKey),
                StringComparer.OrdinalIgnoreCase);

            var dependencies = new Dictionary<string, string[]>(
                StringComparer.OrdinalIgnoreCase)
            {
                [AdminPermissionKeys.Users.View] = new[]
                {
                    AdminPermissionKeys.Users.ViewVerificationDocuments,
                    AdminPermissionKeys.Users.ChangeState,
                    AdminPermissionKeys.Users.Delete
                },
                [AdminPermissionKeys.Places.View] = new[]
                {
                    AdminPermissionKeys.Places.Create,
                    AdminPermissionKeys.Places.Edit,
                    AdminPermissionKeys.Places.Review,
                    AdminPermissionKeys.Places.ChangeStatus,
                    AdminPermissionKeys.Places.Delete,
                    AdminPermissionKeys.Places.ReassignOwner
                },
                [AdminPermissionKeys.MasterData.View] = new[]
                {
                    AdminPermissionKeys.MasterData.ManageCategories,
                    AdminPermissionKeys.MasterData.ManageCities
                },
                [AdminPermissionKeys.Cases.View] = new[]
                {
                    AdminPermissionKeys.Cases.Create,
                    AdminPermissionKeys.Cases.Assign,
                    AdminPermissionKeys.Cases.Manage,
                    AdminPermissionKeys.Cases.Escalate,
                    AdminPermissionKeys.Cases.Resolve,
                    AdminPermissionKeys.Cases.Reopen
                },
                [AdminPermissionKeys.Finance.View] = new[]
                {
                    AdminPermissionKeys.Finance.ReviewTopUps,
                    AdminPermissionKeys.Finance.ManageCommissionAccounts,
                    AdminPermissionKeys.Finance.ManagePlatformWallets,
                    AdminPermissionKeys.Finance.ManageSettings,
                    AdminPermissionKeys.Finance.Reports
                },
                [AdminPermissionKeys.Advertisements.View] = new[]
                {
                    AdminPermissionKeys.Advertisements.Review,
                    AdminPermissionKeys.Advertisements.ManagePlans,
                    AdminPermissionKeys.Advertisements.Control,
                    AdminPermissionKeys.Advertisements.Reports
                },
                [AdminPermissionKeys.Settings.View] = new[]
                {
                    AdminPermissionKeys.Settings.ManageGeneral,
                    AdminPermissionKeys.Settings.ManagePaymentNetworks,
                    AdminPermissionKeys.Settings.ManageHeroSlides
                },
                [AdminPermissionKeys.Employees.View] = new[]
                {
                    AdminPermissionKeys.Employees.Create,
                    AdminPermissionKeys.Employees.Edit,
                    AdminPermissionKeys.Employees.ChangeStatus
                },
                [AdminPermissionKeys.Roles.View] = new[]
                {
                    AdminPermissionKeys.Roles.Manage
                }
            };

            var requiredViewKeys = dependencies
                .Where(pair =>
                    !selectedKeys.Contains(pair.Key) &&
                    pair.Value.Any(selectedKeys.Contains))
                .Select(pair => pair.Key)
                .ToArray();

            if (requiredViewKeys.Length == 0)
            {
                return selectedPermissions
                    .Select(permission => permission.AdminPermissionId)
                    .Distinct()
                    .ToList();
            }

            var requiredViewIds = await _context.AdminPermissions
                .AsNoTracking()
                .Where(permission =>
                    permission.IsActive == true &&
                    requiredViewKeys.Contains(permission.PermissionKey))
                .Select(permission => permission.AdminPermissionId)
                .ToListAsync(cancellationToken);

            return selectedPermissions
                .Select(permission => permission.AdminPermissionId)
                .Concat(requiredViewIds)
                .Distinct()
                .ToList();
        }

        public async Task<bool> HasAnyPermissionAsync(
            int currentAdminUserId,
            CancellationToken cancellationToken = default,
            params string[] permissionKeys)
        {
            if (currentAdminUserId <= 0)
            {
                return false;
            }

            var admin = await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == currentAdminUserId && user.Role == "Admin")
                .Select(user => new
                {
                    user.AccountState,
                    user.EmploymentStatus,
                    user.AdminRoleId,
                    IsRoleActive = user.AdminRole != null && user.AdminRole.IsActive == true,
                    IsSystemRole = user.AdminRole != null && user.AdminRole.IsSystemRole
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (admin == null ||
                !string.Equals(admin.AccountState, "Active", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(admin.EmploymentStatus, AdminEmployeeStatuses.Active, StringComparison.OrdinalIgnoreCase) ||
                !admin.AdminRoleId.HasValue ||
                !admin.IsRoleActive)
            {
                return false;
            }

            if (admin.IsSystemRole)
            {
                return true;
            }

            var keys = (permissionKeys ?? Array.Empty<string>())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (keys.Length == 0)
            {
                return false;
            }

            var assignedPermissionKeys = await _context.AdminRolePermissions
                .AsNoTracking()
                .Where(link =>
                    link.AdminRoleId == admin.AdminRoleId.Value &&
                    link.AdminPermission.IsActive == true)
                .Select(link => link.AdminPermission.PermissionKey)
                .Distinct()
                .ToListAsync(cancellationToken);

            var expandedAssignedKeys =
                ExpandImpliedViewPermissionKeys(assignedPermissionKeys);

            return keys.Any(expandedAssignedKeys.Contains);
        }

        private static HashSet<string> ExpandImpliedViewPermissionKeys(
            IEnumerable<string> permissionKeys)
        {
            var expanded = new HashSet<string>(
                permissionKeys
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Select(key => key.Trim()),
                StringComparer.OrdinalIgnoreCase);

            AddImpliedViewKey(
                expanded,
                AdminPermissionKeys.Users.View,
                "Users.");

            AddImpliedViewKey(
                expanded,
                AdminPermissionKeys.Places.View,
                "Places.");

            AddImpliedViewKey(
                expanded,
                AdminPermissionKeys.MasterData.View,
                "MasterData.");

            AddImpliedViewKey(
                expanded,
                AdminPermissionKeys.Cases.View,
                "Cases.");

            AddImpliedViewKey(
                expanded,
                AdminPermissionKeys.Finance.View,
                "Finance.");

            AddImpliedViewKey(
                expanded,
                AdminPermissionKeys.Advertisements.View,
                "Advertisements.");

            AddImpliedViewKey(
                expanded,
                AdminPermissionKeys.Settings.View,
                "Settings.");

            AddImpliedViewKey(
                expanded,
                AdminPermissionKeys.Employees.View,
                "Employees.");

            AddImpliedViewKey(
                expanded,
                AdminPermissionKeys.Roles.View,
                "Roles.");

            return expanded;
        }

        private static void AddImpliedViewKey(
            HashSet<string> permissionKeys,
            string viewPermissionKey,
            string modulePrefix)
        {
            if (permissionKeys.Contains(viewPermissionKey))
            {
                return;
            }

            if (permissionKeys.Any(key =>
                    key.StartsWith(
                        modulePrefix,
                        StringComparison.OrdinalIgnoreCase)))
            {
                permissionKeys.Add(viewPermissionKey);
            }
        }

        public async Task<(bool usernameAvailable, bool emailAvailable)> CheckAccountAvailabilityAsync(
            string? username,
            string? email,
            int? excludeUserId,
            CancellationToken cancellationToken = default)
        {
            username = Normalize(username, 50);
            email = Normalize(email, 100);

            var usernameAvailable = true;
            var emailAvailable = true;

            if (!string.IsNullOrWhiteSpace(username))
            {
                usernameAvailable = !await _context.Users
                    .AsNoTracking()
                    .AnyAsync(user =>
                        user.Username == username &&
                        (!excludeUserId.HasValue || user.UserId != excludeUserId.Value),
                        cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                emailAvailable = !await _context.Users
                    .AsNoTracking()
                    .AnyAsync(user =>
                        user.ContactInfo == email &&
                        (!excludeUserId.HasValue || user.UserId != excludeUserId.Value),
                        cancellationToken);
            }

            return (usernameAvailable, emailAvailable);
        }

        private async Task<string?> ValidateEmployeeRequestAsync(
            AdminEmployeeSaveDto request,
            bool isCreate,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) ||
                string.IsNullOrWhiteSpace(request.LastName))
            {
                return "الاسم الأول والأخير مطلوبان.";
            }

            if (string.IsNullOrWhiteSpace(request.Username) || !UsernameRegex.IsMatch(request.Username))
            {
                return "اسم المستخدم يجب أن يكون باللغة الإنجليزية وبدون مسافات، ويمكن استخدام . _ -.";
            }

            if (string.IsNullOrWhiteSpace(request.ContactInfo) ||
                !new EmailAddressAttribute().IsValid(request.ContactInfo))
            {
                return "صيغة البريد الإلكتروني غير صحيحة.";
            }

            if (request.AdminRoleId <= 0)
            {
                return "اختر دورًا وظيفيًا للموظف.";
            }

            if (isCreate &&
                !string.IsNullOrWhiteSpace(request.TemporaryPassword) &&
                !TemporaryPasswordRegex.IsMatch(request.TemporaryPassword))
            {
                return "كلمة المرور المؤقتة يجب أن تكون 8 أحرف على الأقل وتحتوي على حرف إنجليزي ورقم.";
            }

            if (isCreate &&
                !string.Equals(
                    request.TemporaryPassword ?? string.Empty,
                    request.ConfirmTemporaryPassword ?? string.Empty,
                    StringComparison.Ordinal))
            {
                return "تأكيد كلمة المرور غير مطابق.";
            }

            var excludeId = request.UserId;

            if (await _context.Users.AsNoTracking().AnyAsync(user =>
                    user.Username == request.Username &&
                    (!excludeId.HasValue || user.UserId != excludeId.Value),
                    cancellationToken))
            {
                return "اسم المستخدم مستخدم مسبقًا.";
            }

            if (await _context.Users.AsNoTracking().AnyAsync(user =>
                    user.ContactInfo == request.ContactInfo &&
                    (!excludeId.HasValue || user.UserId != excludeId.Value),
                    cancellationToken))
            {
                return "البريد الإلكتروني مستخدم مسبقًا.";
            }

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber) &&
                await _context.Users.AsNoTracking().AnyAsync(user =>
                    user.PhoneNumber == request.PhoneNumber &&
                    (!excludeId.HasValue || user.UserId != excludeId.Value),
                    cancellationToken))
            {
                return "رقم الهاتف مستخدم مسبقًا.";
            }

            return null;
        }

        private async Task<List<AdminRoleOptionDto>> GetRoleOptionsAsync(
            bool includeSystemRole,
            int? selectedRoleId,
            bool includePermissionGroups,
            CancellationToken cancellationToken)
        {
            var roles = await _context.AdminRoles
                .AsNoTracking()
                .Where(role =>
                    (role.IsActive == true || role.AdminRoleId == selectedRoleId) &&
                    (includeSystemRole || !role.IsSystemRole))
                .OrderByDescending(role => role.IsSystemRole)
                .ThenBy(role => role.RoleName)
                .Select(role => new AdminRoleOptionDto
                {
                    AdminRoleId = role.AdminRoleId,
                    RoleName = role.RoleName,
                    Description = role.Description,
                    IsActive = role.IsActive == true,
                    IsSystemRole = role.IsSystemRole,
                    PermissionsCount = role.AdminRolePermissions.Count(link => link.AdminPermission.IsActive == true)
                })
                .ToListAsync(cancellationToken);

            if (!includePermissionGroups || roles.Count == 0)
            {
                return roles;
            }

            var roleIds = roles.Select(role => role.AdminRoleId).ToList();
            var links = await _context.AdminRolePermissions
                .AsNoTracking()
                .Where(link =>
                    roleIds.Contains(link.AdminRoleId) &&
                    link.AdminPermission.IsActive == true)
                .Select(link => new
                {
                    link.AdminRoleId,
                    link.AdminPermission.AdminPermissionId,
                    link.AdminPermission.PermissionKey,
                    link.AdminPermission.PermissionName,
                    link.AdminPermission.PermissionGroup,
                    link.AdminPermission.Description,
                    link.AdminPermission.IsSensitive
                })
                .ToListAsync(cancellationToken);

            foreach (var role in roles)
            {
                role.PermissionGroups = links
                    .Where(link => link.AdminRoleId == role.AdminRoleId)
                    .GroupBy(link => link.PermissionGroup)
                    .Select(group => new AdminPermissionGroupDto
                    {
                        GroupName = group.Key,
                        Permissions = group
                            .Select(link => new AdminPermissionItemDto
                            {
                                AdminPermissionId = link.AdminPermissionId,
                                PermissionKey = link.PermissionKey,
                                PermissionName = link.PermissionName,
                                PermissionGroup = link.PermissionGroup,
                                Description = link.Description,
                                IsSensitive = link.IsSensitive,
                                IsActive = true,
                                IsSelected = true
                            })
                            .ToList()
                    })
                    .ToList();
            }

            return roles;
        }

        private async Task AddAuditLogAsync(
            int userId,
            string actionType,
            string? entityType,
            string? entityId,
            string? description,
            object? oldValues,
            object? newValues,
            string? ipAddress,
            string? userAgent,
            bool isSuccessful,
            string? failureReason,
            CancellationToken cancellationToken)
        {
            _context.AdminAuditLogs.Add(new AdminAuditLog
            {
                UserId = userId > 0 ? userId : null,
                ActionType = actionType,
                EntityType = entityType,
                EntityId = entityId,
                Description = description,
                OldValuesJson = oldValues == null ? null : JsonSerializer.Serialize(oldValues),
                NewValuesJson = newValues == null ? null : JsonSerializer.Serialize(newValues),
                Ipaddress = Normalize(ipAddress, 50),
                UserAgent = Normalize(userAgent, 500),
                OccurredAt = DateTime.Now,
                IsSuccessful = isSuccessful,
                FailureReason = Normalize(failureReason, 500)
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        private static void NormalizeEmployeeRequest(AdminEmployeeSaveDto request)
        {
            request.FirstName = Normalize(request.FirstName, 60) ?? string.Empty;
            request.LastName = Normalize(request.LastName, 60) ?? string.Empty;
            request.Username = Normalize(request.Username, 50) ?? string.Empty;
            request.ContactInfo = Normalize(request.ContactInfo, 100) ?? string.Empty;
            request.PhoneNumber = Normalize(request.PhoneNumber, 50);
            request.EmployeeNumber = Normalize(request.EmployeeNumber, 30);
            request.Department = Normalize(request.Department, 100);
            request.JobTitle = Normalize(request.JobTitle, 100);
            request.TemporaryPassword = string.IsNullOrWhiteSpace(request.TemporaryPassword)
                ? null
                : request.TemporaryPassword;
            request.ConfirmTemporaryPassword = string.IsNullOrWhiteSpace(request.ConfirmTemporaryPassword)
                ? null
                : request.ConfirmTemporaryPassword;
        }

        private static string? Normalize(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var result = value.Trim();
            return result.Length <= maxLength ? result : result[..maxLength];
        }

        private static (string FirstName, string LastName) SplitName(string? fullName)
        {
            var cleaned = Normalize(fullName, 100) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return (string.Empty, string.Empty);
            }

            var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                return (parts[0], "-");
            }

            return (parts[0], string.Join(' ', parts.Skip(1)));
        }

        private async Task<bool> TrySendEmployeeCredentialsEmailAsync(
            string? toEmail,
            string fullName,
            string username,
            string temporaryPassword,
            string roleName,
            bool isPasswordReset)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                return false;
            }

            try
            {
                /*
                 * لا نكرر إعدادات SMTP هنا.
                 * نستخدم الدالة التي أضيفت في EmailHelper في دفعة تسجيل دخول الموظفين.
                 * Reflection يجعل هذه الخدمة متوافقة حتى لو كان المشروع ما زال يحمل
                 * نسخة أقدم من EmailHelper؛ في هذه الحالة يفشل الإرسال فقط ولا يفشل إنشاء الحساب.
                 */
                var method =
                    typeof(EmailHelper)
                        .GetMethods(
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.Static)
                        .FirstOrDefault(item =>
                            item.Name == "SendEmployeeCredentialsEmailAsync" &&
                            item.GetParameters().Length == 6);

                if (method == null)
                {
                    /*
                     * توافق خلفي: إذا كانت نسخة EmailHelper قديمة ولا تحتوي قالب الموظف،
                     * نستخدم مسار البريد الموجود أصلًا بدل أن نفقد الإرسال بالكامل.
                     * النسخة الأحدث من EmailHelper ستستخدم القالب الكامل تلقائيًا.
                     */
                    await EmailHelper.SendOtpEmailAsync(
                        toEmail,
                        temporaryPassword,
                        isPasswordReset: true);

                    _logger.LogWarning(
                        "تم إرسال كلمة المرور المؤقتة بقالب البريد الاحتياطي لأن قالب الموظفين غير موجود في EmailHelper.");

                    return true;
                }

                var invocation =
                    method.Invoke(
                        null,
                        new object?[]
                        {
                            toEmail,
                            fullName,
                            username,
                            temporaryPassword,
                            roleName,
                            isPasswordReset
                        });

                if (invocation is Task task)
                {
                    await task;
                    return true;
                }

                return false;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "تعذر إرسال بيانات دخول الموظف إلى البريد {Email}.",
                    toEmail);

                return false;
            }
        }

        private static string GenerateTemporaryPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string digits = "23456789";
            const string symbols = "!@#$%*-_";
            var all = upper + lower + digits + symbols;

            var chars = new List<char>
            {
                upper[RandomNumberGenerator.GetInt32(upper.Length)],
                lower[RandomNumberGenerator.GetInt32(lower.Length)],
                digits[RandomNumberGenerator.GetInt32(digits.Length)],
                symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
            };

            while (chars.Count < 10)
            {
                chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
            }

            for (var i = chars.Count - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars.ToArray());
        }

        private static string NormalizeTab(string? tab)
        {
            return (tab ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "roles" => "roles",
                "audit" => "audit",
                _ => "employees"
            };
        }
    }
}
