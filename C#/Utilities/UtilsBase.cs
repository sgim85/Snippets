using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WkHtmlToPdfDotNet;

namespace CXP.Utilities
{
    /// <summary>
    /// The UtilsBase.cs file contains the Utils contstructor and this is where to inject DI instances you may need for certain Utils functions
    /// </summary>
    public partial class Utils
    {
        private static IHttpContextAccessor? _httpContextAccessor;
        private readonly SynchronizedConverter _synchConverter;
        private readonly ILogger<Utils> _logger;
        public Utils(IHttpContextAccessor httpContextAccessor, SynchronizedConverter synchConverter, ILogger<Utils> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _synchConverter = synchConverter;
            _logger = logger;
        }
    }
}
