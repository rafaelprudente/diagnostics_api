using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace doctors_api.Model
{
    [Table("Doctors")]
    public class Doctor
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int? Crm { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public DateTime? Subscription { get; set; }
        public DateTime? Inactivation { get; set; }
        public string? City { get; set; }
        public string? Uf { get; set; }
        public string? Specialties { get; set; }
    }
}
