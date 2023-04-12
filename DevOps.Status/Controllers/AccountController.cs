using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace DevOps.Status.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet("signin")]
        public IActionResult UserSignIn(string? returnUrl = null) =>
            Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = "/" + returnUrl
                },
                GitHubAuthenticationDefaults.AuthenticationScheme
            );

        [HttpGet("signout")]
        [HttpPost("signout")]
        public IActionResult UserSignOut() =>
            SignOut(
                new AuthenticationProperties
                {
                    RedirectUri = "/"
                },
                CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
