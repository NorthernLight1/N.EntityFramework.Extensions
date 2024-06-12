using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N.EntityFramework.Extensions.Test.Data;

[Table("TphPeople")]
public abstract class TphPerson
{
    public long Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}