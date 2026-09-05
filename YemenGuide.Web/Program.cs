// =============================================================
//  Program.cs - نقطة دخول التطبيق وتهيئة الخدمات
// =============================================================

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization; // أضفنا هذا لتعدد اللغات
using System.Globalization; // أضفنا هذا لتعريف الثقافات (ar, en)
using YemenGuide.Web.Hubs; // ✨ أضفنا مسار الهاب (برج المراقبة) الخاص بالدردشة
using Microsoft.AspNetCore.Authentication.Google; // ✨ أضفنا مكتبة مصادقة جوجل
using Microsoft.AspNetCore.HttpOverrides; // ✨ أضفنا هذه المكتبة لحل مشكلة الاستضافة (MonsterASP)
using YemenGuide.Web.Services.Finance;
using YemenGuide.Web.Services.OwnerDashboard;
using YemenGuide.Web.Services.Employees;
using YemenGuide.Web.Services.Advertisements;
using YemenGuide.Web.Services.Notifications;
using YemenGuide.Web.Services.Reports;
using YemenGuide.Web.Services.ExchangeRates;
using YemenGuide.Web.Maintenance;
using YemenGuide.Core.Helpers;


// =============================================================
//  1. بناء التطبيق (Web Application Builder)
// =============================================================

var builder = WebApplication.CreateBuilder(args);


// =============================================================
//  1.1. استدعاء قاعدة البيانات
// =============================================================

builder.Services.AddDbContext<YemenGuide.Data.AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


// =============================================================
//  1.2. إضافة خدمات تعدد اللغات (Localization Services)
// =============================================================

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Add services to the container (تم دمج تفعيل الترجمة للواجهات هنا)
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// ذاكرة مؤقتة مطلوبة للـ HomeController والخدمات التي تستخدم IMemoryCache
builder.Services.AddMemoryCache();


// =============================================================
//  1.3. ضبط اللغات المدعومة في النظام وخوارزمية الاختيار
// =============================================================

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("ar"),
        new CultureInfo("en")
    };

    options.DefaultRequestCulture = new RequestCulture("ar"); // اللغة الافتراضية
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // جعل الأولوية الأولى للكوكيز لمعرفة لغة المستخدم الحالية
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});


// =============================================================
//  1.4. إعداد نظام تسجيل الدخول (الكوكيز + مصادقة جوجل)
// =============================================================

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

if (string.IsNullOrWhiteSpace(googleClientId) || string.IsNullOrWhiteSpace(googleClientSecret))
{
    throw new InvalidOperationException(
        "Google authentication configuration is missing. Configure Authentication:Google:ClientId and ClientSecret.");
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "YemenGuide.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
    })
    .AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;

        // URI ثابت وواضح على الكمبيوتر والهاتف والاستضافة.
        // يجب تسجيل https://YOUR-DOMAIN/signin-google في Google Cloud Console.
        options.CallbackPath = "/signin-google";
        options.SaveTokens = false;

        // OAuth correlation cookie يجب أن يسمح بالرجوع من accounts.google.com.
        options.CorrelationCookie.HttpOnly = true;
        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    });



// =============================================================
//  1.4.1 إعداد البريد SMTP (OTP + بيانات موظفي الإدارة)
// =============================================================
// IConfiguration يقرأ appsettings.json ثم appsettings.Development.json
// ثم Environment Variables؛ لذلك نفس الكود يعمل محلياً وعلى الاستضافة.
EmailHelper.Configure(
    builder.Configuration["EmailSettings:FromEmail"],
    builder.Configuration["EmailSettings:AppPassword"],
    builder.Configuration["EmailSettings:BaseUrl"]);

// =============================================================
//  1.5. تسجيل الخدمات الإضافية ومهام الخلفية (Background Services)
// =============================================================

// تسجيل Resolver سوق الصرف للمدن (صنعاء / عدن)
builder.Services.AddSingleton<ExchangeRateRegionResolver>();

// تسجيل مزود YemenSul عبر HttpClient
builder.Services.AddHttpClient<IExchangeRateProvider, YemenSulExchangeRateProvider>();

// تسجيل طبقة التنسيق المركزية لأسعار الصرف
builder.Services.AddScoped<ExchangeRateService>();

// تسجيل الخدمة الخلفية المستضافة لتحديث الأسعار تلقائياً
builder.Services.AddHostedService<ExchangeRateBackgroundService>();

// تسجيل خدمة التحويل لتكون متاحة في جميع الواجهات
builder.Services.AddScoped<YemenGuide.Web.Services.CurrencyService>();

// تسجيل خدمة عرض العملة المستخدمة في Home/Places/Bookings وغيرها
builder.Services.AddScoped<CurrencyDisplayService>();

builder.Services.AddScoped<
    ICommissionFinanceService,
    CommissionFinanceService>();
