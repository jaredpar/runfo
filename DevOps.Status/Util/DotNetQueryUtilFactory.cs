using AspNet.Security.OAuth.VisualStudio;
using DevOps.Util;
using DevOps.Util.DotNet;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevOps.Status.Util
{
    public sealed class DotNetQueryUtilFactory
    {
        public BlobStorageUtil BlobStorageUtil { get; }
        public IHttpContextAccessor HttpContextAccessor { get; }

        public DotNetQueryUtilFactory(IHttpContextAccessor httpContextAccessor, BlobStorageUtil blobStorageUtil)
        {
            BlobStorageUtil = BlobStorageUtil;
            HttpContextAccessor = httpContextAccessor;
        }

        public async Task<DevOpsServer> CreateDevOpsServerForUserAsync()
        {
            var accessToken = await HttpContextAccessor.HttpContext.GetTokenAsync(VisualStudioAuthenticationDefaults.AuthenticationScheme, "access_token");
            var token = new AuthorizationToken(AuthorizationKind.BearerToken, accessToken);
            return new DevOpsServer(DotNetUtil.AzureOrganization, token);
        }

        public async Task<DotNetQueryUtil> CreateDotNetQueryUtilForUserAsync()
        {
            var server = await CreateDevOpsServerForUserAsync();
            // TODO: use caching one
            var azureUtil = new AzureUtil(server);
            return new DotNetQueryUtil(server, azureUtil);
        }
    }
}
