using System;

namespace ToodledoConsole
{
    public class TokenStorage
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }

    public class TokenResponse 
    { 
        public string access_token { get; set; } 
        public string refresh_token { get; set; } 
    }

    public class ToodledoTask 
    { 
        public string id { get; set; } 
        public string title { get; set; } 
    }
}
