using NkFlightWeb.Impl;
using NkFlightWeb.Service;
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
                    var s = 1;
                    var token = InitConfig.Get_Token();
                    if (token == null || token.PassTime < DateTime.Now.AddMinutes(3))
                    {
                        s = 1;
                    }
                    else
                    {
                        var sleepTime = token.PassTime.Value - DateTime.Now.AddMinutes(3);
                        s = Convert.ToInt32(sleepTime.TotalSeconds);
                    }
                    Log.Information($"获取token{stopwatch.ElapsedMilliseconds}ms准备休眠 {s} s");

                    await Task.Delay(1 * s * 1000, stoppingToken);
                }
                catch (Exception ex)
                {
                    Log.Error($"EarlyWarningWorker运行出错{ex.Message}");
                    continue;
                }
            }
        }
    }
}