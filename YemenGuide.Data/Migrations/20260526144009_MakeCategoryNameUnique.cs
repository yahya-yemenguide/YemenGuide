using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YemenGuide.Data.Migrations
{
    public partial class MakeCategoryNameUnique : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // هذا هو السطر الوحيد الذي نحتاجه: إضافة فهرس يمنع تكرار اسم الفئة
            migrationBuilder.CreateIndex(
                name: "IX_Categories_CategoryName",
                table: "Categories",
                column: "CategoryName",
                unique: true);
        }


        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // هذا الكود للتراجع عن العملية إذا احتجنا لذلك مستقبلاً
            migrationBuilder.DropIndex(
                name: "IX_Categories_CategoryName",
                table: "Categories");
        }
    }
}
