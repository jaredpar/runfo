using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevOps.Status.Util
{
    /// <summary>
    /// Display a pagination stripe for a given page. Couple of notes:
    ///     1. The page must accept a route parameter named "pageNumber"
    ///     2. Page counts start at zero.
    /// </summary>
    public sealed class PaginationDisplay
    {
        public int? TotalPageCount { get; set; }
        public int PageNumber { get; set; }
        public int? NextPageNumber { get; set; }
        public int? PreviousPageNumber { get; set; }

        /// <summary>
        /// The route to request in the pagination 
        /// </summary>
        public string Route { get;  }

        /// <summary>
        /// The collection of route data to include with the request
        /// </summary>
        public Dictionary<string, string> RouteData { get; }

        public PaginationDisplay(string route, Dictionary<string, string> routeData, int pageNumber)
        {
            Route = route;
            RouteData = routeData;
            PageNumber = pageNumber;
            if (pageNumber > 0)
            {
                PreviousPageNumber = pageNumber - 1;
            }

            NextPageNumber = pageNumber + 1;
        }

        public PaginationDisplay(string route, Dictionary<string, string> routeData, int pageNumber, int totalPages)
        {
            Route = route;
            RouteData = routeData;
            PageNumber = pageNumber;
            TotalPageCount = totalPages;
            if (pageNumber > 0)
            {
                PreviousPageNumber = pageNumber - 1;
            }

            if (pageNumber < totalPages)
            {
                NextPageNumber = pageNumber + 1;
            }
        }
    }
}
