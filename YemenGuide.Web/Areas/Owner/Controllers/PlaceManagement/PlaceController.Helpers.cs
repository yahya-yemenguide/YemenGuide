using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.IO;
using YemenGuide.Data;
using YemenGuide.Core.Entities;
using YemenGuide.Web.Filters;
using YemenGuide.Core.DTOs.Owner;
using BCrypt.Net;

using Microsoft.AspNetCore.Hosting;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    public partial class PlaceController
    {

        public class OwnerRejectDto
        {
            public int Id { get; set; }
            public string Reason { get; set; }
        }


        private string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;
            return BCrypt.Net.BCrypt.HashPassword(password, 11);
        }




        // 🌟 دالة مساعدة لرفع صور الغرف من الاستبيان
        private async Task<string> ProcessRoomImagesFromWizardAsync(IReadOnlyList<IFormFile> files)
        {
            if (files == null || files.Count == 0) return null;

            // حماية السيرفر
            long totalSize = files.Sum(f => f.Length);
            if (totalSize > 5 * 1024 * 1024) return null;

            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "rooms");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            List<string> savedPaths = new List<string>();
            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var stream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(stream); }
                    savedPaths.Add("/uploads/rooms/" + uniqueFileName);
                }
            }
            return string.Join(",", savedPaths);
        }

        /// <summary>
        /// يحدد المنشأة النشطة للمالك من نفس الكوكي الذي يستخدمه هيدر المالك.
        /// عند غياب الكوكي أو عدم صلاحيته، يعتمد أحدث منشأة صالحة للمالك.
        /// </summary>
        private Place? ResolveActiveOwnerPlace(int ownerId)
        {
            if (ownerId <= 0)
                return null;

            string cookieName = $"ownerActivePlace_{ownerId}";
            int.TryParse(Request.Cookies[cookieName], out int requestedPlaceId);

            IQueryable<Place> placesQuery = _context.Places
                .Include(place => place.Category)
                .AsNoTracking()
                .Where(place =>
                    place.OwnerId == ownerId &&
                    place.Status != "WaitOwner" &&
                    place.Status != "RejectedByOwner");

            if (requestedPlaceId > 0)
            {
                Place? requestedPlace = placesQuery
                    .FirstOrDefault(place => place.PlaceId == requestedPlaceId);

                if (requestedPlace != null)
                    return requestedPlace;
            }

            return placesQuery
                .OrderByDescending(place => place.PlaceId)
                .FirstOrDefault();
        }

    }
}
