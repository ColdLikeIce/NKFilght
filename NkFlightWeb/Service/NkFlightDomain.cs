using CommonCore.EntityFramework.Common;
using CommonCore.Mapper;
using CommonCore.Security;
using CommonCore.Util;
using DtaAccess.Domain.Enums;
using DtaAccess.Domain.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NkFlightWeb.Config;
using NkFlightWeb.Db;
using NkFlightWeb.Entity;
using NkFlightWeb.Impl;
using NkFlightWeb.Service.Dto;
using NkFlightWeb.Util;
using OpenCvSharp;
using OpenCvSharp.Detail;
using Qunar.Airtickets.Supplier.Concat.Dtos.Enums;
using Qunar.Airtickets.Supplier.Concat.Dtos.Input;
using Qunar.Airtickets.Supplier.Concat.Dtos.Models;
using Qunar.Airtickets.Supplier.Concat.Dtos.Output;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Point = OpenCvSharp.Point;

namespace NkFlightWeb.Service
{
    public class NkFlightDomain : INkFlightDomain
    {
        private readonly IBaseRepository<HeyTripDbContext> _repository;
        private readonly string apiUrl = "https://www.spirit.com/api/prod-availability/api/availability/search";

        private readonly string detailurl = $"https://www.spirit.com/book/flights?tripType=oneWay&bookingType=flight&from=SJC&departDate={DateTime.Now.AddDays(3).ToString("yyyy-MM-dd")}&to=BQN&returnDate=&adt=1&chd=0&inf=0";
        private readonly string url = $"https://www.spirit.com";
        private readonly IConfiguration _configuration;
        private readonly AppSetting _setting;
        private readonly IMapper _mapper;
        private readonly IServiceProvider _serviceProvider;

        public NkFlightDomain(IConfiguration configuration, IOptions<AppSetting> options,
            IBaseRepository<HeyTripDbContext> repository, IMapper mapper, IServiceProvider serviceProvider)
        {
            _setting = options.Value;
            _configuration = configuration;
            _repository = repository;
            _mapper = mapper;
            _serviceProvider = serviceProvider;
        }

        private async Task<bool> JustToken()
        {
            var tokenList = InitConfig.Get_TokenList();
            var oldToken = InitConfig.Get_OldToken();
            if (tokenList.Count < 1 || oldToken == null || (oldToken.PassTime < DateTime.Now.AddMinutes(3) && tokenList.Count < 2))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 无头浏览器
        /// </summary>
        /// <returns></returns>
        public async Task<bool> BuildToken_cmd()
        {
            //解决5分钟超时 则去重新获取
            var userDataDir = "D:\\work\\NF";
            var port = "9898";

            using var playwright = await Playwright.CreateAsync();
            try
            {
                await playwright.Chromium.ConnectOverCDPAsync($"http://localhost:{port}", new BrowserTypeConnectOverCDPOptions { SlowMo = 10 });
            }
            catch (Exception ex)
            {
                try
                {
                    var browserPath = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
                    ProcessStartInfo psi = new ProcessStartInfo(browserPath);
                    psi.Arguments = $" --remote-debugging-port={port} --user-data-dir=\"{userDataDir}\"  --start-maximized  --incognito --new-window https://www.spirit.com";
                    //psi.Arguments = $" --remote-debugging-port={port}  --start-maximized  --incognito --new-window https://www.taobao.com";

                    Process.Start(psi);
                    await Task.Delay(2000);
                }
                catch (Exception ex1)
                {
                    Console.WriteLine("Error opening the browser: " + ex1.Message);
                }
            }
            await using var browser = await playwright.Chromium.ConnectOverCDPAsync($"http://localhost:{port}", new BrowserTypeConnectOverCDPOptions { SlowMo = 10 });
            var defaultContext = browser.Contexts[0];
            var page = defaultContext.Pages[0];
            return await DoRunPage(page);
        }

        /// <summary>
        /// 打开浏览器无缓存
        /// </summary>
        /// <returns></returns>
        public async Task<bool> GetToken()
        {
            if (!await JustToken())
            {
                return await BuildToken();
            }

            return true;
        }

        public async Task<bool> BuildToken()
        {
            var json = "";
            // 指定可执行文件的路径
            var pythonScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NK_Flight_Pass.exe");
            var checkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rootbot.png");
            // 构造命令行参数字符串
            string arguments = $"--path {checkPath}";

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = pythonScriptPath; // 可执行文件的路径
            startInfo.Arguments = arguments; // 传递给可执行文件的参数
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardError = true;
            using Process process = new Process { StartInfo = startInfo };
            try
            {
                process.Start();

                // 异步读取输出
                StringBuilder output = new StringBuilder();
                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        output.AppendLine(args.Data);
                    }
                };

                process.BeginOutputReadLine();
                process.WaitForExit(1000 * 180); //3分钟
                // 读取并显示Python脚本的输出
                json = output.ToString();
                Log.Information($"脚本返回{json}");
                json = HtmlHelper.MidMatchString(json, "jsonstart", "jsonEnd");
                if (!string.IsNullOrWhiteSpace(json))
                {
                    dynamic qq = JsonConvert.DeserializeObject(json);
                    dynamic header = JsonConvert.DeserializeObject(qq.header.ToString());
                    dynamic passTime = JsonConvert.DeserializeObject(qq.token.ToString());
                    var longtime = long.Parse(passTime.lastUsedTimeInMilliseconds.ToString());
                    var intduring = Convert.ToInt32(passTime.idleTimeoutInMinutes);
                    var useTime = UtilTimeHelper.ConvertToDateTimeByTimeSpane(longtime);
                    TokenUserModel tokenModel = new TokenUserModel
                    {
                        PassTime = useTime.AddMinutes(intduring),
                        UseTime = useTime,
                        Headers = qq.header.ToString(),
                        Cookies = qq.cookies.ToString(),
                        Token = passTime.token.ToString()
                    };
                    InitConfig.AddTokenList(tokenModel);
                    Log.Information($"获取token成功");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred: {ex.Message}");
            }
            finally
            {
                process.Kill();
            }
            return true;
        }

