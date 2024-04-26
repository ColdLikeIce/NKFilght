using NkFlightWeb.Impl;
using Serilog;
using System.Diagnostics;

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
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    stopwatch.Start();
                    var result = await _domain.GetToken();
                    stopwatch.Stop();
                    Log.Information($"获取token{stopwatch.ElapsedMilliseconds}ms");
                    if (!result)
                    {
                        await Task.Delay(1 * 5 * 1000, stoppingToken);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"EarlyWarningWorker运行出错{ex.Message}");
                    await Task.Delay(1 * 5 * 1000, stoppingToken);
                    continue;
                }

                await Task.Delay(1 * 10 * 1000, stoppingToken);
            }
        }
    }
}