// تسجيل خدمة مركز قيادة المالك
builder.Services.AddScoped<
    IOwnerDashboardService,
    OwnerDashboardService>();
builder.Services.AddScoped<
    IOwnerFinancialService,
    OwnerFinancialService>();
builder.Services.AddScoped<
    IAdminFinancialService,
    AdminFinancialService>();

builder.Services.AddSingleton<
    AdminFinancialExcelBuilder>();

// تسجيل خدمة إدارة البلاغات والقضايا في لوحة الأدمن
builder.Services.AddScoped<
    IAdminReportsService,
    AdminReportsService>();

// تسجيل خدمة الشكاوى والبلاغات الخاصة بالعميل
builder.Services.AddScoped<
    ICustomerReportsService,
    CustomerReportsService>();

// تسجيل خدمة البلاغات والقضايا الخاصة بالمالك
builder.Services.AddScoped<
    IOwnerReportsService,
    OwnerReportsService>();

// تسجيل محرك صلاحيات موظفي الإدارة وسجل العمليات
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<
    IAdminPermissionService,
    AdminPermissionService>();
builder.Services.AddScoped<
    IAdminAuditService,
    AdminAuditService>();
builder.Services.AddScoped<
    IAdminEmployeeService,
    AdminEmployeeService>();

// تسجيل خدمة التسعير المشتركة للإعلانات
builder.Services.AddScoped<
    IAdvertisementPricingService,
    AdvertisementPricingService>();

// تسجيل خدمة إدارة الإعلانات في لوحة الأدمن
builder.Services.AddScoped<
    IAdminAdvertisementService,
    AdminAdvertisementService>();

// تسجيل خدمة إدارة الإعلانات في لوحة المالك
builder.Services.AddScoped<
    IOwnerAdvertisementService,
    OwnerAdvertisementService>();

// تسجيل محرك عرض وتتبع الإعلانات في واجهات العميل
builder.Services.AddScoped<
    ICustomerAdvertisementService,
    CustomerAdvertisementService>();

// تسجيل محرك الإشعارات المركزي (عميل + مالك + أدمن)
builder.Services.AddScoped<
    INotificationService,
    NotificationService>();

// تسجيل محرك الصيانة المركزي
builder.Services.AddYemenGuideMaintenance();

// =============================================================
//  1.6. تفعيل خدمة الاتصال اللحظي (SignalR)
// =============================================================

builder.Services.AddSignalR(); // ✨ السطر الثاني: تفعيل المحرك اللحظي في حاوية الخدمات


// =============================================================
//  2. بناء التطبيق (Build the Application)
// =============================================================

var app = builder.Build();

// ✨ الحل السحري الشامل لاستضافة MonsterASP (Runasp) لإجبار التوجيه الآمن (HTTPS)
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// تفريغ الشبكات المعروفة لكي يثق التطبيق في بروكسي الاستضافة
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);


// =============================================================
//  3. تكوين خط أنابيب HTTP (HTTP Request Pipeline)
// =============================================================

// =============================================================
//  3.1. معالجة الأخطاء (Exception Handling)
// =============================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}


// =============================================================
//  3.2. إعادة التوجيه HTTPS والملفات الثابتة
// =============================================================

// ✨ تم تهميش هذا السطر لمنع التصادم مع إعدادات الاستضافة المجانية
// app.UseHttpsRedirection();

app.UseStaticFiles();


// =============================================================
//  3.3. التوجيه (Routing)
// =============================================================

app.UseRouting();


// =============================================================
//  3.4. تفعيل وسيط التعريب (Localization Middleware)
// =============================================================

var localizationOptions = app.Services.GetService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);


// =============================================================
//  3.5. تفعيل وسيط المصادقة والتفويض (Authentication & Authorization)
// =============================================================

app.UseAuthentication();

// محرك الصيانة تم تسجيله في الخدمات أعلاه.
// سنفعّل السطر التالي بعد إنشاء MaintenanceController
// وصفحتي MaintenancePage و RestrictedService حتى لا يحدث تحويل لمسار غير موجود.
// app.UseYemenGuideMaintenance();

app.UseAuthorization();


// =============================================================
//  3.6. تكوين التوجيه (Routing Configuration)
// =============================================================

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/chatHub"); // ✨ السطر الثالث: فتح قناة الاتصال للواجهات الأمامية لتتحدث مع برج المراقبة


// =============================================================
//  4. تشغيل التطبيق (Run the Application)
// =============================================================

// ✨ هنا يتم استدعاء الزرع التلقائي للأدمن قبل تشغيل السيرفر
try
{
    YemenGuide.Data.Seeding.DatabaseSeeder.SeedAdminUser(app.Services);
}
catch (Exception ex)
{
    Console.WriteLine($"حدث خطأ أثناء تهيئة الإدارة والصلاحيات: {ex.Message}");
}

app.Run();
