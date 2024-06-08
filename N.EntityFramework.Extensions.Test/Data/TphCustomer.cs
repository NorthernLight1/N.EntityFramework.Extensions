using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions.Test.Data
{
    public class TphCustomer : TphPerson
    {
        public string Email { get; set; }
        public string Phone { get; set; }
        public DateTime AddedDate { get; set; }
    }
}