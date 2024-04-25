using CommonCore.EntityFramework.Common;
using DtaAccess.Domain.Enums;
using DtaAccess.Domain.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NkFlightWeb.Db;
using NkFlightWeb.Entity;
using NkFlightWeb.Impl;
using NkFlightWeb.Service.Dto;
using NkFlightWeb.Util;
using OpenCvSharp;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Point = OpenCvSharp.Point;

namespace NkFlightWeb.Service
{
    public class NkFlightDomain : INkFlightDomain
    {
        private readonly IBaseRepository<HeyTripDbContext> _repository;
        private readonly string nktoken = "Nktoken";
        private readonly int tokenCount = 3;

        //private readonly string url = $"https://www.spirit.com/book/flights?tripType=oneWay&bookingType=flight&from=SJC&departDate={DateTime.Now.AddDays(3).ToString("yyyy-MM-dd")}&to=BQN&returnDate=&adt=1&chd=0&inf=0";
        private readonly string url = $"https://www.spirit.com";

        public NkFlightDomain(IBaseRepository<HeyTripDbContext> repository)
        {
            _repository = repository;
        }

        private async Task<bool> JustToken()
        {
            var tokenList = InitConfig.Get_TokenList();
            //删除小于当前时间的token
            var passToken = tokenList.Where(n => n.PassTime.Value < DateTime.Now.AddSeconds(30)).ToList();
            foreach (var token in passToken)
            {
                tokenList.Remove(token);
            }
            var oldToken = InitConfig.Get_OldToken();
            var newToken = InitConfig.Get_Token();
            if (oldToken == null || newToken == null || (oldToken.PassTime < DateTime.Now.AddMinutes(5) && newToken.PassTime < DateTime.Now.AddMinutes(10)))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 获取token
        /// </summary>
        /// <returns></returns>
        public async Task<bool> GetToken_old()
        {
            var count = 1;
            //解决5分钟超时 则去重新获取

            var tokenList = InitConfig.Get_TokenList();
            var passToken = tokenList.Where(n => n.PassTime.Value < DateTime.Now.AddMinutes(5)).ToList();
            foreach (var token in passToken)
            {
                tokenList.Remove(token);
            }
            if (tokenList == null || tokenList.Count < count)
            {
                while (tokenList.Count < count || tokenList.Exists(n => n.PassTime.Value < DateTime.Now.AddMinutes(5)))
                {
                    var port = "5656";

                    using var playwright = await Playwright.CreateAsync();
                    await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });

                    var userDataDir = "D:\\work\\NK";

                    try
                    {
                        await playwright.Chromium.ConnectOverCDPAsync($"http://localhost:{port}", new BrowserTypeConnectOverCDPOptions { SlowMo = 10 });
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            //新的命令行
                            var str = $"cd/d   C:\\Program Files\\Google\\Chrome\\Application\\ && chrome.exe --remote-debugging-port={port} --user-data-dir={userDataDir}  --start-maximized --new-window https://www.spirit.com";
                            System.Diagnostics.Process p = new System.Diagnostics.Process();
                            p.StartInfo.FileName = "cmd.exe";
                            p.StartInfo.UseShellExecute = false;    //是否使用操作系统shell启动
                            p.StartInfo.RedirectStandardInput = true;//接受来自调用程序的输入信息
                            p.StartInfo.RedirectStandardOutput = true;//由调用程序获取输出信息
                            p.StartInfo.RedirectStandardError = true;//重定向标准错误输出
                            p.StartInfo.CreateNoWindow = true;//不显示程序窗口
                            p.Start();//启动程序
                            p.StandardInput.WriteLine(str + "&exit");
                            p.StandardInput.AutoFlush = true;
                            p.WaitForExit();//等待程序执行完退出进程
                            p.Close();
                            // --incognito
                        }
                        catch (Exception ex1)
                        {
                            Console.WriteLine("Error opening the browser: " + ex1.Message);
                        }
                    }
                    await using var browser = await playwright.Chromium.ConnectOverCDPAsync($"http://localhost:{port}", new BrowserTypeConnectOverCDPOptions { SlowMo = 10 });
                    var defaultContext = browser.Contexts[0];
                    var page = defaultContext.Pages[0];
                    var response = await page.GotoAsync(url);
                    /*    var defaultContext = browser.Contexts[0];
                        var page = defaultContext.Pages[0];*/
                    await Task.Delay(5000);
                    await DoRobot(page);
                    var toke = "";
                    var exLog = false;
                    try
                    {
                        page.Response += listenerResonse;
                        async void listenerResonse(object sender, IResponse request)
                        {
                            if (request.Url.Contains("token"))
                            {
                                Log.Information($"{request.Url}");
                            }
                            if (request.Url.Contains("api/availability/search"))
                            {
                                var expire = request.Headers["expires"];
                                var expireTime = DateTime.Now.AddMinutes(15);
                                var value = JsonConvert.SerializeObject(request.Request.Headers);
                                var urls = new List<string> { request.Url };
                                var cookiesList = await page.Context.CookiesAsync(urls);

                                string cookies = "";

                                foreach (var cookie in cookiesList)
                                {
                                    cookies += $"{cookie.Name}={cookie.Value};";
                                }
                                Log.Information($"{DateTime.Now}获取到token{cookiesList.Count}");
                                TokenUserModel tokenModel = new TokenUserModel
                                {
                                    PassTime = DateTime.Now.AddMinutes(15),
                                    UseTime = DateTime.Now,
                                    Headers = value,
                                    Cookies = cookies,
                                };
                                InitConfig.AddTokenList(tokenModel);
                            }
                        };
                        await page.WaitForSelectorAsync(".toStation", new PageWaitForSelectorOptions { Timeout = 6000 });
                        var cookiesBtn = await page.QuerySelectorAsync("#onetrust-accept-btn-handler");
                        if (cookiesBtn != null)
                        {
                            await cookiesBtn?.ClickAsync();
                            //await page.WaitForSelectorAsync(".close");
                        }
                        await Task.Delay(2000);
                        var closeBtn = await page.QuerySelectorAsync(".close");
                        if (closeBtn != null)
                        {
                            await closeBtn?.ClickAsync();
                        }
                        /*   var onway = await page.QuerySelectorAsync("body > app-root > main > div.container > app-home-page > section.home-widget-section.breakout-full-width.ng-star-inserted > div > div > div > div > app-home-widget > div > div.home-widget-wrapper > form > div > div > div.home-widget.fare-selection > div.left > app-nk-dropdown > div > label > div");
                           await onway.ClickAsync();
                           await page.QuerySelectorAsync("#oneWay").Result.ClickAsync();*/
                        var toStationbtn = await page.QuerySelectorAsync(".toStation");
                        await toStationbtn?.ClickAsync();
                        var rand = new Random();
                        var descBtn = await page.QuerySelectorAsync($".stationPickerDestDropdown > div > div > div.d-flex.flex-column.flex-wrap.ng-star-inserted > div:nth-child(1) > div > p");
                        await descBtn?.ClickAsync();

                        var subBtn = await page.QuerySelectorAsync(".btn-block");
                        await subBtn.ClickAsync();
                        await DoRobot(page);
                        var unBtn = await page.QuerySelectorAsync(".modal-btn");
                        if (unBtn != null)
                        {
                            await unBtn.ClickAsync();
                        }
                        await DoRobot(page);

                        await Task.Delay(10000);
                        Log.Information($"出来结果为{toke}");
                    }
                    catch (Exception ex)
                    {
                        exLog = true;
                        await DoRobot(page);
                        Log.Error($"获取token报错{ex.Message}");
                        return false;
                    }
                    finally
                    {
                        //await defaultContext.CloseAsync();
                        await page.CloseAsync();
                        await browser.CloseAsync();
                        if (exLog)
                        {
                            await Task.Delay(5000);
                            try
                            {
                                DirectoryInfo di = new DirectoryInfo($"{userDataDir.Replace("\"", "")}\\default");
                                di.Delete(true);
                                Log.Error($"删除文件");
                            }
                            catch (Exception ex2)
                            {
                                var ss = ex2;
                            }
                        }
                    }
                }
            }
            return true;
        }

        public async Task<bool> GetToken_cmd()
        {
            var count = 5;
            var tokenList = InitConfig.Get_TokenList();
            //解决5分钟超时 则去重新获取
            var userDataDir = "D:\\work\\NF";
            if (!await JustToken())
            {
                Log.Information($"尝试获取token过期时间为{tokenList.FirstOrDefault()?.PassTime}");
                var port = "8989";

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
                        psi.Arguments = $" --remote-debugging-port={port} --user-data-dir=\"{userDataDir}\"  --start-maximized  --incognito --new-window https://www.taobao.com";
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
                var response = await page.GotoAsync(url, new PageGotoOptions { Timeout = 30000 });
                var toke = "";
                var exLog = false;
                try
                {
                    page.Response += listenerResonse;
                    async void listenerResonse(object sender, IResponse request)
                    {
                        //Log.Information($"{request.Url}");
                        if (request.Url.Contains("api/availability/search"))
                        {
                            toke = "1";
                            var expire = request.Headers["expires"];
                            var expireTime = DateTime.Now.AddMinutes(15);
                            var value = JsonConvert.SerializeObject(request.Request.Headers);
                            var urls = new List<string> { request.Url };
                            var cookiesList = await page.Context.CookiesAsync(urls);
                            string cookies = "";
                            foreach (var cookie in cookiesList)
                            {
                                cookies += $"{cookie.Name}={cookie.Value};";
                            }
                            var token = request.Request.Headers["authorization"];
                            Log.Information($"{DateTime.Now}获取到token {token}");
                            TokenUserModel tokenModel = new TokenUserModel
                            {
                                PassTime = DateTime.Now.AddMinutes(15),
                                UseTime = DateTime.Now,
                                Headers = value,
                                Cookies = cookies,
                            };
                            InitConfig.AddTokenList(tokenModel);
                            if (tokenList.Count > 0 && tokenList.FirstOrDefault().PassTime <= DateTime.Now)
                            {
                                Log.Error($"Warning token已过期{tokenList.FirstOrDefault().PassTime}");
                            }
                        }
                    };
                    await page.WaitForSelectorAsync("#onetrust-accept-btn-handler", new PageWaitForSelectorOptions { Timeout = 5000 });
                    var cookiesBtn = await page.QuerySelectorAsync("#onetrust-accept-btn-handler");
                    if (cookiesBtn != null)
                    {
                        await cookiesBtn?.ClickAsync();
                    }
                    await Task.Delay(2000);
                    var closeBtn = await page.QuerySelectorAsync(".close");
                    if (closeBtn != null)
                    {
                        await closeBtn?.ClickAsync();
                    }
                    await page.WaitForSelectorAsync(".btn-block", new PageWaitForSelectorOptions { Timeout = 5000 });
                    var onway = await page.QuerySelectorAsync("body > app-root > main > div.container > app-home-page > section.home-widget-section.breakout-full-width.ng-star-inserted > div > div > div > div > app-home-widget > div > div.home-widget-wrapper > form > div > div > div.home-widget.fare-selection > div.left > app-nk-dropdown > div > label > div");
                    await onway.ClickAsync();
                    await page.QuerySelectorAsync("#oneWay").Result.ClickAsync();
                    await page.WaitForSelectorAsync(".toStation", new PageWaitForSelectorOptions { Timeout = 5000 });
                    var toStationbtn = await page.QuerySelectorAsync(".toStation");
                    await toStationbtn?.ClickAsync();
                    var rand = new Random();
                    await Task.Delay(1000);
                    var descBtn = await page.QuerySelectorAsync($".stationPickerDestDropdown > div > div > div.d-flex.flex-column.flex-wrap.ng-star-inserted > div:nth-child(1) > div > p");
                    await descBtn?.ClickAsync();

                    var subBtn = await page.QuerySelectorAsync(".btn-block");
                    await subBtn.ClickAsync();
                    var unBtn = await page.QuerySelectorAsync(".modal-btn");
                    if (unBtn != null)
                    {
                        await unBtn.ClickAsync();
                    }
                    await Task.Delay(6000);
                    if (string.IsNullOrWhiteSpace(toke))
                    {
                        exLog = true;
                    }
                    Log.Information($"出来结果为{toke}");
                    return true;
                }
                catch (Exception ex)
                {
                    exLog = true;

                    Log.Error($"获取token报错{ex.Message}");
                    await DoRobot(page);

                    return false;
                }
                finally
                {
                    //await defaultContext.CloseAsync();
                    await page.CloseAsync();
                    await browser.CloseAsync();
                    if (exLog)
                    {
                        await Task.Delay(5000);
                        try
                        {
                            DirectoryInfo di = new DirectoryInfo($"{userDataDir.Replace("\"", "")}\\default");
                            di.Delete(true);
                        }
                        catch (Exception ex2)
                        {
                            var ss = ex2;
                        }
                    }
                }
            }
            return true;
        }

        public async Task<bool> GetToken()
        {
            var userDataDir = "D:\\work\\NF";

            if (!await JustToken())
            {
                using var playwright = await Playwright.CreateAsync();

                var args = new List<string>() { "--start-maximized", "--disable-blink-features=AutomationControlled" };
                /*
                                await using var browser = await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                                { Headless = false, Args = args.ToArray(), ViewportSize = ViewportSize.NoViewport, BypassCSP = true, SlowMo = 10, });*/
                var chromiunPath = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
                await using var browser = await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                { Headless = false, Args = args.ToArray(), ViewportSize = ViewportSize.NoViewport, BypassCSP = true, SlowMo = 10, Devtools = false, ExecutablePath = chromiunPath });
                var token = "";
                var page = browser.Pages[0];
                var response = await page.GotoAsync(url, new PageGotoOptions { Timeout = 200000 });
                var exLog = false;
                try
                {
                    page.Response += listenerResonse;
                    async void listenerResonse(object sender, IResponse request)
                    {
                        Log.Information($"{request.Url}");
                        if (request.Url.Contains("token"))
                        {
                            Log.Information($"{request.Url}");
                        }
                        if (request.Url.Contains("api/availability/search"))
                        {
                            var expire = request.Headers["expires"];
                            var expireTime = DateTime.Now.AddMinutes(15);
                            var value = JsonConvert.SerializeObject(request.Request.Headers);
                            var urls = new List<string> { request.Url };
                            var cookiesList = await page.Context.CookiesAsync(urls);
                            string cookies = "";

                            foreach (var cookie in cookiesList)
                            {
                                cookies += $"{cookie.Name}={cookie.Value};";
                            }
                            token = request.Request.Headers["authorization"];
                            TokenUserModel tokenModel = new TokenUserModel
                            {
                                PassTime = DateTime.Now.AddMinutes(15),
                                UseTime = DateTime.Now,
                                Headers = value,
                                Cookies = cookies,
                            };
                            InitConfig.AddTokenList(tokenModel);

                            Log.Information($"{DateTime.Now} tokenCount【 {InitConfig.Get_TokenList().Count}】  获取到token {token}");
                        }
                    };
                    await page.WaitForSelectorAsync(".toStation", new PageWaitForSelectorOptions { Timeout = 5000 });
                    var cookiesBtn = await page.QuerySelectorAsync("#onetrust-accept-btn-handler");
                    if (cookiesBtn != null)
                    {
                        await cookiesBtn?.ClickAsync();
                        //await page.WaitForSelectorAsync(".close");
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

                    await Task.Delay(6000);
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        await DoRobot(page);
                    }
                    Log.Information($"出来结果为{token}");
                    return true;
                }
                catch (Exception ex)
                {
                    exLog = true;

                    Log.Error($"获取token报错{ex.Message}");
                    await DoRobot(page);

                    return false;
                }
                finally
                {
                    //await defaultContext.CloseAsync();

                    await page.CloseAsync();
                    /*  if (exLog)
                      {
                          await Task.Delay(5000);
                          try
                          {
                              DirectoryInfo di = new DirectoryInfo($"{userDataDir.Replace("\"", "")}\\default");
                              di.Delete(true);
                          }
                          catch (Exception ex2)
                          {
                              var ss = ex2;
                          }
                      }*/
                }
            }

            return true;
        }

        public async Task DoRobot(IPage page)
        {
            try
            {
                var tip = await page.QuerySelectorAsync("#px-captcha-modal");
                var dia = await page.QuerySelectorAsync("#px-captcha");
                var title = await page.TitleAsync();
                if (tip != null || dia != null || title.Contains("Access"))
                {
                    var srcPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cf.jpg");
                    await page.ScreenshotAsync(new()
                    {
                        Path = srcPath,
                    });
                    var checkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rootbot.jpg");
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
                    var clickX = matchLoc.X + 150;
                    var clickY = matchLoc.Y + 50;
                    Log.Information($"点击{clickX}【{clickY}】");
                    //Task.Delay(1000000000).Wait();
                    //Cv2.Rectangle(mask, matchLoc, new Point(clickX, clickY), Scalar.Green, 2);
                    var sleepTime = 2000;

                    await page.Mouse.MoveAsync(clickX, clickY);
                    await page.Mouse.DownAsync();

                    Task.Delay(100000).Wait();
                    await page.Mouse.UpAsync();
                    /*while (sleepTime < 10000)
                    {
                        await page.Mouse.MoveAsync(clickX, clickY);
                        await page.Mouse.DownAsync();
                        Task.Delay(sleepTime).Wait();
                        await page.Mouse.UpAsync();
                        Task.Delay(3000).Wait();
                        sleepTime += 500;
                    }*/
                    //await page.Mouse.ClickAsync(clickX, clickY, new MouseClickOptions { Delay = 15000 });
                    Task.Delay(10000).Wait();
                }
            }
            catch (Exception ex)
            {
                Log.Information($"处理机器人报错{ex.Message}");
            }
        }

        /// <summary>
        /// 构建城市
        /// </summary>
        /// <returns></returns>

        public async Task BuildCity()
        {
            var citys = await _repository.GetRepository<NkToAirlCity>().Query().ToListAsync();
            if (citys.Count == 0)
            {
                var port = "8989";

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
                        psi.Arguments = $" --remote-debugging-port={port} --user-data-dir=\"C:\\work\\chrome\"  --start-maximized  --incognito --new-window https://www.taobao.com";
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
                try
                {
                    var response = await page.GotoAsync(url, new PageGotoOptions { Timeout = 300000 });
                    await page.WaitForSelectorAsync("#onetrust-accept-btn-handler", new PageWaitForSelectorOptions { Timeout = 60000 });
                    var cookiesBtn = await page.QuerySelectorAsync("#onetrust-accept-btn-handler");
                    await cookiesBtn?.ClickAsync();
                    await page.WaitForSelectorAsync(".close");
                    var closeBtn = await page.QuerySelectorAsync(".close");
                    await closeBtn?.ClickAsync();

                    var onway = await page.QuerySelectorAsync("body > app-root > main > div.container > app-home-page > section.home-widget-section.breakout-full-width.ng-star-inserted > div > div > div > div > app-home-widget > div > div.home-widget-wrapper > form > div > div > div.home-widget.fare-selection > div.left > app-nk-dropdown > div > label > div");
                    await onway.ClickAsync();
                    await page.QuerySelectorAsync("#oneWay").Result.ClickAsync();

                    var fromBtn = await page.QuerySelectorAsync(".fromStation");
                    await fromBtn?.ClickAsync();
                    await page.WaitForSelectorAsync(".toStation");
                    var fromLocation = await page.QuerySelectorAllAsync("#widget > div.home-widget.ng-tns-c170-3 > div > div.home-widget.city-selection.ng-tns-c170-3.ng-star-inserted > div.city-selection.station-list.station-list-picker-origin.ng-tns-c170-3 > app-station-picker-dropdown > div > div > div.d-flex.flex-column.flex-wrap.ng-star-inserted >div");
                    foreach (var item in fromLocation)
                    {
                        List<NkToAirlCity> toCity = new List<NkToAirlCity>();

                        await fromBtn?.ClickAsync();

                        await page.WaitForSelectorAsync(".toStation");
                        await item.ClickAsync();
                        var p = await item.QuerySelectorAsync("p");
                        var cityText = await p.InnerHTMLAsync();
                        var h4 = await item.QuerySelectorAsync("h4");
                        var h5 = await h4.InnerHTMLAsync();
                        //需要特殊处理
                        var fromcityText = cityText.Split(",").FirstOrDefault().Trim();
                        var searchcity = h5.Trim();
                        if (h5.Contains("Airports"))
                        {
                            searchcity = await GetShortCity(fromcityText);
                            Log.Error($"需要特殊处理{cityText}");
                        }
                        var toStationbtn = await page.QuerySelectorAsync(".toStation");
                        await toStationbtn?.ClickAsync();
                        var descLocation = await page.QuerySelectorAllAsync(".stationPickerDestDropdown > div > div > div.d-flex.flex-column.flex-wrap.ng-star-inserted > div");
                        foreach (var toItem in descLocation)
                        {
                            var to_p = await toItem.QuerySelectorAsync("p");
                            var to_cityText = await to_p.InnerHTMLAsync();
                            var to_h4 = await toItem.QuerySelectorAsync("h4");
                            var to_h5 = await to_h4.InnerHTMLAsync();
                            //需要特殊处理
                            NkToAirlCity tocity = new NkToAirlCity()
                            {
                                city = to_cityText.Split(",").FirstOrDefault().Trim(),
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
                        await _repository.GetRepository<NkToAirlCity>().BatchInsertAsync(toCity);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"获取城市报错{ex.Message}");
                }
                finally
                {
                    await page.CloseAsync();
                    await defaultContext.CloseAsync();
                    await browser.CloseAsync();
                }
            }
        }

        public async Task<bool> PushAllFlightToDb(SearchDayDto dto)
        {
            var AllTo = await _repository.GetRepository<NkToAirlCity>().QueryListAsync();
            var date = DateTime.Now.AddDays(dto.day.Value);
            var journeyRe = _repository.GetRepository<NKJourney>();
            var segmentRe = _repository.GetRepository<NKSegment>();
            Stopwatch stopwatch = Stopwatch.StartNew();
            foreach (var tocity in AllTo)
            {
                try
                {
                    stopwatch.Restart();
                    StepDto stepDto = new StepDto()
                    {
                        startArea = tocity.fromcity,
                        endArea = tocity.city,
                        adtSourceNum = dto.AtdNum,
                        childSourceNum = dto.ChildNum,
                        fromTime = date
                    };
                    var resList = await StepSearch(stepDto);
                    if (resList.Count > 0)
                    {
                        using var transaction = await _repository.BeginTransactionAsync();
                        NKJourney lionAirlJourney = new NKJourney
                        {
                            JourneyId = Guid.NewGuid(),
                            TripType = (int)FlightSegmentType.OneWay,
                            DepCity = tocity.fromcity,
                            ArrCity = tocity.city,
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
                        await segmentRe.BatchDeleteAsync(dbRegList);
                        await journeyRe.InsertAsync(lionAirlJourney);
                        await segmentRe.BatchInsertAsync(segments);
                        await transaction.CommitAsync();
                        stopwatch.Stop();
                        Log.Information($"{tocity.fromcity}_{tocity.city}_{date}抓取到数据{resList.Count}条报价数据耗时{stopwatch.ElapsedMilliseconds}ms");
                        await Task.Delay(2000);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"已执行{AllTo.IndexOf(tocity)}条数据");
                    Log.Error($"{tocity.fromcity}_{tocity.city}_{date}获取失败 {ex.Message}");
                }
            }
            return true;
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
            SearchAirtickets_Data searchAirtickets_Data = new SearchAirtickets_Data() { Success = true };
            List<SearchAirticket_PriceDetail> res = new List<SearchAirticket_PriceDetail>();
            foreach (var item in dto.Data.FromSegments)
            {
                StepDto stepDto = new StepDto()
                {
                    startArea = item.DepCityCode,
                    endArea = item.ArrCityCode,
                    adtSourceNum = dto.Data.AdultNum,
                    childSourceNum = dto.Data.ChildNum,
                    fromTime = Convert.ToDateTime(item.DepDate),
                    cabinClass = dto.Data.CabinClass,
                    FlightNumber = dto.Data.FlightNumber,
                    carrier = dto.Data.Carrier,
                    Cabin = dto.Data.Cabin
                };
                var seg = await StepSearch(stepDto);
                res.AddRange(seg);
            }
            searchAirtickets_Data.PriceDetails = res;
            return searchAirtickets_Data;
        }

        /// <summary>
        /// 搜价格接口
        /// </summary>
        /// <param name="stepDto"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<List<SearchAirticket_PriceDetail>> StepSearch(StepDto stepDto)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            stopwatch.Start();
            var adtNum = stepDto.adtSourceNum.Value;
            var childNum = stepDto.childSourceNum.Value;
            var tokenList = InitConfig.Get_TokenList();
            var fromDate = stepDto.fromTime.Value.ToString("yyyy-MM-dd");
            var dbToken = InitConfig.Get_Token();
            var header = JsonConvert.DeserializeObject<Dictionary<string, string>>(dbToken.Headers);
            var newHeader = new Dictionary<string, string>();
            List<string> containHeader = new List<string>
              {
                  "authorization",
                  "user-agent",
                  "ocp-apim-subscription-key",
                  "content-type",
                  "referer",
                  "origin"
              };
            foreach (var h in header)
            {
                if (containHeader.Contains(h.Key))
                {
                    newHeader.Add(h.Key, h.Value);
                }
            }
            newHeader.Add("origin", "https://www.spirit.com");
            //newHeader.Add("Accept-Encoding", "deflate");

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
            var tocity = _repository.GetRepository<NkToAirlCity>().Query().FirstOrDefault(n => n.city.ToLower() == stepDto.endArea.ToLower() && n.fromcity.ToLower() == stepDto.startArea);
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
            var apiUrl = "https://www.spirit.com/api/prod-availability/api/availability/search";
            var ss4 = JsonConvert.SerializeObject(newHeader);
            var res = HttpHelper.HttpPostRetry(apiUrl, json, "application/json", retry: 10, timeOut: 5, headers: newHeader, cookie: dbToken.Cookies);
            // var res = HttpHelper.HttpOriginPost(apiUrl, json, header, dbToken.Cookies);
            dynamic data = JsonConvert.DeserializeObject(res);

            try
            {
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
                            decimal adultPrice = 0;
                            decimal taxPrice = 0;

                            foreach (var fare in faresList)
                            {
                                if (q > 1)
                                {
                                    break;
                                }
                                q++;
                                decimal.TryParse(fare.Value.details.passengerFares[0].fareAmount.ToString(), out adultPrice);
                                decimal.TryParse(fare.Value.details.passengerFares[0].serviceCharges[1].amount.ToString(), out taxPrice);
                            }
                            model.AdultPrice = adultPrice * adtNum;
                            model.AdultTax = taxPrice * adtNum;
                            model.ChildPrice = adultPrice * childNum;
                            model.ChildTax = taxPrice * childNum;
                            model.NationalityType = NationalityApplicableType.All;
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
                            var rateCode = $"NK_{stepDto.startArea}_{stepDto.endArea}_{fromDate}";
                            List<SearchAirticket_Segment> segList = new List<SearchAirticket_Segment>();
                            foreach (var segment in journey.segments)
                            {
                                var eq = segment.legs[0].legInfo.equipmentType;
                                SearchAirticket_Segment seg = new SearchAirticket_Segment()
                                {
                                    Carrier = segment.identifier.carrierCode,
                                    CabinClass = CabinClass.EconomyClass,
                                    FlightNumber = segment.identifier.identifier,
                                    DepAirport = segment.designator.origin,
                                    ArrAirport = segment.designator.destination,
                                    DepDate = Convert.ToDateTime(segment.designator.departure).ToString("yyyy-MM-dd HH:mm"),
                                    ArrDate = Convert.ToDateTime(segment.designator.departure).ToString("yyyy-MM-dd HH:mm"),
                                    StopCities = segment.designator.destination,
                                    CodeShare = segment.identifier.carrierCode == "NK" ? false : true,
                                    ShareCarrier = segment.identifier.carrierCode == "NK" ? "" : segment.identifier.carrierCode,
                                    ShareFlightNumber = segment.identifier.carrierCode == "NK" ? "" : segment.identifier.identifier,
                                    AircraftCode = eq
                                };
                                segList.Add(seg);
                                rateCode += $"_{seg.Carrier}{seg.FlightNumber}_{Convert.ToDateTime(seg.DepDate).ToString("yyyyMMddHHmm")}";
                            }
                            model.FromSegments = segList;
                            model.RateCode = $"{rateCode}_{adtNum}_{childNum}";
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
                Log.Error($"result {res}");
                tokenList.Remove(dbToken);
            }

            stopwatch.Stop();
            Log.Information($"获取{priceList.Count}条数据耗时{stopwatch.ElapsedMilliseconds}ms");
            return priceList;
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
            var flyList = dto.Data.RateCode.Split("_").ToList();
            var startArea = flyList[1];
            var endArea = flyList[2];
            var date = flyList[3];
            StepDto stepDto = new StepDto()
            {
                fromTime = Convert.ToDateTime(date),
                adtSourceNum = dto.Data.AdultNum,
                childSourceNum = dto.Data.ChildNum,
                startArea = startArea,
                endArea = endArea,
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
            var list = await StepSearch(stepDto);
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
                fromTime = fromTime,
                adtSourceNum = adtSourceNum,
                childSourceNum = childSourceNum,
                startArea = startArea,
                endArea = endArea,
                carrier = carrier,
            };
            try
            {
                var model = await GetPriceDetailByRateCode(reRateCode, stepDto);
                if (model != null && model.RateCode == reRateCode)
                {
                    using var transaction = await _repository.BeginTransactionAsync();
                    NKFlightOrder order = new NKFlightOrder
                    {
                        OrderId = Guid.NewGuid().ToString(),
                        PNR = "", //表示虚拟单
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
                    await _repository.GetRepository<NKFlightOrder>().InsertAsync(order);
                    await _repository.GetRepository<NKAirlPassenger>().BatchInsertAsync(passengers);
                    await _repository.GetRepository<NKAirlSegment>().BatchInsertAsync(segList);
                    await transaction.CommitAsync();
                    return new CreateOrder_Data
                    {
                        SupplierOrderId = order.OrderId.ToString(),
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

        /// <summary>
        /// 订单详情
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<QueryOrder_Data> QueryOrder(QueryOrderInput dto)
        {
            //打开查询接口

            var order = await _repository.GetRepository<NKFlightOrder>().Query().Include(n => n.Segment).Include(n => n.NKAirlPassenger).FirstOrDefaultAsync(n => n.platOrderId == dto.Data.OrderId);
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
                    //BaggageRule = await BuildBaggageRule(se.Carrier)
                };
                segList.Add(seg);
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
            if (string.IsNullOrWhiteSpace(order.PNR) || order.PNR.Contains("NA_"))
            {
                //虚拟单
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
                    FromSegments = segList
                };
            }
            return new QueryOrder_Data
            {
                Success = false
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

            StepDto stepDto = new StepDto
            {
                adtSourceNum = order.Adult,
                childSourceNum = order.Child,
                carrier = order.Carrier,
                startArea = order.startArea,
                endArea = order.endArea,
                fromTime = order.FlyDate,
                cabinClass = order.CabinClass
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
    }
}