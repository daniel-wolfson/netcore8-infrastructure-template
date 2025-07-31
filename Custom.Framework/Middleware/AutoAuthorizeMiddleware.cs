using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Custom.Framework.Middleware
{
    public class AutoAuthorizeMiddleware(RequestDelegate rd)
    {
        public const string IDENTITY_ID = "abcd2024-abcd-2024-abcd-2024abcd2024";
        private readonly RequestDelegate _next = rd;

        public async Task Invoke(HttpContext httpContext)
        {
            var identity = new ClaimsIdentity("cookies");

            identity.AddClaim(new Claim("sub", IDENTITY_ID));
            identity.AddClaim(new Claim("unique_name", IDENTITY_ID));
            identity.AddClaim(new Claim(ClaimTypes.Name, IDENTITY_ID));

            httpContext.User.AddIdentity(identity);

            await _next.Invoke(httpContext);
        }
    }
}
