using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AspNet.Security.OAuth.GitHub;
using AspNet.Security.OAuth.VisualStudio;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace DevOps.Status.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet("signin")]
        public IActionResult SignIn(string? returnUrl = null)
        {
            return Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = "/" + returnUrl
                },
                GitHubAuthenticationDefaults.AuthenticationScheme
            );
        }

        [HttpGet("signout")]
        [HttpPost("signout")]
        public IActionResult SignOut()
        {
            return SignOut(
                new AuthenticationProperties
                {
                    RedirectUri = "/"
                },
                GitHubAuthenticationDefaults.AuthenticationScheme,
                VisualStudioAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
