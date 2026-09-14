using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System;

namespace YemenGuide.Web.Infrastructure
{
    [HtmlTargetElement("body")]
    public class ProfileDisplaySettingsTagHelper : TagHelper
    {
        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; } = null!;

        public override void Process(
            TagHelperContext context,
            TagHelperOutput output)
        {
            var path =
                ViewContext.HttpContext.Request.Path.Value ??
                string.Empty;

            bool isOwnerProfile =
                path.Equals(
                    "/Owner/Profile/OwnerProfile",
                    StringComparison.OrdinalIgnoreCase);

            bool isCustomerProfile =
                path.Equals(
                    "/Profile/CustomerProfile",
                    StringComparison.OrdinalIgnoreCase);

            if (!isOwnerProfile && !isCustomerProfile)
            {
                return;
            }

            output.PostContent.AppendHtml(
                "<script src=\"/js/shared/profile-display-settings.js?v=20260914\"></script>");
        }
    }
}
