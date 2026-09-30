using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BJS.Master.Models
{
    public class SmtpModel
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string From { get; set; }
        public string FromAlias { get; set; }
        public string To { get; set; }
        public string ToAlias { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Subject { get; set; }
        public bool Secure { get; set; }
    }
}
