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
using Qunar.Airtickets.Supplier.Concat.Dtos.Input;
using Qunar.Airtickets.Supplier.Concat.Dtos.Models;
using Qunar.Airtickets.Supplier.Concat.Dtos.Output;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading;
using Point = OpenCvSharp.Point;

namespace NkFlightWeb.Service
{
    public class NkFlightDomain : INkFlightDomain
    {
        private readonly IBaseRepository<HeyTripDbContext> _repository;
        private readonly string nktoken = "Nktoken";
        private readonly string url = "https://www.spirit.com";

        public NkFlightDomain(IBaseRepository<HeyTripDbContext> repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// 获取token
        /// </summary>
        /// <returns></returns>
        public async Task GetToken()
        {
            await BuildCity();
            var tokenList = InitConfig.Get_TokenList();
            //解决5分钟超时 则去重新获取
            var passToken = tokenList.Where(n => n.PassTime.Value < DateTime.Now.AddMinutes(5)).ToList();
            foreach (var token in passToken)
            {
                tokenList.Remove(token);
            }
            if (tokenList == null || tokenList.Count < 5)
            {
                while (tokenList.Count < 5 || tokenList.Exists(n => n.PassTime.Value < DateTime.Now.AddMinutes(5)))
                {
                    var port = "6666";

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

                    var title = await page.TitleAsync();
                    if (title.Contains("Access"))
                    {
                        await DoRobot(page);
                    }
                    /*  await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
                      var page = await browser.NewPageAsync();*/
                    var toke = "";
                    var exLog = false;
                    try
                    {
                        page.Response += listenerResonse;
                        async void listenerResonse(object sender, IResponse request)
                        {
                            if (request.Url.Contains("api/availability/search"))
                            {
                                toke = "1";
                                var expire = request.Headers["expires"];
                                var expireTime = DateTime.Now.AddMinutes(15);
                                var value = JsonConvert.SerializeObject(request.Request.Headers);
                                var urls = new List<string> { request.Url };
                                var cookiesList = await page.Context.CookiesAsync(urls);

                                var cookie = cookiesList.FirstOrDefault(n => n.Name == "tokenData");
                                var cookies = $"{cookie.Name}={cookie.Value};";
                                /* foreach (var cookie in cookiesList)
                                 {
                                     cookies += $"{cookie.Name}={cookie.Value};";
                                 }*/
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
                        //var response = await page.GotoAsync(url, new PageGotoOptions { Timeout = 300000 });
                        await page.WaitForSelectorAsync(".toStation", new PageWaitForSelectorOptions { Timeout = 10000 });
                        var cookiesBtn = await page.QuerySelectorAsync("#onetrust-accept-btn-handler");
                        if (cookiesBtn != null)
                        {
                            await cookiesBtn?.ClickAsync();
                            await page.WaitForSelectorAsync(".close");
                            var closeBtn = await page.QuerySelectorAsync(".close");
                            await closeBtn?.ClickAsync();
                            await page.WaitForSelectorAsync(".toStation");
                        }
                        var toStationbtn = await page.QuerySelectorAsync(".toStation");
                        await toStationbtn?.ClickAsync();
                        var rand = new Random();
                        var randIndex = rand.Next(50);
                        var descBtn = await page.QuerySelectorAsync($".stationPickerDestDropdown > div > div > div.d-flex.flex-column.flex-wrap.ng-star-inserted > div:nth-child({randIndex}) > div > p");
                        await descBtn?.ClickAsync();
                        await Task.Delay(1000);
                        var subBtn = await page.QuerySelectorAsync(".btn-block");
                        await subBtn.ClickAsync();
                        await Task.Delay(5000);
                        Log.Information($"出来结果为{toke}");
                    }
                    catch (Exception ex)
                    {
                        exLog = true;
                        await DoRobot(page);
                        Log.Error($"获取token报错{ex.Message}");
                    }
                    finally
                    {
                        //await defaultContext.CloseAsync();
                        await page.CloseAsync();
                        await browser.CloseAsync();
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
            }
        }

        public async Task GetToken_new()
        {
            await BuildCity();
            var tokenList = InitConfig.Get_TokenList();
            var userDataDir = "D:\\work\\NF";
            //解决5分钟超时 则去重新获取
            if (tokenList == null || tokenList.Exists(n => n.PassTime.Value < DateTime.Now.AddMinutes(5)) || tokenList.Count < 5)
            {
                using var playwright = await Playwright.CreateAsync();

                var args = new List<string>() { "--start-maximized", "--disable-blink-features=AutomationControlled" };
                await using var browser = await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                { Headless = false, Args = args.ToArray(), ViewportSize = ViewportSize.NoViewport, BypassCSP = true, SlowMo = 10, });
                var page = await browser.NewPageAsync();
                await page.GotoAsync(url);
                var toke = "";
                var exLog = false;
                try
                {
                    page.Response += listenerResonse;
                    async void listenerResonse(object sender, IResponse request)
                    {
                        if (request.Url.Contains("api/availability/search"))
                        {
                            toke = "1";
                            var expire = request.Headers["expires"];
                            var expireTime = DateTime.Now.AddMinutes(15);
                            var value = JsonConvert.SerializeObject(request.Request.Headers);
                            var urls = new List<string> { request.Url };
                            var cookiesList = await page.Context.CookiesAsync(urls);

                            var cookie = cookiesList.FirstOrDefault(n => n.Name == "tokenData");
                            var cookies = $"{cookie.Name}={cookie.Value};";
                            Log.Information($"{DateTime.Now}获取到token{cookiesList.Count}");
                            //todo
                        }
                    };
                    //var response = await page.GotoAsync(url, new PageGotoOptions { Timeout = 300000 });
                    await page.WaitForSelectorAsync(".toStation", new PageWaitForSelectorOptions { Timeout = 10000 });
                    var title = await page.TitleAsync();
                    var cookiesBtn = await page.QuerySelectorAsync("#onetrust-accept-btn-handler");
                    if (cookiesBtn != null)
                    {
                        await cookiesBtn?.ClickAsync();
                        await page.WaitForSelectorAsync(".close");
                        var closeBtn = await page.QuerySelectorAsync(".close");
                        await closeBtn?.ClickAsync();
                        await page.WaitForSelectorAsync(".toStation");
                    }
                    //await page.Mouse.MoveAsync(0, 0);
                    var toStationbtn = await page.QuerySelectorAsync(".toStation");
                    //await page.Mouse.MoveAsync(0, 100);
                    await toStationbtn?.ClickAsync();
                    var rand = new Random();
                    var randIndex = rand.Next(50);
                    var descBtn = await page.QuerySelectorAsync($".stationPickerDestDropdown > div > div > div.d-flex.flex-column.flex-wrap.ng-star-inserted > div:nth-child({randIndex}) > div > p");
                    await descBtn?.ClickAsync();
                    await Task.Delay(1000);
                    var subBtn = await page.QuerySelectorAsync(".btn-block");
                    await subBtn.ClickAsync();
                    await Task.Delay(5000);
                    Log.Information($"出来结果为{toke}");
                }
                catch (Exception ex)
                {
                    await DoRobot(page);
                    exLog = true;
                    Log.Error($"获取token报错{ex.Message}");
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
        }

        public async Task DoRobot(IPage page)
        {
            try
            {
                Task.Delay(3000).Wait();
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
                var clickX = matchLoc.X + 130;
                var clickY = matchLoc.Y + 50;
                Cv2.Rectangle(mask, matchLoc, new Point(clickX, clickY), Scalar.Green, 2);

                await page.Mouse.MoveAsync(clickX, clickY);
                await page.Mouse.DownAsync();
                Task.Delay(10000).Wait();
                await page.Mouse.UpAsync();
                Task.Delay(15000).Wait();
            }
            catch (Exception ex)
            {
                var e = ex;
            }
        }

        /// <summary>
        /// 构建城市
        /// </summary>
        /// <returns></returns>

        private async Task BuildCity()
        {
            var citys = await _repository.GetRepository<NkFromAirlCity>().Query().ToListAsync();
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
                List<NkFromAirlCity> fromCity = new List<NkFromAirlCity>();
                List<NkToAirlCity> toCity = new List<NkToAirlCity>();
                try
                {
                    var response = await page.GotoAsync(url, new PageGotoOptions { Timeout = 300000 });
                    await page.WaitForSelectorAsync("#onetrust-accept-btn-handler", new PageWaitForSelectorOptions { Timeout = 60000 });
                    var cookiesBtn = await page.QuerySelectorAsync("#onetrust-accept-btn-handler");
                    await cookiesBtn?.ClickAsync();
                    await page.WaitForSelectorAsync(".close");
                    var closeBtn = await page.QuerySelectorAsync(".close");
                    await closeBtn?.ClickAsync();

                    await page.WaitForSelectorAsync(".toStation");
                    var fromBtn = await page.QuerySelectorAsync(".fromStation");
                    await fromBtn?.ClickAsync();
                    var fromLocation = await page.QuerySelectorAllAsync("#widget > div.home-widget.ng-tns-c170-3 > div > div.home-widget.city-selection.ng-tns-c170-3.ng-star-inserted > div.city-selection.station-list.station-list-picker-origin.ng-tns-c170-3 > app-station-picker-dropdown > div > div > div.d-flex.flex-column.flex-wrap.ng-star-inserted >div");
                    foreach (var item in fromLocation)
                    {
                        var p = await item.QuerySelectorAsync("p");
                        var cityText = await p.InnerHTMLAsync();
                        var h4 = await item.QuerySelectorAsync("h4");
                        var h5 = await h4.InnerHTMLAsync();
                        //需要特殊处理
                        NkFromAirlCity city = new NkFromAirlCity()
                        {
                            city = cityText.Split(",").FirstOrDefault().Trim(),
                            searchcity = h5.Trim()
                        };
                        if (h5.Contains("Airports"))
                        {
                            city.searchcity = await GetShortCity(city.city);
                            Log.Error($"需要特殊处理{cityText}");
                        }
                        fromCity.Add(city);
                    }
                    var toStationbtn = await page.QuerySelectorAsync(".toStation");
                    await toStationbtn?.ClickAsync();
                    var descLocation = await page.QuerySelectorAllAsync(".stationPickerDestDropdown > div > div > div.d-flex.flex-column.flex-wrap.ng-star-inserted > div");
                    foreach (var item in descLocation)
                    {
                        var p = await item.QuerySelectorAsync("p");
                        var cityText = await p.InnerHTMLAsync();
                        var h4 = await item.QuerySelectorAsync("h4");
                        var h5 = await h4.InnerHTMLAsync();
                        //需要特殊处理
                        NkToAirlCity city = new NkToAirlCity()
                        {
                            city = cityText.Split(",").FirstOrDefault().Trim(),
                            searchcity = h5.Trim()
                        };
                        if (h5.Contains("Airports"))
                        {
                            city.searchcity = await GetShortCity(city.city);
                        }
                        toCity.Add(city);
                    }
                    await _repository.GetRepository<NkFromAirlCity>().BatchInsertAsync(fromCity);
                    await _repository.GetRepository<NkToAirlCity>().BatchInsertAsync(toCity);
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

        public async Task<List<SearchAirticket_PriceDetail>> StepSearch(StepDto stepDto)
        {
            var adtNum = stepDto.adtSourceNum.Value;
            var childNum = stepDto.childSourceNum.Value;
            var tokenList = InitConfig.Get_TokenList();

            var dbToken = tokenList.OrderByDescending(n => n.UseTime).FirstOrDefault();
            var header = JsonConvert.DeserializeObject<Dictionary<string, string>>(dbToken.Headers);

            header.Add("origin", "https://www.spirit.com");
            header.Add("sec-fetch-dest", "empty");
            header.Add("sec-fetch-mode", "cors");
            header.Add("sec-fetch-site", "same-origin");
            header.Add("Connection", "keep-alive");
            // header.Add("referer", "https://www.spirit.com/book/flights");
            //header.Add("Host", "https://www.spirit.com");
            var s = JsonConvert.SerializeObject(header);
            var qq = JsonConvert.SerializeObject(tokenList);
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
            var fromcity = _repository.GetRepository<NkFromAirlCity>().Query().FirstOrDefault(n => n.city.ToLower() == stepDto.startArea.ToLower());
            var tocity = _repository.GetRepository<NkToAirlCity>().Query().FirstOrDefault(n => n.city.ToLower() == stepDto.endArea.ToLower());
            if (fromcity == null || tocity == null)
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
                originStationCodes = new List<string> { fromcity.searchcity },
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
            var res = HttpHelper.HttpPostRetry(apiUrl, json, "application/json", retry: 10, timeOut: 3, headers: header, cookie: dbToken.Cookies);
            dynamic data = JsonConvert.DeserializeObject(res);
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
                        var rateCode = $"NK_{stepDto.startArea}_{stepDto.endArea}_{stepDto.fromTime}";
                        List<SearchAirticket_Segment> segList = new List<SearchAirticket_Segment>();
                        foreach (var segment in journey.segments)
                        {
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
                                AircraftCode = "A321", //机型？
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
            return priceList;
        }
    }
}