using LionAir.Core.TimeSecurity;
using NkFlightWeb.Service.Dto;
using Serilog;

namespace NkFlightWeb.Service
{
    public class InitConfig
    {
        private static readonly object _locker = new Object();
        private static InitConfig _instance = null;
        private static List<TokenUserModel> tokenList = new List<TokenUserModel>();
        private static int ClickX = 0;
        private static int ClickY = 0;
        private static bool isFirst = true;

        public static void setFirst()
        {
            isFirst = false;
        }

        public static bool GetFirst()
        {
            return isFirst;
        }

        public static int GetClickX()
        {
            return ClickX;
        }

        public static void SetClickX(int clickX, int clickY)
        {
            ClickX = clickX;
            ClickY = clickY;
        }

        public static int GetClickY()
        {
            return ClickY;
        }

        /// <summary>
        /// 单例
        /// </summary>
        public static InitConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_locker)
                    {
                        if (_instance == null)
                        {
                            _instance = new InitConfig();
                        }
                    }
                }
                return _instance;
            }
        }

        public static void AddTokenList(TokenUserModel token)
        {
            Log.Information($"rootbot:替换token【{token.PassTime}】【{tokenList.FirstOrDefault()?.PassTime}】");
            tokenList = new List<TokenUserModel>();
            tokenList.Add(token);
        }

        /// <summary>
        /// 获取订单创建外呼任务配置
        /// </summary>
        /// <returns></returns>
        public static List<TokenUserModel> Get_TokenList()
        {
            return tokenList;
        }

        /// <summary>
        /// 获取最新的passtime
        /// </summary>
        /// <returns></returns>
        public static TokenUserModel Get_Token()
        {
            return tokenList.Where(n => n.PassTime.Value > DateTime.Now).OrderBy(n => n.UseTime).FirstOrDefault();
        }

        /// <summary>
        /// 获取最老的passtime
        /// </summary>
        /// <returns></returns>
        public static TokenUserModel Get_OldToken()
        {
            return tokenList.OrderBy(n => n.PassTime).FirstOrDefault();
        }
    }
}