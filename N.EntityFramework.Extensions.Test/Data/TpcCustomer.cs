using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions.Test.Data;

public class TpcCustomer : TpcPerson
{
    public string Email { get; set; }
    public string Phone { get; set; }
    public DateTime AddedDate { get; set; }
}