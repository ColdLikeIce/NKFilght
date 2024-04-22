using CommonCore.EntityFramework.Common;
using NkFlightWeb.Db;
using NkFlightWeb.Entity;
using NkFlightWeb.Impl;

namespace NkFlightWeb.Service
{
    public class CacheDomain : ICacheDomain
    {
        private readonly IBaseRepository<HeyTripDbContext> _repository;

        public CacheDomain(IBaseRepository<HeyTripDbContext> repository)
        {
            _repository = repository;
        }

        public async Task<string> GetValueParameter(string key)
        {
            return "";
        }
    }
}