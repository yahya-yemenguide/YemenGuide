using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace YemenGuide.Core.DTOs.Admin
{
    public static class AdminEmployeeStatuses
    {
        public const string Active = "Active";
        public const string Suspended = "Suspended";
        public const string OnLeave = "OnLeave";
        public const string Terminated = "Terminated";

        public static readonly string[] All =
        {
            Active,
            Suspended,
            OnLeave,
            Terminated
        };

        public static string ArabicLabel(string? status) => status switch
        {
            Active => "نشط",
            Suspended => "موقوف",
            OnLeave => "في إجازة",
            Terminated => "منتهي الخدمة",
            _ => "غير محدد"
        };
    }

    public class AdminEmployeeManagementPageDto
    {
        public int EmployeesCount { get; set; }
        public int RolesCount { get; set; }
        public int AuditLogsCount { get; set; }
        public int ActiveEmployeesCount { get; set; }
        public int SuspendedEmployeesCount { get; set; }

        public bool CanViewEmployees { get; set; }
        public bool CanCreateEmployees { get; set; }
        public bool CanEditEmployees { get; set; }
        public bool CanChangeEmployeeState { get; set; }
        public bool CanResetPasswords { get; set; }
        public bool CanViewRoles { get; set; }
        public bool CanManageRoles { get; set; }
        public bool CanViewAuditLogs { get; set; }

        public string DefaultTab { get; set; } = "employees";
    }

    public class AdminEmployeeFilterDto
    {
        [MaxLength(120)]
        public string? Search { get; set; }

        public int? AdminRoleId { get; set; }

        [MaxLength(100)]
        public string? Department { get; set; }

        [MaxLength(20)]
        public string? EmploymentStatus { get; set; }

        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(5, 100)]
        public int PageSize { get; set; } = 12;
    }

    public class AdminEmployeesTabDto
    {
        public List<AdminEmployeeListItemDto> Employees { get; set; } = new();
        public List<AdminRoleOptionDto> Roles { get; set; } = new();
        public List<string> Departments { get; set; } = new();
        public AdminEmployeeFilterDto Filter { get; set; } = new();
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int SuspendedCount { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 12;

        public int TotalPages => TotalCount <= 0
            ? 0
            : (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize));

        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanChangeState { get; set; }
        public bool CanResetPassword { get; set; }
    }

    public class AdminEmployeeListItemDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? ContactInfo { get; set; }
        public string? PhoneNumber { get; set; }
        public string? EmployeeNumber { get; set; }
        public string? Department { get; set; }
        public string? JobTitle { get; set; }
        public string EmploymentStatus { get; set; } = string.Empty;
        public DateTime? HireDate { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool MustChangePassword { get; set; }
        public int? AdminRoleId { get; set; }
        public string? AdminRoleName { get; set; }
        public bool IsSystemAdmin { get; set; }
        public bool IsRoleActive { get; set; }

        public string EmploymentStatusLabel =>
            AdminEmployeeStatuses.ArabicLabel(EmploymentStatus);
    }

    public class AdminRoleOptionDto
    {
        public int AdminRoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystemRole { get; set; }
        public int PermissionsCount { get; set; }
        public List<AdminPermissionGroupDto> PermissionGroups { get; set; } = new();
    }

    public class AdminEmployeeFormDto
    {
        public bool IsEdit { get; set; }
        public bool IsProtectedSystemAdmin { get; set; }
        public AdminEmployeeSaveDto Employee { get; set; } = new();
        public List<AdminRoleOptionDto> Roles { get; set; } = new();
    }

    public class AdminEmployeeSaveDto
    {
        public int? UserId { get; set; }

        [Required(ErrorMessage = "الاسم الأول مطلوب.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "الاسم الأول يجب أن يكون من حرفين إلى 60 حرفًا.")]
        [RegularExpression(@"^[\p{L}\p{M}][\p{L}\p{M}\s'’-]*$", ErrorMessage = "الاسم الأول يجب أن يحتوي على حروف فقط.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم الأخير مطلوب.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "الاسم الأخير يجب أن يكون من حرفين إلى 60 حرفًا.")]
        [RegularExpression(@"^[\p{L}\p{M}][\p{L}\p{M}\s'’-]*$", ErrorMessage = "الاسم الأخير يجب أن يحتوي على حروف فقط.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم المستخدم مطلوب.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "اسم المستخدم يجب أن يكون من 3 إلى 50 حرفًا.")]
        [RegularExpression(@"^[a-zA-Z0-9_.-]{3,50}$", ErrorMessage = "اسم المستخدم بالإنجليزية فقط، ويسمح بالأرقام والنقطة والشرطة والشرطة السفلية بدون مسافات.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب.")]
        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة.")]
        [StringLength(100, ErrorMessage = "البريد الإلكتروني طويل جدًا.")]
        public string ContactInfo { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف مطلوب.")]
        [StringLength(20, ErrorMessage = "رقم الهاتف طويل جدًا.")]
        [RegularExpression(@"^\+[1-9]\d{7,14}$", ErrorMessage = "رقم الهاتف غير صحيح. اختر الدولة ثم أدخل رقمًا صالحًا.")]
        public string? PhoneNumber { get; set; }

        // هذه الحقول تُدار من النظام داخليًا.
        // EmployeeNumber يُولّد بعد إنشاء الحساب، وJobTitle يُشتق من الدور.
        [StringLength(30)]
        public string? EmployeeNumber { get; set; }

        [StringLength(100)]
        public string? Department { get; set; }

        [StringLength(100)]
        public string? JobTitle { get; set; }

        public DateTime? HireDate { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "اختر دورًا وظيفيًا للموظف.")]
        public int AdminRoleId { get; set; }

        [StringLength(100, MinimumLength = 8, ErrorMessage = "كلمة المرور المؤقتة لا تقل عن 8 أحرف.")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$", ErrorMessage = "كلمة المرور المؤقتة يجب أن تحتوي على حرف إنجليزي ورقم على الأقل.")]
        public string? TemporaryPassword { get; set; }

        [Compare(nameof(TemporaryPassword), ErrorMessage = "تأكيد كلمة المرور غير مطابق.")]
        public string? ConfirmTemporaryPassword { get; set; }
    }

    public class AdminEmployeeStatusDto
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public string EmploymentStatus { get; set; } = AdminEmployeeStatuses.Active;
    }

    public class AdminPasswordResetDto
    {
        [Required]
        public int UserId { get; set; }
    }

    public class AdminOperationResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public string? TemporaryPassword { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public bool? EmailSent { get; set; }

        public static AdminOperationResultDto Ok(
            string message,
            int? entityId = null,
            string? temporaryPassword = null,
            string? username = null,
            string? email = null,
            bool? emailSent = null) =>
            new()
            {
                Success = true,
                Message = message,
                EntityId = entityId,
                TemporaryPassword = temporaryPassword,
                Username = username,
                Email = email,
                EmailSent = emailSent
            };

        public static AdminOperationResultDto Fail(string message) =>
            new()
            {
                Success = false,
                Message = message
            };
    }

    public class AdminRolesTabDto
    {
        public List<AdminRoleListItemDto> Roles { get; set; } = new();
        public string? Search { get; set; }
        public int ActiveRolesCount { get; set; }
        public int TotalPermissionsCount { get; set; }
        public bool CanManage { get; set; }
    }

    public class AdminRoleListItemDto
    {
        public int AdminRoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystemRole { get; set; }
        public int EmployeesCount { get; set; }
        public int PermissionsCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> PermissionGroupNames { get; set; } = new();
    }

    public class AdminRoleFormDto
    {
        public bool IsEdit { get; set; }
        public bool IsSystemRole { get; set; }
        public AdminRoleSaveDto Role { get; set; } = new();
        public List<AdminPermissionGroupDto> PermissionGroups { get; set; } = new();

        public int SelectedPermissionsCount =>
            PermissionGroups.Sum(group =>
                group.Permissions.Count(permission => permission.IsSelected));
    }

    public class AdminRoleSaveDto
    {
        public int? AdminRoleId { get; set; }

        [Required(ErrorMessage = "اسم الدور مطلوب.")]
        [StringLength(100, MinimumLength = 2)]
        public string RoleName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public List<int> PermissionIds { get; set; } = new();
    }

    public class AdminRoleStatusDto
    {
        public int AdminRoleId { get; set; }
        public bool IsActive { get; set; }
    }

    public class AdminPermissionGroupDto
    {
        public string GroupName { get; set; } = string.Empty;
        public List<AdminPermissionItemDto> Permissions { get; set; } = new();

        public int SelectedCount => Permissions.Count(permission => permission.IsSelected);
        public bool AllSelected => Permissions.Count > 0 && Permissions.All(permission => permission.IsSelected);
    }

    public class AdminPermissionItemDto
    {
        public int AdminPermissionId { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
        public string PermissionName { get; set; } = string.Empty;
        public string PermissionGroup { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsSensitive { get; set; }
        public bool IsActive { get; set; }
        public bool IsSelected { get; set; }
        public bool IsRequired { get; set; }
    }

    public class AdminAuditLogFilterDto
    {
        [MaxLength(120)]
        public string? Search { get; set; }

        [MaxLength(100)]
        public string? ActionType { get; set; }

        public bool? IsSuccessful { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(5, 100)]
        public int PageSize { get; set; } = 15;
    }

    public class AdminAuditLogsTabDto
    {
        public List<AdminAuditLogItemDto> Items { get; set; } = new();
        public List<string> ActionTypes { get; set; } = new();
        public AdminAuditLogFilterDto Filter { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 15;

        public int TotalPages => TotalCount <= 0
            ? 0
            : (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize));
    }

    public class AdminAuditLogItemDto
    {
        public long AdminAuditLogId { get; set; }
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Username { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? Description { get; set; }
        public string? IpAddress { get; set; }
        public DateTime OccurredAt { get; set; }
        public bool IsSuccessful { get; set; }
        public string? FailureReason { get; set; }
    }
}
