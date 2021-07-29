using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions.Test.Data
{
    public class TphVendor : TphPerson
    {
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Url { get; set; }
    }
}
