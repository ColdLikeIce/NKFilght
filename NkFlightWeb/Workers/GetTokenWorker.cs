using NkFlightWeb.Impl;
using Serilog;

namespace NkFlightWeb.Workers
{
    public class GetTokenWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 获取token
        /// </summary>
        /// <param name="serviceProvider"></param>
        public GetTokenWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateAsyncScope();
                var _domain = scope.ServiceProvider.GetRequiredService<INkFlightDomain>();
                try
                {
                    await _domain.GetToken();
                }
                catch (Exception ex)
                {
                    Log.Error($"EarlyWarningWorker运行出错{ex.Message}");
                }
                await Task.Delay(1 * 60 * 1000, stoppingToken);
            }
        }
    }
}