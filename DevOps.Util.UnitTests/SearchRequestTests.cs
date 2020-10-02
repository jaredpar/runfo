using DevOps.Util.DotNet.Triage;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public class SearchRequestTests : StandardTestBase
    {
        [Fact]
        public async Task BuildResultSearches()
        {
            var def = AddBuildDefinition("dnceng|public|roslyn|42");
            AddBuild("1|dotnet|roslyn||Failed", def);
            AddBuild("2|dotnet|roslyn||Failed", def);
            AddBuild("3|dotnet|roslyn||Succeeded", def);
            AddBuild("4|dotnet|roslyn||Canceled", def);
            await Context.SaveChangesAsync();

            await Test(2, "result:failed");
            await Test(1, "result:succeeded");
            await Test(1, "result:canceled");

            async Task Test(int count, string value)
            {
                var request = new SearchBuildsRequest();
                request.ParseQueryString(value);
                var query = request.Filter(Context.ModelBuilds);
                var queryCount = await query.CountAsync();
                Assert.Equal(count, queryCount);
            }
        }
    }
}