        /// <summary>
        /// 构建token
        /// </summary>
        /// <returns></returns>
        public async Task<bool> BuildTokencmd()
        {
            using var playwright = await Playwright.CreateAsync();

            var args = new List<string>() { "--start-maximized", "--disable-blink-features=AutomationControlled", "--no-sandbox" };
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Args = args.ToArray(),
                Timeout = 120000,
                Headless = false,
                ChromiumSandbox = true,
                IgnoreDefaultArgs = new[] { "--enable-automation" },
                SlowMo = 100,
            });
            var page = await browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = ViewportSize.NoViewport });
            return await DoRunPage(page);
        }

        /// <summary>
        /// 正常操作按钮
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public async Task DoClick(IPage page, bool isSec = false)
        {
            try
            {
                var title = await page.TitleAsync();
                if (title.Contains("Access"))
                {
                    return;
                }
                if (isSec)
                {
                    await Task.Delay(3000);
                }
                var cookiesBtn = await page.QuerySelectorAsync("#onetrust-accept-btn-handler");
                if (cookiesBtn != null)
                {
                    await cookiesBtn?.ClickAsync();
                }
                await Task.Delay(1500);
                var closeBtn = await page.QuerySelectorAsync(".close");
                if (closeBtn != null)
                {
                    await closeBtn?.ClickAsync();
                }
                await page.WaitForSelectorAsync(".toStation", new PageWaitForSelectorOptions { Timeout = 5000 });
                var toStationbtn = await page.QuerySelectorAsync(".toStation");
                await toStationbtn?.ClickAsync(new ElementHandleClickOptions { Timeout = 2000 });
                var descBtn = await page.QuerySelectorAsync($".stationPickerDestDropdown > div > div > div.d-flex.flex-column.flex-wrap.ng-star-inserted > div:nth-child(1) > div > p");
                await descBtn?.ClickAsync(new ElementHandleClickOptions { Timeout = 2000 });

                var subBtn = await page.QuerySelectorAsync(".btn-block");
                await subBtn.ClickAsync(new ElementHandleClickOptions { Timeout = 2000 });
                await page.WaitForSelectorAsync(".nk-breadcrumbs", new PageWaitForSelectorOptions { Timeout = 7000 });
            }
            catch (Exception ex)
            {
                if (!isSec)
                {
                    throw ex;
                }
                else
                {
                    Log.Information($"rootbot:人机识别后还是报错了");
                }
            }
        }

        public async Task<bool> DoRunPage(IPage page)
        {
            List<string> robotToken = new List<string>();
            string token = "";
            var exLog = false;
            var min = 0;
            var max = 100;
            try
            {
                page.Response += listenerResonse;
                async void listenerResonse(object sender, IResponse request)
                {
                    try
                    {
                        if (request.Url.Contains("/assets/js/bundle"))
                        {
                            var body = await request.JsonAsync();
                            var json = body.ToString();
                            dynamic obj = JsonConvert.DeserializeObject(json);
                            var ob = Convert.ToString(obj.ob);
                            robotToken.Add(ob);
                            Log.Information($"rootbot:{request.Url}");
                        }
                        if (request.Url.Contains("api/availability/search"))
                        {
                            var value = JsonConvert.SerializeObject(request.Request.Headers);
                            var urls = new List<string> { request.Url };
                            var cookiesList = await page.Context.CookiesAsync(urls);
                            string cookies = "";
                            var passToken = "";
                            foreach (var cookie in cookiesList)
                            {
                                if (cookie.Name == "tokenData")
                                {
                                    passToken = cookie.Value;
                                }
                                cookies += $"{cookie.Name}={cookie.Value};";
                            }
                            dynamic passTime = JsonConvert.DeserializeObject(passToken);
                            var longtime = long.Parse(passTime.lastUsedTimeInMilliseconds.ToString());
                            var intduring = Convert.ToInt32(passTime.idleTimeoutInMinutes);
                            var useTime = UtilTimeHelper.ConvertToDateTimeByTimeSpane(longtime);
                            token = request.Request.Headers["authorization"];
                            TokenUserModel tokenModel = new TokenUserModel
                            {
                                PassTime = useTime.AddMinutes(intduring),
                                UseTime = useTime,
                                Headers = value,
                                Cookies = cookies,
                                Token = token
                            };
                            InitConfig.AddTokenList(tokenModel);

                            Log.Information($"rootbot:{DateTime.Now} tokenCount【 {InitConfig.Get_TokenList().Count}】  获取到token {token}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"rootbot:监听失败{ex.Message}");
                    }
                };
                await page.RouteAsync("**/*", async route =>
                {
                    var blockList = new List<string> { "image" }; // 禁止加载的资源类型
                    if (blockList.Contains(route.Request.ResourceType)) await route.AbortAsync();
                    else await route.ContinueAsync(); // 其他资源继续加载
                });
                var response = await page.GotoAsync(url, new PageGotoOptions { Timeout = 120000 });
                await Task.Delay(3000);
                var isRobot = await DoRobot(page, robotToken, min, max);
                if (!isRobot)
                {
                    await DoClick(page);
                }
                var during = 0;
                while (string.IsNullOrWhiteSpace(token))
                {
                    await Task.Delay(100);
                    if (during > 1000 * 10)
                    {
                        break;
                    }
                    during += 100;
                }
                return true;
            }
            catch (Exception ex)
            {
                exLog = true;

                Log.Error($"rootbot:获取token报错{ex.Message}");
                if (robotToken.Count > 0)
                {
                    Log.Information($"rootbot:robotToken【{robotToken.Count}】");
                    await DoRobot(page, robotToken, min, max);
                }

                return false;
            }
            finally
            {
                await page.CloseAsync();
            }
        }

        /// <summary>
        /// js破解 人机进度条时间
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<int> GetSleepTime(string token)
        {
            byte[] data = System.Convert.FromBase64String(token);
            var base64Decoded = System.Text.ASCIIEncoding.ASCII.GetString(data);

            var secStr = await GetPt(base64Decoded, 122);
            var secList = secStr.Split("|").ToList();
            var contain = secList.Where(n => n.Contains("1oo11o")).FirstOrDefault();
            if (contain == null)
            {
                secStr = await GetPt(base64Decoded, 0);
                secList = secStr.Split("|").ToList();
                contain = secList.Where(n => n.Contains("1oo11o")).FirstOrDefault();
                if (contain == null)
                {
                    return 0;
                }
            }
            var iddex = secList.IndexOf(contain);
            var fith = secList[iddex + 4].Split("_").LastOrDefault();
            var result = await GetPt(fith, 10);
            return Convert.ToInt32(result);
        }

        public async Task<string> GetPt(string s, int e)
        {
            var res = "";
            for (var i = 0; i < s.Length; i++)
            {
                var cc = e ^ (int)s[i];
                var app = Convert.ToChar(cc).ToString();
                res += app;
            }
            return res;
        }

        /// <summary>
        /// 点击人机验证
        /// </summary>
        /// <param name="page"></param>
        /// <param name="token"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public async Task<bool> DoRobot(IPage page, List<string> token, int min = 40, int max = 45)
        {
            try
            {
                var tip = await page.QuerySelectorAsync("#px-captcha-modal");
                var dia = await page.QuerySelectorAsync("#px-captcha");
                var title = await page.TitleAsync();
                var isRobot = false;
                if (tip != null || dia != null || title.Contains("Access"))
                {
                    isRobot = true;
                    var sleepTime = 0;
                    Random random = new Random();
                    var ran = random.Next(min, max);
                    foreach (var tokenItem in token)
                    {
                        var needTime = await GetSleepTime(tokenItem);
                        Log.Information($"rootbot::算出时间{needTime}ms");
                        if (needTime > 0)
                        {
                            ran = needTime / 100;
                            /*  if (needTime % 1000 > 0)
                              {
                                  needTime = (needTime / 1000 + 1) * 1000;
                              }*/
                            sleepTime = needTime + ran;
                        }
                    }
                    if (sleepTime == 0)
                    {
                        return false;
                    }
                    var clickX = InitConfig.GetClickX();
                    var clickY = InitConfig.GetClickY();
                    if (InitConfig.GetClickX() == 0)
                    {
                        var srcPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cf.jpg");
                        await page.ScreenshotAsync(new()
                        {
                            Path = srcPath,
                        });
                        var checkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rootbot.png");
                        Mat temp = Cv2.ImRead(checkPath);
                        //被匹配图
                        Mat wafer = Cv2.ImRead(srcPath);
                        //匹配结果
                        Mat result = new Mat();
                        //模板匹配
                        Cv2.MatchTemplate(wafer, temp, result, TemplateMatchModes.CCoeffNormed);//最好匹配为1,值越小匹配越差
                                                                                                //数组位置下x,y

                        Point minLoc = new Point(0, 0);
                        Point maxLoc = new Point(0, 0);
                        Point matchLoc = new Point(0, 0);
                        Cv2.MinMaxLoc(result, out minLoc, out maxLoc);
                        matchLoc = maxLoc;
                        Mat mask = wafer.Clone();
                        //画框显示
                        /*       var clickX = matchLoc.X + 130;
                               var clickY = matchLoc.Y + 35;*/
                        clickX = matchLoc.X;
                        clickY = matchLoc.Y;
                        InitConfig.SetClickX(clickX, clickY);
                    }
                    Log.Information($"rootbot:点击{clickX}【{clickY}】休眠【{sleepTime}】偏移量【{ran}】");
                    await page.Mouse.MoveAsync(clickX, clickY);
                    await page.Mouse.DownAsync();
                    Task.Delay(sleepTime).Wait();
                    await page.Mouse.UpAsync();
                    await Task.Delay(5000);
                    await page.GotoAsync(url);
                    await DoClick(page, true);
                }
                return isRobot;
            }
            catch (Exception ex)
            {
                Log.Information($"处理机器人报错{ex.Message}");
                return true;
            }
        }

        #region 构建城市

        /// <summary>
        /// 人机识别（构建城市）
        /// </summary>
        /// <param name="page"></param>
        /// <param name="isSec"></param>
        /// <returns></returns>
        public async Task DoClickCity(IPage page, bool isSec = false)
        {
            try
            {
                var title = await page.TitleAsync();
                if (title.Contains("Access"))
                {
                    return;
                }
                if (isSec)
                {
                    await Task.Delay(3000);
                }
                var cookiesBtn = await page.QuerySelectorAsync("#onetrust-accept-btn-handler");
                if (cookiesBtn != null)
                {
                    await cookiesBtn?.ClickAsync();
                }
                await Task.Delay(1500);
                var closeBtn = await page.QuerySelectorAsync(".close");
                if (closeBtn != null)
                {
                    await closeBtn?.ClickAsync();
                }

                var onway = await page.QuerySelectorAsync("body > app-root > main > div.container > app-home-page > section.home-widget-section.breakout-full-width.ng-star-inserted > div > div > div > div > app-home-widget > div > div.home-widget-wrapper > form > div > div > div.home-widget.fare-selection > div.left > app-nk-dropdown > div > label > div");
                await onway.ClickAsync();
                await page.QuerySelectorAsync("#oneWay").Result.ClickAsync();
                await page.WaitForSelectorAsync(".fromStation");
                var fromBtn = await page.QuerySelectorAsync(".fromStation");
                await fromBtn?.ClickAsync(new ElementHandleClickOptions { Timeout = 2000 });
                await page.WaitForSelectorAsync(".toStation");
                var fromLocation = await page.QuerySelectorAllAsync("#widget > div.home-widget.ng-tns-c170-3 > div > div.home-widget.city-selection.ng-tns-c170-3.ng-star-inserted > div.city-selection.station-list.station-list-picker-origin.ng-tns-c170-3 > app-station-picker-dropdown > div > div > div.d-flex.flex-column.flex-wrap.ng-star-inserted >div");
                foreach (var item in fromLocation)
                {
                    List<NkToAirlCity> toCity = new List<NkToAirlCity>();
                    if (fromLocation.ToList().IndexOf(item) != 0)
                    {
                        await fromBtn?.ClickAsync(new ElementHandleClickOptions { Timeout = 2000 });
                    }
                    await item.ClickAsync(new ElementHandleClickOptions { Timeout = 2000 });
                    var p = await item.QuerySelectorAsync("p");
                    var cityText = await p.InnerHTMLAsync();
                    var h4 = await item.QuerySelectorAsync("h4");
                    var h5 = await h4.InnerHTMLAsync();
                    //需要特殊处理
                    var fromcityList = cityText.Split(",").FirstOrDefault().Trim().Split("/");
                    foreach (var fromcityText in fromcityList)
                    {
                        var searchcity = h5.Trim();
                        if (h5.Contains("Airports"))
                        {
                            searchcity = await GetShortCity(fromcityText);
                            Log.Error($"需要特殊处理{cityText}");
                        }
                        var toStationbtn = await page.QuerySelectorAsync(".toStation");
                        await toStationbtn?.ClickAsync(new ElementHandleClickOptions { Timeout = 2000 });
                        var descLocation = await page.QuerySelectorAllAsync(".stationPickerDestDropdown > div > div > div.d-flex.flex-column.flex-wrap.ng-star-inserted > div");
                        foreach (var toItem in descLocation)
                        {
                            var to_p = await toItem.QuerySelectorAsync("p");
                            var to_cityText = await to_p.InnerHTMLAsync();
                            var to_h4 = await toItem.QuerySelectorAsync("h4");
                            var to_h5 = await to_h4.InnerHTMLAsync();
                            var cityList = to_cityText.Split(",").FirstOrDefault().Trim().Split("/");
                            foreach (var tocityText in cityList)
                            {
                                //需要特殊处理
                                NkToAirlCity tocity = new NkToAirlCity()
                                {
                                    city = tocityText.Trim(),
                                    searchcity = to_h5.Trim(),
                                    fromcity = fromcityText,
                                    searchFromCity = searchcity
                                };
                                if (to_h5.Contains("Airports"))
                                {
                                    tocity.searchcity = await GetShortCity(tocity.city);
                                }
                                toCity.Add(tocity);
                            }
                        }
                    }
                    await _repository.GetRepository<NkToAirlCity>().BatchInsertAsync(toCity);
                }
            }
            catch (Exception ex)
            {
                if (!isSec)
                {
                    throw ex;
                }
                else
                {
                    Log.Information($"rootbot:人机识别后还是报错了");
                }
            }
        }

        /// <summary>
        /// 点击人机验证
        /// </summary>
        /// <param name="page"></param>
        /// <param name="token"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public async Task<bool> DoRobotCity(IPage page, List<string> token)
        {
            try
            {
                var tip = await page.QuerySelectorAsync("#px-captcha-modal");
                var dia = await page.QuerySelectorAsync("#px-captcha");
                var title = await page.TitleAsync();
                var isRobot = false;
                if (tip != null || dia != null || title.Contains("Access"))
                {
                    isRobot = true;
                    var sleepTime = 0;
                    Random random = new Random();
                    var ran = 0;
                    foreach (var tokenItem in token)
                    {
                        var needTime = await GetSleepTime(tokenItem);
                        Log.Information($"rootbot::算出时间{needTime}ms");
                        if (needTime > 0)
                        {
                            ran = needTime / 100;
                            sleepTime = needTime + ran;
                        }
                    }
                    if (sleepTime == 0)
                    {
                        return false;
                    }
                    var clickX = InitConfig.GetClickX();
                    var clickY = InitConfig.GetClickY();
                    if (InitConfig.GetClickX() == 0)
                    {
                        var srcPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cf.jpg");
                        await page.ScreenshotAsync(new()
                        {
                            Path = srcPath,
                        });
                        var checkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rootbot.png");
                        Mat temp = Cv2.ImRead(checkPath);
                        //被匹配图
                        Mat wafer = Cv2.ImRead(srcPath);
                        //匹配结果
                        Mat result = new Mat();
                        //模板匹配
                        Cv2.MatchTemplate(wafer, temp, result, TemplateMatchModes.CCoeffNormed);//最好匹配为1,值越小匹配越差
                                                                                                //数组位置下x,y

                        Point minLoc = new Point(0, 0);
                        Point maxLoc = new Point(0, 0);
                        Point matchLoc = new Point(0, 0);
                        Cv2.MinMaxLoc(result, out minLoc, out maxLoc);
                        matchLoc = maxLoc;
                        Mat mask = wafer.Clone();
                        //画框显示
                        /*       var clickX = matchLoc.X + 130;
                               var clickY = matchLoc.Y + 35;*/
                        clickX = matchLoc.X;
                        clickY = matchLoc.Y;
                        InitConfig.SetClickX(clickX, clickY);
                    }
                    Log.Information($"rootbot:点击{clickX}【{clickY}】休眠【{sleepTime}】偏移量【{ran}】");
                    await page.Mouse.MoveAsync(clickX, clickY);
                    await page.Mouse.DownAsync();
                    Task.Delay(sleepTime).Wait();
                    await page.Mouse.UpAsync();
                    await Task.Delay(5000);
                    await page.GotoAsync(url);
                    await DoClickCity(page, true);
                }
                return isRobot;
            }
            catch (Exception ex)
            {
                Log.Information($"处理机器人报错{ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// 获取配置编码
        /// </summary>
        /// <returns></returns>
        private async Task<ClientKey> GetClientKey()
        {
            var clientKeys = _configuration.GetSection("ClientKey").Get<List<ClientKey>>();
            return clientKeys.FirstOrDefault();
        }

        public async Task<string> BuildCity()
        {
            var citys = await _repository.GetRepository<NkToAirlCity>().Query().ToListAsync();
            var timeSpan = GetTimeStamp();
            var clientKeys = _configuration.GetSection("ClientKey").Get<List<ClientKey>>();
            var clients = clientKeys.FirstOrDefault();
            var md5 = GetMD5WithString($"{clients.AppId}{clients.AccessKey}{timeSpan}");
            if (citys.Count == 0)
            {
                using var playwright = await Playwright.CreateAsync();

                var args = new List<string>() { "--start-maximized", "--disable-blink-features=AutomationControlled", "--no-sandbox" };
                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Args = args.ToArray(),
                    Timeout = 120000,
                    Headless = false,
                    ChromiumSandbox = true,
                    IgnoreDefaultArgs = new[] { "--enable-automation" },
                    SlowMo = 100,
                });
                var page = await browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = ViewportSize.NoViewport });
                List<string> robotToken = new List<string>();
                try
                {
                    page.Response += listenerResonse;
                    async void listenerResonse(object sender, IResponse request)
                    {
                        try
                        {
                            if (request.Url.Contains("/assets/js/bundle"))
                            {
                                var body = await request.JsonAsync();
                                var json = body.ToString();
                                dynamic obj = JsonConvert.DeserializeObject(json);
                                var ob = Convert.ToString(obj.ob);
                                robotToken.Add(ob);
                                Log.Information($"rootbot:{request.Url}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"rootbot:监听失败{ex.Message}");
                        }
                    };
                    await page.RouteAsync("**/*", async route =>
                    {
                        var blockList = new List<string> { "image" }; // 禁止加载的资源类型
                        if (blockList.Contains(route.Request.ResourceType)) await route.AbortAsync();
                        else await route.ContinueAsync(); // 其他资源继续加载
                    });
                    var response = await page.GotoAsync(url, new PageGotoOptions { Timeout = 300000 });
                    await Task.Delay(3000);
                    var isRobot = await DoRobotCity(page, robotToken);
                    if (!isRobot)
                    {
                        await DoClickCity(page);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"rootbot:获取token报错{ex.Message}");
                    if (robotToken.Count > 0)
                    {
                        Log.Information($"rootbot:robotToken【{robotToken.Count}】");
                        await DoRobotCity(page, robotToken);
                    }
                    Log.Error($"获取城市报错{ex.Message}");
                }
                finally
                {
                    await page.CloseAsync();
                }
            }
            return md5 + "_" + timeSpan;
        }

        /// <summary>
        /// 获取时间戳
        /// </summary>
        /// <returns></returns>
        public static string GetTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalMilliseconds).ToString();
        }

        private string GetMD5WithString(String input)
        {
            MD5 md5Hash = MD5.Create();
            // 将输入字符串转换为字节数组并计算哈希数据
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            // 创建一个 Stringbuilder 来收集字节并创建字符串
            StringBuilder str = new StringBuilder();
            // 循环遍历哈希数据的每一个字节并格式化为十六进制字符串
            for (int i = 0; i < data.Length; i++)
            {
                str.Append(data[i].ToString("x2")); //加密结果"x2"结果为32位,"x3"结果为48位,"x4"结果为64位
            }
            // 返回十六进制字符串
            return str.ToString();
        }

        #endregion 构建城市

        public async Task<bool> PushAllFlightToDb(SearchDayDto dto)
        {
            var AllTo = await _repository.GetRepository<NkToAirlCity>().QueryListAsync();
            var date = DateTime.Now.Date.AddDays(dto.day.Value);
            var journeyRe = _repository.GetRepository<NKJourney>();
            var segmentRe = _repository.GetRepository<NKSegment>();
            //内存队列
            var queue = new CustomMemoryQueue<JourneyInsertModel>();

            var se = _serviceProvider;
            foreach (var tocity in AllTo)
            {
                var index = AllTo.IndexOf(tocity);
                try
                {
                    Task.Run(() => DetailWithDb(date, dto, se, queue, tocity, index));
                    //await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Log.Error($"Api:已执行{index}条数据");
                    Log.Error($"{tocity.fromcity}_{tocity.city}_{date}获取失败 {ex.Message}");
                }
            }
            while (true)
            {
                try
                {
                    var model = new JourneyInsertModel();
                    var has = queue.TryDequeue(out model);
                    if (has)
                    {
                        using var transaction = await _repository.BeginTransactionAsync();
                        await segmentRe.BatchDeleteAsync(model.dbRegList);
                        await journeyRe.InsertAsync(model.NKJourney);
                        await segmentRe.BatchInsertAsync(model.SegList);
                        await transaction.CommitAsync();
                        Log.Information($"Api:Commint提交【{model.index}】成功");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"退站出错{ex.Message}");
                }
            }
            return true;
        }

        private static SemaphoreSlim semaphore = new SemaphoreSlim(3); // 控制同时运行的线程数量为10

        public async Task<IBaseRepository<HeyTripDbContext>> GetRto()
        {
            return _repository;
        }

        public async Task DetailWithDb(DateTime date, SearchDayDto dto, IServiceProvider service, CustomMemoryQueue<JourneyInsertModel> queue, NkToAirlCity tocity, int index)
        {
            await semaphore.WaitAsync(); // 尝试获取信号量，如果无法获取则等待
            try
            {
                using var scope = service.CreateAsyncScope();
                var _domain = scope.ServiceProvider.GetRequiredService<INkFlightDomain>();
                var repository = await _domain.GetRto();
                var journeyRe = repository.GetRepository<NKJourney>();
                var segmentRe = repository.GetRepository<NKSegment>();
                Stopwatch stopwatch = Stopwatch.StartNew();
                stopwatch.Start();
                StepDto stepDto = new StepDto()
                {
                    startArea = tocity.searchcity,
                    endArea = tocity.city,
                    adtSourceNum = dto.AtdNum,
                    childSourceNum = dto.ChildNum,
                    fromTime = date,
                    FromSegments = new List<SegmentLineDetail>
                        {
                            new SegmentLineDetail
                            {
                                ArrAirport = tocity.searchcity,
                                DepAirport=tocity.searchFromCity,
                                DepDate=date.ToString("yyyy-MM-dd")
                            }
                        }
                };
                var resList = await GetPriceDetail(stepDto);
                if (resList.Count > 0)
                {
                    //using var transaction = await _repository.BeginTransactionAsync();
                    NKJourney lionAirlJourney = new NKJourney
                    {
                        JourneyId = Guid.NewGuid(),
                        TripType = (int)FlightSegmentType.OneWay,
                        DepCity = tocity.searchFromCity,
                        ArrCity = tocity.searchcity,
                        DepTime = Convert.ToDateTime(date),
                        Adult = dto.AtdNum,
                        Child = dto.ChildNum,
                        RequestTime = DateTime.Now
                    };
                    List<NKSegment> segments = new List<NKSegment>();
                    List<string> rateCodes = resList.Select(n => n.RateCode).Distinct().ToList();
                    var dbRegList = await segmentRe.Query().Where(n => rateCodes.Contains(n.RateCode)).ToListAsync();
                    foreach (var item in resList)
                    {
                        foreach (var seg in item.FromSegments)
                        {
                            NKSegment segment = new NKSegment
                            {
                                SegmentId = Guid.NewGuid(),
                                JourneyId = lionAirlJourney.JourneyId,
                                RateCode = item.RateCode,
                                Carrier = seg.Carrier,
                                Cabin = seg.Cabin,
                                CabinClass = (int)seg.CabinClass,
                                FlightNumber = seg.FlightNumber,
                                DepAirport = seg.DepAirport,
                                ArrAirport = seg.ArrAirport,
                                DepDate = seg.DepDate,
                                ArrDate = seg.ArrDate,
                                StopCities = seg.StopCities,
                                CodeShare = seg.CodeShare,
                                ShareCarrier = seg.ShareCarrier,
                                ShareFlightNumber = seg.ShareFlightNumber,
                                AircraftCode = seg.AircraftCode,
                                Group = seg.Group,
                                FareBasis = seg.FareBasis,
                                GdsType = seg.GdsType == null ? null : (int)seg.GdsType,
                                PosArea = seg.PosArea,
                                AirlinePnrCode = seg.AirlinePnrCode,
                                BaggageRule = seg.BaggageRule == null ? "" : JsonConvert.SerializeObject(seg.BaggageRule),
                            };
                            segments.Add(segment);
                        }
                    }
                    JourneyInsertModel insertModel = new JourneyInsertModel
                    {
                        dbRegList = dbRegList,
                        SegList = segments,
                        NKJourney = lionAirlJourney,
                        index = index
                    };
                    Log.Information($"Api:{index}入队列");
                    queue.Enqueue(insertModel);
                    /*  var i = 1;
                      while (i < 100)
                      {
                          try
                          {
                              await segmentRe.BatchDeleteAsync(dbRegList);
                              await journeyRe.InsertAsync(lionAirlJourney);
                              await segmentRe.BatchInsertAsync(segments);
                              await transaction.CommitAsync();
                              Log.Information($"Api:Commint提交【{index}】【{i}】成功");
                              break;
                          }
                          catch (Exception ex)
                          {
                              await transaction.RollbackAsync();
                              i++;
                              Log.Error($"Api:Commint提交【{index}】【{i}】失败{ex.Message}");
                          }
                      }*/
                }
                stopwatch.Stop();
                Log.Information($"Api:【{index}】抓取到数据{resList.Count}条报价数据耗时{stopwatch.ElapsedMilliseconds}");
            }
            catch (Exception ex)
            {
                Log.Error($"Api:【{index}】报错{ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<string> GetShortCity(string city)
        {
            switch (city)
            {
                case "Boston Area":
                    return "1BO";

                case "New York City Area":
                    return "1NY";

                case "Central Texas":
                    return "1CT";

                case "Los Angeles Area":
                    return "1LA";

                case "Philadelphia Area":
                    return "1PH";

                case "Miami Area":
                    return "1FL";

                case "Pittsburgh Area":
                    return "1PT";

                case "Puerto Rico":
                    return "1PR";

                case "San Francisco Area":
                    return "1SF";

                default:
                    return city;
            }
        }

        /// <summary>
        /// 搜价接口
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<SearchAirtickets_Data> SearchAirtickets(SearchAirticketsInput dto)
        {
            Log.Information($"接口接收到了{JsonConvert.SerializeObject(dto)}");
            SearchAirtickets_Data searchAirtickets_Data = new SearchAirtickets_Data() { Success = true };

            List<SegmentLineDetail> fromList = new List<SegmentLineDetail>();
            foreach (var seg in dto.Data.FromSegments)
            {
                fromList.Add(new SegmentLineDetail
                {
                    ArrAirport = seg.ArrCityCode,
                    DepAirport = seg.DepCityCode,
                    DepDate = seg.DepDate
                });
            }
            List<SegmentLineDetail> toList = new List<SegmentLineDetail>();
            foreach (var seg in dto.Data.RetSegments)
            {
                toList.Add(new SegmentLineDetail
                {
                    ArrAirport = seg.ArrCityCode,
                    DepAirport = seg.DepCityCode,
                    DepDate = seg.DepDate
                });
            }
            StepDto stepDto = new StepDto()
            {
                adtSourceNum = dto.Data.AdultNum,
                childSourceNum = dto.Data.ChildNum,
                cabinClass = dto.Data.CabinClass,
                FlightNumber = dto.Data.FlightNumber,
                carrier = dto.Data.Carrier,
                Cabin = dto.Data.Cabin,
                FromSegments = fromList,
                RetSegments = toList,
            };
            var result = await GetPriceDetail(stepDto);
            
            searchAirtickets_Data.PriceDetails = result;
            return searchAirtickets_Data;
        }

        public async Task<List<SearchAirticket_PriceDetail>> GetPriceDetail(StepDto dto)
        {
            if (dto.FromSegments.Count > 0)
            {
                dto.FromSegments = new List<SegmentLineDetail>
                    {
                        new SegmentLineDetail
                        {
                            ArrAirport = dto.FromSegments.LastOrDefault().ArrAirport,
                            DepAirport = dto.FromSegments.FirstOrDefault().DepAirport,
                            DepDate = Convert.ToDateTime(dto.FromSegments.FirstOrDefault().DepDate).ToString("yyyy-MM-dd")
                        }
                    };
            }
            if (dto.RetSegments.Count > 0)
            {
                dto.RetSegments = new List<SegmentLineDetail>
                    {
                        new SegmentLineDetail
                        {
                            ArrAirport = dto.RetSegments.LastOrDefault().ArrAirport,
                            DepAirport = dto.RetSegments.FirstOrDefault().DepAirport,
                            DepDate = Convert.ToDateTime(dto.RetSegments.FirstOrDefault().DepDate).ToString("yyyy-MM-dd")
                        }
                    };
            }
            var segList = await StepSearch(dto);
            List<SearchAirticket_PriceDetail> res = new List<SearchAirticket_PriceDetail>();
            foreach (var item in dto.FromSegments)
            {
                var key = $"{Convert.ToDateTime(item.DepDate).ToString("yyyy-MM-dd")}_{item.DepAirport}_{item.ArrAirport}";
                var dict = segList.FirstOrDefault(n => n.Key == key);
                if (dict != null)
                {
                    res = dict.Detail;
                }
            }

            List<SearchAirticket_PriceDetail> backRes = new List<SearchAirticket_PriceDetail>();
            foreach (var item in dto.RetSegments)
            {
                var key = $"{Convert.ToDateTime(item.DepDate).ToString("yyyy-MM-dd")}_{item.DepAirport}_{item.ArrAirport}";
                var dict = segList.FirstOrDefault(n => n.Key == key);
                if (dict != null)
                {
                    backRes = dict.Detail;
                }
            }
            List<SearchAirticket_PriceDetail> result = new List<SearchAirticket_PriceDetail>();
            foreach (var come in res)
            {
                if (backRes.Count == 0)
                {
                    come.RateCode = $"NK_{come.RateCode}_{dto.adtSourceNum}_{dto.childSourceNum}";
                    result.Add(come);
                }
                foreach (var back in backRes)
                {
                    var newSeg = _mapper.Map<SearchAirticket_PriceDetail, SearchAirticket_PriceDetail>(come);
                    newSeg.RateCode = $"NK_{come.RateCode}|{back.RateCode}_{dto.adtSourceNum}_{dto.childSourceNum}";
                    newSeg.AdultPrice = come.AdultPrice + back.AdultPrice;
                    newSeg.AdultTax = come.AdultTax + back.AdultTax;
                    newSeg.ChildPrice = come.ChildPrice + back.ChildPrice;
                    newSeg.ChildTax = come.ChildTax + back.ChildTax;
                    newSeg.RetSegments = back.RetSegments;
                    result.Add(newSeg);
                }
            }
            if (res.Count == 0)
            {
                foreach (var back in backRes)
                {
                    back.RateCode = $"NK_{back.RateCode}_{dto.adtSourceNum}_{dto.childSourceNum}";
                    result.Add(back);
                }
            }
            Log.Information($"Api:获取到报价{result.Count}去程{res.Count}来程{backRes.Count}");
            return result;
        }

        /// <summary>
        /// 搜价格接口
        /// </summary>
        /// <param name="stepDto"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<List<PriceDetailDict>> StepSearch(StepDto stepDto)
        {
            var result = new List<PriceDetailDict>();
            Stopwatch stopwatch = Stopwatch.StartNew();
            stopwatch.Start();
            var adtNum = stepDto.adtSourceNum.Value;
            var childNum = stepDto.childSourceNum.Value;

            List<passengersType> types = new List<passengersType>();
            if (stepDto.adtSourceNum > 0)
            {
                types.Add(new passengersType
                {
                    type = "ADT",
                    count = adtNum,
                });
            }
            if (stepDto.childSourceNum > 0)
            {
                types.Add(new passengersType
                {
                    type = "CHD",
                    count = childNum,
                });
            }
            passengers passengers = new passengers()
            {
                types = types
            };
            /*  var tocity = _repository.GetRepository<NkToAirlCity>().Query()
                  .FirstOrDefault(n => n.city.ToLower() == stepDto.endArea.ToLower() || n.searchcity == stepDto.endArea.ToLower()
              && (n.searchFromCity.ToLower() == stepDto.startArea.ToLower() || n.fromcity.ToLower() == stepDto.startArea.ToLower()));
              if (tocity == null)
              {
                  throw new Exception("不存在的城市");
              }*/
            List<criteria> criteriaList = new List<criteria>();
            foreach (var seg in stepDto.FromSegments)
            {
                var dates = new dates
                {
                    beginDate = Convert.ToDateTime(seg.DepDate).ToString("yyyy-MM-dd"),
                    endDate = Convert.ToDateTime(seg.DepDate).ToString("yyyy-MM-dd")
                };
                var stations = new stations
                {
                    originStationCodes = new List<string> { seg.DepAirport },
                    destinationStationCodes = new List<string> { seg.ArrAirport }
                };
                criteriaList.Add(new criteria
                {
                    dates = dates,
                    stations = stations,
                });
            }

            foreach (var seg in stepDto.RetSegments)
            {
                var dates = new dates
                {
                    beginDate = Convert.ToDateTime(seg.DepDate).ToString("yyyy-MM-dd"),
                    endDate = Convert.ToDateTime(seg.DepDate).ToString("yyyy-MM-dd")
                };
                var stations = new stations
                {
                    originStationCodes = new List<string> { seg.DepAirport },
                    destinationStationCodes = new List<string> { seg.ArrAirport }
                };
                criteriaList.Add(new criteria
                {
                    dates = dates,
                    stations = stations,
                });
            }
            var sourceQuery = new SourceQueryDto
            {
                criteria = criteriaList,
                passengers = passengers,
            };
            var json = JsonConvert.SerializeObject(sourceQuery);
            try
            {
                dynamic data = await BuildApiRequest(json);

                var error = data.errors.ToString();
                if (string.IsNullOrWhiteSpace(error))
                {
                    var trips = data.data.trips;
                    if (trips != null && trips.Count > 0)
                    {
                        for (var i = 0; i < trips.Count; i++)
                        {
                            var cri = criteriaList[i];
                            List<SearchAirticket_PriceDetail> priceList = new List<SearchAirticket_PriceDetail>();
                            var journeys = data.data.trips[i].journeysAvailable;
                            if (journeys != null && journeys.Count > 0)
                            {
                                foreach (var journey in journeys)
                                {
                                    var fares = JsonConvert.SerializeObject(journey.fares);
                                    if (fares == "{}")
                                    {
                                        continue;
                                    }
                                    var faresList = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(fares);
                                    SearchAirticket_PriceDetail model = new SearchAirticket_PriceDetail
                                    {
                                        Currency = data.data.currencyCode //(CurrencyEnums)Enum.Parse(typeof(CurrencyEnums), data.data.currencyCode)
                                    };
                                    var q = 1;
                                    decimal adtPrice = 0;
                                    decimal adtCount = 0;
                                    decimal adttaxPrice = 0;
                                    decimal childPrice = 0;
                                    decimal childCount = 0;
                                    decimal childtaxPrice = 0;
                                    foreach (var fare in faresList)
                                    {
                                        if (q > 1)
                                        {
                                            break;
                                        }
                                        q++;
                                        var passengerFares = fare.Value.details.passengerFares;
                                        foreach (var peo in passengerFares)
                                        {
                                            if (peo.passengerType == "ADT")
                                            {
                                                decimal.TryParse(peo.fareAmount.ToString(), out adtPrice);
                                                decimal.TryParse(peo.multiplier.ToString(), out adtCount);
                                                decimal.TryParse(peo.serviceCharges[1].amount.ToString(), out adttaxPrice);
                                            }
                                            else if (peo.passengerType == "CHD")
                                            {
                                                decimal.TryParse(peo.fareAmount.ToString(), out childPrice);
                                                decimal.TryParse(peo.multiplier.ToString(), out childCount);
                                                decimal.TryParse(peo.serviceCharges[1].amount.ToString(), out childtaxPrice);
                                            }
                                        }
                                    }
                                    model.AdultPrice = adtPrice * adtCount;
                                    model.AdultTax = adttaxPrice * adtCount;
                                    model.ChildPrice = childPrice * childCount;
                                    model.ChildTax = childtaxPrice * childCount;
                                    model.NationalityType = NationalityApplicableType.All;
                                    model.SuitAge = "0~99";
                                    model.MaxFittableNum = 9;
                                    model.MinFittableNum = 1;
                                    model.TicketInvoiceType = 1; //没有默认1
                                    model.TicketAirline = "NK";
                                    List<AirticketRuleItem> refundRule = new List<AirticketRuleItem>();
                                    refundRule.Add(new AirticketRuleItem
                                    {
                                        FlightStatus = FlightStatus.BeforeTakeOff,
                                        Hours = 6 * 24,
                                        Penalty = 119
                                    });
                                    refundRule.Add(new AirticketRuleItem
                                    {
                                        FlightStatus = FlightStatus.BeforeTakeOff,
                                        Hours = 30 * 24,
                                        Penalty = 99
                                    });
                                    refundRule.Add(new AirticketRuleItem
                                    {
                                        FlightStatus = FlightStatus.BeforeTakeOff,
                                        Hours = 59 * 24,
                                        Penalty = 69
                                    });
                                    refundRule.Add(new AirticketRuleItem
                                    {
                                        FlightStatus = FlightStatus.BeforeTakeOff,
                                        Hours = 60 * 24,
                                        Penalty = 0
                                    });
                                    SearchAirticket_Rule rule = new SearchAirticket_Rule
                                    {
                                        HasRefund = true, //设置可退
                                        RefundRule = refundRule,
                                        HasChangeDate = true, //设置可改
                                        ChangeDateRule = refundRule,
                                    };
                                    model.Rule = rule;
                                    model.DeliveryPolicy = "Standardproduct";
                                    var rateCode = $"{cri.stations.originStationCodes.FirstOrDefault()}_{cri.stations.destinationStationCodes.FirstOrDefault()}_{cri.dates.beginDate}";
                                    List<SearchAirticket_Segment> segList = new List<SearchAirticket_Segment>();
                                    foreach (var segment in journey.segments)
                                    {
                                        var eq = segment.legs[0].legInfo.equipmentType.ToString();
                                        SearchAirticket_Segment seg = new SearchAirticket_Segment()
                                        {
                                            Carrier = segment.identifier.carrierCode,
                                            CabinClass = CabinClass.EconomyClass,
                                            FlightNumber = segment.identifier.identifier,
                                            DepAirport = segment.designator.origin,
                                            ArrAirport = segment.designator.destination,
                                            DepDate = Convert.ToDateTime(segment.designator.departure).ToString("yyyy-MM-dd HH:mm"),
                                            ArrDate = Convert.ToDateTime(segment.designator.arrival).ToString("yyyy-MM-dd HH:mm"),
                                            StopCities = segment.designator.destination,
                                            CodeShare = segment.identifier.carrierCode == "NK" ? false : true,
                                            ShareCarrier = segment.identifier.carrierCode == "NK" ? "" : segment.identifier.carrierCode,
                                            ShareFlightNumber = segment.identifier.carrierCode == "NK" ? "" : segment.identifier.identifier,
                                            AircraftCode = await GetAircraftCodeByEq(eq),
                                            BaggageRule = await BuildBaggageRule()
                                        };
                                        segList.Add(seg);
                                        rateCode += $"_{seg.Carrier}{seg.FlightNumber}_{Convert.ToDateTime(seg.DepDate).ToString("yyyyMMddHHmm")}";
                                    }
                                    if (i >= stepDto.FromSegments.Count)
                                    {
                                        model.RetSegments = segList;
                                    }
                                    else
                                    {
                                        model.FromSegments = segList;
                                    }
                                    model.RateCode = $"{rateCode}";
                                    priceList.Add(model);
                                }
                            }

                            if (priceList.Count > 0)
                            {
                                PriceDetailDict priceDetailDict = new PriceDetailDict()
                                {
                                    Detail = priceList,
                                    Key = $"{cri.dates.beginDate}_{cri.stations.originStationCodes.FirstOrDefault()}_{cri.stations.destinationStationCodes.FirstOrDefault()}"
                                };
                                result.Add(priceDetailDict);
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception("请稍后重试");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Api:请求发生错误，{ex.Message}");
            }

            stopwatch.Stop();
            Log.Information($"Api:获取{result.Count}条数据耗时{stopwatch.ElapsedMilliseconds}ms");
            return result;
        }

        /// <summary>
        /// 搜价格接口
        /// </summary>
        /// <param name="stepDto"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<List<SearchAirticket_PriceDetail>> StepSearch_Old(StepDto stepDto)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            stopwatch.Start();
            var adtNum = stepDto.adtSourceNum.Value;
            var childNum = stepDto.childSourceNum.Value;

            var fromDate = stepDto.fromTime.Value.ToString("yyyy-MM-dd");

            List<passengersType> types = new List<passengersType>();
            if (stepDto.adtSourceNum > 0)
            {
                types.Add(new passengersType
                {
                    type = "ADT",
                    count = adtNum,
                });
            }
            if (stepDto.childSourceNum > 0)
            {
                types.Add(new passengersType
                {
                    type = "CHD",
                    count = childNum,
                });
            }
            passengers passengers = new passengers()
            {
                types = types
            };
            List<SearchAirticket_PriceDetail> priceList = new List<SearchAirticket_PriceDetail>();
            var tocity = _repository.GetRepository<NkToAirlCity>().Query()
                .FirstOrDefault(n => n.city.ToLower() == stepDto.endArea.ToLower() || n.searchcity == stepDto.endArea.ToLower()
            && (n.searchFromCity.ToLower() == stepDto.startArea.ToLower() || n.fromcity.ToLower() == stepDto.startArea.ToLower()));
            if (tocity == null)
            {
                throw new Exception("不存在的城市");
            }
            var dates = new dates
            {
                beginDate = stepDto.fromTime.Value.ToString("yyyy-MM-dd"),
                endDate = stepDto.fromTime.Value.ToString("yyyy-MM-dd")
            };

            var stations = new stations
            {
                originStationCodes = new List<string> { tocity.searchFromCity },
                destinationStationCodes = new List<string> { tocity.searchcity }
            };
            List<criteria> criteria = new List<criteria> { new criteria
                {
                    dates = dates,
                    stations = stations,
                } };
            var sourceQuery = new SourceQueryDto
            {
                criteria = criteria,
                passengers = passengers,
            };
            var json = JsonConvert.SerializeObject(sourceQuery);
            try
            {
                dynamic data = await BuildApiRequest(json);

                var error = data.errors.ToString();
                if (string.IsNullOrWhiteSpace(error))
                {
                    var journeys = data.data.trips[0].journeysAvailable;
                    if (journeys != null && journeys.Count > 0)
                    {
                        foreach (var journey in journeys)
                        {
                            var fares = JsonConvert.SerializeObject(journey.fares);
                            if (fares == "{}")
                            {
                                continue;
                            }
                            var faresList = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(fares);
                            SearchAirticket_PriceDetail model = new SearchAirticket_PriceDetail
                            {
                                Currency = data.data.currencyCode //(CurrencyEnums)Enum.Parse(typeof(CurrencyEnums), data.data.currencyCode)
                            };
                            var q = 1;
                            decimal adtPrice = 0;
                            decimal adtCount = 0;
                            decimal adttaxPrice = 0;
                            decimal childPrice = 0;
                            decimal childCount = 0;
                            decimal childtaxPrice = 0;
                            foreach (var fare in faresList)
                            {
                                if (q > 1)
                                {
                                    break;
                                }
                                q++;
                                var passengerFares = fare.Value.details.passengerFares;
                                foreach (var peo in passengerFares)
                                {
                                    if (peo.passengerType == "ADT")
                                    {
                                        decimal.TryParse(peo.fareAmount.ToString(), out adtPrice);
                                        decimal.TryParse(peo.multiplier.ToString(), out adtCount);
                                        decimal.TryParse(peo.serviceCharges[1].amount.ToString(), out adttaxPrice);
                                    }
                                    else if (peo.passengerType == "CHD")
                                    {
                                        decimal.TryParse(peo.fareAmount.ToString(), out childPrice);
                                        decimal.TryParse(peo.multiplier.ToString(), out childCount);
                                        decimal.TryParse(peo.serviceCharges[1].amount.ToString(), out childtaxPrice);
                                    }
                                }
                            }
                            model.AdultPrice = adtPrice * adtCount;
                            model.AdultTax = adttaxPrice * adtCount;
                            model.ChildPrice = childPrice * childCount;
                            model.ChildTax = childtaxPrice * childCount;
                            model.NationalityType = NationalityApplicableType.All;
                            model.SuitAge = "0~99";
                            model.MaxFittableNum = 9;
                            model.MinFittableNum = 1;
                            model.TicketInvoiceType = 1; //没有默认1
                            model.TicketAirline = "NK";
                            List<AirticketRuleItem> refundRule = new List<AirticketRuleItem>();
                            refundRule.Add(new AirticketRuleItem
                            {
                                FlightStatus = FlightStatus.BeforeTakeOff,
                                Hours = 6 * 24,
                                Penalty = 119
                            });
                            refundRule.Add(new AirticketRuleItem
                            {
                                FlightStatus = FlightStatus.BeforeTakeOff,
                                Hours = 30 * 24,
                                Penalty = 99
                            });
                            refundRule.Add(new AirticketRuleItem
                            {
                                FlightStatus = FlightStatus.BeforeTakeOff,
                                Hours = 59 * 24,
                                Penalty = 69
                            });
                            refundRule.Add(new AirticketRuleItem
                            {
                                FlightStatus = FlightStatus.BeforeTakeOff,
                                Hours = 60 * 24,
                                Penalty = 0
                            });
                            SearchAirticket_Rule rule = new SearchAirticket_Rule
                            {
                                HasRefund = true, //设置可退
                                RefundRule = refundRule,
                                HasChangeDate = true, //设置可改
                                ChangeDateRule = refundRule,
                            };
                            model.Rule = rule;
                            model.DeliveryPolicy = "Standardproduct";
                            var rateCode = $"{stepDto.startArea}_{stepDto.endArea}_{fromDate}";
                            List<SearchAirticket_Segment> segList = new List<SearchAirticket_Segment>();
                            foreach (var segment in journey.segments)
                            {
                                var eq = segment.legs[0].legInfo.equipmentType.ToString();
                                SearchAirticket_Segment seg = new SearchAirticket_Segment()
                                {
                                    Carrier = segment.identifier.carrierCode,
                                    CabinClass = CabinClass.EconomyClass,
                                    FlightNumber = segment.identifier.identifier,
                                    DepAirport = segment.designator.origin,
                                    ArrAirport = segment.designator.destination,
                                    DepDate = Convert.ToDateTime(segment.designator.departure).ToString("yyyy-MM-dd HH:mm"),
                                    ArrDate = Convert.ToDateTime(segment.designator.arrival).ToString("yyyy-MM-dd HH:mm"),
                                    StopCities = segment.designator.destination,
                                    CodeShare = segment.identifier.carrierCode == "NK" ? false : true,
                                    ShareCarrier = segment.identifier.carrierCode == "NK" ? "" : segment.identifier.carrierCode,
                                    ShareFlightNumber = segment.identifier.carrierCode == "NK" ? "" : segment.identifier.identifier,
                                    AircraftCode = await GetAircraftCodeByEq(eq),
                                    BaggageRule = await BuildBaggageRule()
                                };
                                segList.Add(seg);
                                rateCode += $"_{seg.Carrier}{seg.FlightNumber}_{Convert.ToDateTime(seg.DepDate).ToString("yyyyMMddHHmm")}";
                            }
                            if (stepDto.IsBack)
                            {
                                model.RetSegments = segList;
                            }
                            else
                            {
                                model.FromSegments = segList;
                            }
                            model.RateCode = $"{rateCode}";
                            priceList.Add(model);
                        }
                    }
                }
                else
                {
                    throw new Exception("请稍后重试");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Api:请求发生错误，{ex.Message}");
            }

            stopwatch.Stop();
            Log.Information($"Api:获取{priceList.Count}条数据耗时{stopwatch.ElapsedMilliseconds}ms");
            return priceList;
        }

        public async Task<string> GetAircraftCodeByEq(string eq)
        {
            switch (eq)
            {
                case "32A":
                case "32N":
                    return "A320";

                case "32B":
                case "32Q":
                    return "A321";

                case "319":
                    return "A319";
            }
            return "A320";
        }

        public async Task<dynamic> BuildApiRequest(string json, int i = 0)
        {
            var tokenList = InitConfig.Get_TokenList();
            tokenList = tokenList.OrderBy(n => n.UseTime).ToList();
            foreach (var dbToken in tokenList)
            {
                try
                {
                    var header = JsonConvert.DeserializeObject<Dictionary<string, string>>(dbToken.Headers);
                    header.Add("Accept-Encoding", "utf-8");
                    var res = HttpHelper.HttpOriginPost(apiUrl, json, header, cookies: dbToken.Cookies);
                    dynamic data = JsonConvert.DeserializeObject(res);

                    var during = dbToken.PassTime.Value - DateTime.Now;
                    Log.Information($"Api:距离token过期{during.TotalSeconds}s");
                    // dbToken.UseTime = DateTime.Now;
                    return data;
                }
                catch (Exception ex)
                {
                    Log.Error($"Api:请求发生错误，使用时间:{dbToken.UseTime}过期时间:{dbToken.PassTime}出错{ex.Message}");
                    //当出现403 主动去获取token 重试3次
                    if (ex.Message.Contains("403") && i < 3)
                    //if (i < 3)
                    {
                        var token = await BuildToken();
                        Log.Error($"Api:403主动获取token{token}");
                        return await BuildApiRequest(json, i++);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 校验订单是否有效
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<Verification_Data> Verification(VerificationInput dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Data.RateCode))
            {
                throw new Exception("找不到报价编码");
            }
            StepDto stepDto = new StepDto()
            {
                adtSourceNum = dto.Data.AdultNum,
                childSourceNum = dto.Data.ChildNum,
                FromSegments = _mapper.Map<SearchAirticket_Segment, SegmentLineDetail>(dto.Data.FromSegments),
                RetSegments = _mapper.Map<SearchAirticket_Segment, SegmentLineDetail>(dto.Data.RetSegments),
            };
            var existModel = await GetPriceDetailByRateCode(dto.Data.RateCode, stepDto);
            if (existModel != null)
            {
                return new Verification_Data
                {
                    MaxSeats = dto.Data.AdultNum + dto.Data.ChildNum,
                    PriceDetails = existModel,
                    Success = true
                };
            }
            return new Verification_Data
            {
                Success = false,
                Message = "找不到对应的报价",
            };
        }

        /// <summary>
        /// 根据报价编码获取报价信息
        /// </summary>
        /// <param name="rateCode"></param>
        /// <returns></returns>
        public async Task<SearchAirticket_PriceDetail> GetPriceDetailByRateCode(string dbrateCode, StepDto stepDto)
        {
            var list = await GetPriceDetail(stepDto);
            return list.FirstOrDefault(n => n.RateCode == dbrateCode);
        }

        /// <summary>
        /// 下订单 (虚拟单)
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<CreateOrder_Data> CreateOrder(CreateOrderInput dto)
        {
            var flyList = dto.Data.RateCode.Split("_").ToList();
            var reRateCode = dto.Data.RateCode;
            var startArea = flyList[1];
            var endArea = flyList[2];
            var adtSourceNum = dto.Data.Passengers.Where(n => n.PassengerType == PassengerType.Adult).Count();
            var childSourceNum = dto.Data.Passengers.Where(n => n.PassengerType == PassengerType.Children).Count();
            var fromTime = Convert.ToDateTime(flyList[3]);
            var carrier = flyList[0];
            StepDto stepDto = new StepDto()
            {
                adtSourceNum = adtSourceNum,
                childSourceNum = childSourceNum,
                FromSegments = _mapper.Map<SearchAirticket_Segment, SegmentLineDetail>(dto.Data.FromSegments),
                RetSegments = _mapper.Map<SearchAirticket_Segment, SegmentLineDetail>(dto.Data.RetSegments),
            };
            try
            {
                var model = await GetPriceDetailByRateCode(reRateCode, stepDto);
                if (model != null && model.RateCode == reRateCode)
                {
                    var pnr = await CreatePnr(new pnrCreateModel { orderId = dto.Data.OrderId });
                    using var transaction = await _repository.BeginTransactionAsync();
                    NKFlightOrder order = new NKFlightOrder
                    {
                        OrderId = Guid.NewGuid().ToString(),
                        PNR = pnr, //表示虚拟单
                        PayCode = "",
                        Refer = "",
                        Currency = model.Currency,
                        BookDate = DateTime.Now,
                        SumPrice = model.AdultPrice + model.ChildPrice,
                        TaxPrice = model.AdultTax + model.ChildTax,
                        RateCode = reRateCode,
                        startArea = startArea,
                        Carrier = carrier,
                        endArea = endArea,
                        Status = 0,
                        Adult = adtSourceNum,
                        Child = childSourceNum,
                        ConcatName = dto.Data.ConcatName,
                        ConcatEmail = dto.Data.ConcatEmail,
                        ConcatPhone = dto.Data.ConcatPhone,
                        FlyDate = fromTime,
                        CTime = DateTime.Now,
                        platOrderId = dto.Data.OrderId,
                        CabinClass = stepDto.cabinClass
                    };
                    ///存入旅客信息
                    var passengers = new List<NKAirlPassenger>();
                    foreach (var peo in dto.Data.Passengers)
                    {
                        passengers.Add(new NKAirlPassenger
                        {
                            Name = peo.Name,
                            SupplierOrderId = order.OrderId,
                            AirlinePnrCode = order.PNR,
                            Gender = peo.Gender,
                            PassengerType = peo.PassengerType,
                            Birthday = peo.Birthday,
                            Nationality = peo.Nationality,
                            CredentialIssuingCountry = peo.CredentialIssuingCountry,
                            CredentialsExpired = peo.CredentialsExpired,
                            CredentialsType = peo.CredentialsType,
                            CredentialsNum = peo.CredentialsNum,
                            ctime = DateTime.Now
                        });
                    }
                    List<NKAirlSegment> segList = new List<NKAirlSegment>();
                    foreach (var item in model.FromSegments)
                    {
                        segList.Add(new NKAirlSegment
                        {
                            OrderId = order.OrderId,
                            RateCode = model.RateCode,
                            Carrier = item.Carrier,
                            Cabin = item.Cabin,
                            CabinClass = item.CabinClass,
                            FlightNumber = item.FlightNumber,
                            DepAirport = item.DepAirport,
                            ArrAirport = item.ArrAirport,
                            DepDate = item.DepDate,
                            ArrDate = item.ArrDate,
                            StopCities = item.StopCities,
                            CodeShare = item.CodeShare,
                            ShareCarrier = item.ShareCarrier,
                            ShareFlightNumber = item.ShareFlightNumber,
                            AircraftCode = item.AircraftCode,
                            Group = item.Group,
                            FareBasis = item.FareBasis,
                            GdsType = item.GdsType,
                            PosArea = item.PosArea,
                            //BaggageRule = item.BaggageRule,
                            AirlinePnrCode = item.AirlinePnrCode,
                        });
                    }
                    List<NKAirlToSegment> segToList = new List<NKAirlToSegment>();
                    foreach (var item in model.RetSegments)
                    {
                        segToList.Add(new NKAirlToSegment
                        {
                            OrderId = order.OrderId,
                            RateCode = model.RateCode,
                            Carrier = item.Carrier,
                            Cabin = item.Cabin,
                            CabinClass = item.CabinClass,
                            FlightNumber = item.FlightNumber,
                            DepAirport = item.DepAirport,
                            ArrAirport = item.ArrAirport,
                            DepDate = item.DepDate,
                            ArrDate = item.ArrDate,
                            StopCities = item.StopCities,
                            CodeShare = item.CodeShare,
                            ShareCarrier = item.ShareCarrier,
                            ShareFlightNumber = item.ShareFlightNumber,
                            AircraftCode = item.AircraftCode,
                            Group = item.Group,
                            FareBasis = item.FareBasis,
                            GdsType = item.GdsType,
                            PosArea = item.PosArea,
                            //BaggageRule = item.BaggageRule,
                            AirlinePnrCode = item.AirlinePnrCode,
                        });
                    }
                    await _repository.GetRepository<NKFlightOrder>().InsertAsync(order);
                    await _repository.GetRepository<NKAirlPassenger>().BatchInsertAsync(passengers);
                    await _repository.GetRepository<NKAirlSegment>().BatchInsertAsync(segList);
                    await _repository.GetRepository<NKAirlToSegment>().BatchInsertAsync(segToList);
                    await transaction.CommitAsync();
                    return new CreateOrder_Data
                    {
                        SupplierOrderId = order.OrderId,
                        BookingPNR = order.PNR,
                        MaxSeats = adtSourceNum + childSourceNum,
                        Currency = model.Currency,
                        PriceDetail = model,
                        Success = true
                    };
                }
                else
                {
                    Log.Error("没票了");
                    throw new Exception("no ticket");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 取消订单
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<CancelOrde_Data> CancelOrder(CancelOrderInput dto)
        {
            CancelOrde_Data res = new CancelOrde_Data()
            {
                Success = true
            };
            var _re = _repository.GetRepository<NKFlightOrder>();
            var order = await _re.FirstOrDefaultAsync(n => n.platOrderId == dto.Data.OrderId);
            if (order != null)
            {
                order.Status = OrderStatus.Canceled;
                order.UTime = DateTime.Now;
                order.CancelTime = DateTime.Now;
                await _re.UpdateAsync(order);
                res = new CancelOrde_Data
                {
                    Success = true,
                    Currency = order.Currency,
                    Amercement = 0
                };
            }
            else
            {
                res = new CancelOrde_Data
                {
                    Success = false,
                    Amercement = 0,
                    Message = "未找到订单信息"
                };
            }
            return res;
        }

        public async Task<BaggageRule> BuildBaggageRule()
        {
            return new BaggageRule
            {
                HasBaggage = false
            };
        }

        /// <summary>
        /// 订单详情
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<QueryOrder_Data> QueryOrder(QueryOrderInput dto)
        {
            //打开查询接口

            var order = await _repository.GetRepository<NKFlightOrder>().Query().Include(n => n.Segment).Include(n => n.ToSegment).Include(n => n.NKAirlPassenger).FirstOrDefaultAsync(n => n.platOrderId == dto.Data.OrderId);
            if (order == null)
            {
                return new QueryOrder_Data
                {
                    Success = false,
                    Message = "找不到对应的订单Id"
                };
            }
            List<SearchAirticket_Segment> segList = new List<SearchAirticket_Segment>();
            foreach (var se in order.Segment)
            {
                SearchAirticket_Segment seg = new SearchAirticket_Segment
                {
                    Cabin = se.Cabin,
                    Carrier = se.Carrier,
                    CabinClass = se.CabinClass,
                    FlightNumber = se.FlightNumber,
                    DepAirport = se.DepAirport,
                    ArrAirport = se.ArrAirport,
                    DepDate = se.DepDate,
                    ArrDate = se.ArrDate,
                    StopCities = se.StopCities,
                    CodeShare = se.CodeShare,
                    ShareCarrier = se.ShareCarrier,
                    ShareFlightNumber = se.ShareFlightNumber,
                    AircraftCode = se.AircraftCode,
                    Group = se.Group,
                    FareBasis = se.FareBasis,
                    GdsType = se.GdsType,
                    PosArea = se.PosArea,
                    AirlinePnrCode = se.AirlinePnrCode,
                    BaggageRule = await BuildBaggageRule()
                };
                segList.Add(seg);
            }
            List<SearchAirticket_Segment> segToList = new List<SearchAirticket_Segment>();
            foreach (var se in order.ToSegment)
            {
                SearchAirticket_Segment seg = new SearchAirticket_Segment
                {
                    Cabin = se.Cabin,
                    Carrier = se.Carrier,
                    CabinClass = se.CabinClass,
                    FlightNumber = se.FlightNumber,
                    DepAirport = se.DepAirport,
                    ArrAirport = se.ArrAirport,
                    DepDate = se.DepDate,
                    ArrDate = se.ArrDate,
                    StopCities = se.StopCities,
                    CodeShare = se.CodeShare,
                    ShareCarrier = se.ShareCarrier,
                    ShareFlightNumber = se.ShareFlightNumber,
                    AircraftCode = se.AircraftCode,
                    Group = se.Group,
                    FareBasis = se.FareBasis,
                    GdsType = se.GdsType,
                    PosArea = se.PosArea,
                    AirlinePnrCode = se.AirlinePnrCode,
                    BaggageRule = await BuildBaggageRule()
                };
                segToList.Add(seg);
            }
            List<QueryOrder_Passenger> passengers = new List<QueryOrder_Passenger>();
            foreach (var dbPeo in order.NKAirlPassenger)
            {
                QueryOrder_Passenger item = new QueryOrder_Passenger();
                item.Name = dbPeo.Name;
                item.Gender = dbPeo.Gender;
                item.PassengerType = dbPeo.PassengerType;
                item.Birthday = dbPeo.Birthday;
                item.Nationality = dbPeo.Nationality;
                item.CredentialsType = dbPeo.CredentialsType;
                item.CredentialsNum = dbPeo.CredentialsNum;
                item.CredentialsExpired = dbPeo.CredentialsExpired;
                passengers.Add(item);
            }
            return new QueryOrder_Data
            {
                Success = true,
                OrderId = dto.Data.OrderId,
                SupplierOrderId = order.OrderId,
                BookingPNR = order.PNR,
                IsTicketIssuance = false,
                SupplierOrginOrderStatus = order.Status.ToString(),
                OrderStatus = (SupplierOrderStatus)order.Status,
                Currency = order.Currency,
                TotalPrice = order.SumPrice.Value,
                Tax = order.TaxPrice.Value,
                Passengers = passengers,
                FromSegments = segList,
                RetSegments = segToList
            };
        }

        public async Task<PayVerification_Data> PayVerification(PayVerificationInput dto)
        {
            PayVerification_Data res = new PayVerification_Data();
            var order = await _repository.GetRepository<NKFlightOrder>().FirstOrDefaultAsync(n => n.platOrderId == dto.Data.OrderId);
            if (order == null)
            {
                return new PayVerification_Data
                {
                    Success = false,
                    Message = "找不到订单信息"
                };
            }
            List<SegmentLineDetail> fromList = new List<SegmentLineDetail>();
            foreach (var seg in dto.Data.FromSegments)
            {
                fromList.Add(new SegmentLineDetail
                {
                    ArrAirport = seg.ArrAirport,
                    DepAirport = seg.DepAirport,
                    DepDate = seg.DepDate.ToString("yyyy-MM-dd")
                });
            }
            List<SegmentLineDetail> tolList = new List<SegmentLineDetail>();
            foreach (var seg in dto.Data.RetSegments)
            {
                tolList.Add(new SegmentLineDetail
                {
                    ArrAirport = seg.ArrAirport,
                    DepAirport = seg.DepAirport,
                    DepDate = seg.DepDate.ToString("yyyy-MM-dd")
                });
            }
            StepDto stepDto = new StepDto()
            {
                adtSourceNum = order.Adult,
                childSourceNum = order.Child,
                FromSegments = fromList,
                RetSegments = tolList,
            };
            var model = await GetPriceDetailByRateCode(order.RateCode, stepDto);
            if (model != null && model.RateCode == model.RateCode)
            {
                res.Success = true;
                res.OrderId = dto.Data.OrderId;
                res.Currency = order.Currency.ToString();
                res.PriceDetail = model;
            }
            else
            {
                res.Success = false;
                res.Message = "该订单不可预定";
            }
            return res;
        }

        /// <summary>
        /// 调用供应商 生成假的 pnr码
        /// </summary>
        /// <returns></returns>
        public async Task<string> CreatePnr(pnrCreateModel model)
        {
            var client = await GetClientKey();
            try
            {
                var timeSpan = GetTimeStamp();
                var md5 = GetMD5WithString($"{client.AppId}{client.AccessKey}{timeSpan}");
                var appid = client.AppId;
                /*       timeSpan = "20240509174541";
                       appid = "d6167818-85af-4a23-9114-3ea3f816acc5";
                       md5 = "c12ddfac153fb7bc7819d02da977b284";*/
                OsCreatePnrRequest postdata = new OsCreatePnrRequest
                {
                    authration = new authration
                    {
                        appId = appid,
                        timespan = timeSpan,
                        token = md5
                    },
                    orderId = model.orderId,
                    bookingPNR = model.bookingPNR
                };
                var response = HttpHelper.PostAjaxData(_setting.CreatePnrUrl, JsonConvert.SerializeObject(postdata), Encoding.UTF8);
                var res = JsonConvert.DeserializeObject<OsCreatePnrResponse>(response);
                return res?.result?.bookingPNR;
            }
            catch (Exception ex)
            {
                var ss = ex.Message;
            }
            return "";
        }
    }
}