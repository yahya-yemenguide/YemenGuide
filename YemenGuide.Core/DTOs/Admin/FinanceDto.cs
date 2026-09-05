using YemenGuide.Core.Entities;
using System.Collections.Generic;

namespace YemenGuide.Core.DTOs.Admin
{
    public class FinanceDto
    {
        // التبويب الأول: العمولات المستحقة (IsPaymentConfirmed = 0)
        public IEnumerable<FinancialRecord> DueCommissions { get; set; } = new List<FinancialRecord>();

        // التبويب الثاني: سجل العمولات المسددة (IsPaymentConfirmed = 1)
        public IEnumerable<FinancialRecord> PaidCommissions { get; set; } = new List<FinancialRecord>();

        // أرقام التقارير المالية
        public decimal TotalRevenue { get; set; } // إجمالي أرباح المنصة المكتملة
        public decimal ExpectedRevenue { get; set; } // أرباح المنصة قيد الانتظار
        public int TotalTransactions { get; set; } // عدد العمليات المؤكدة
    }
}
