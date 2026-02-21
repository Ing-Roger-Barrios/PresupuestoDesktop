using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresupuestoPro.Auth.Models
{
    public class LoginResponse
    {
        public string Message { get; set; }
        public string AccessToken { get; set; }
        public string TokenType { get; set; }
        public User User { get; set; }
    }
}
