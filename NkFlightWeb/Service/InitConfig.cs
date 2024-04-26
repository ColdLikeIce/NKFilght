using LionAir.Core.TimeSecurity;
using NkFlightWeb.Service.Dto;

namespace NkFlightWeb.Service
{
    public class InitConfig
    {
        private static readonly object _locker = new Object();
        private static InitConfig _instance = null;
        private static List<TokenUserModel> tokenList = new List<TokenUserModel>();

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