using System.ComponentModel.DataAnnotations;

namespace Assesment.Models
{
    public class StudentModel
    {
        [Key]
        public int Id { get; set; }
        public required string StudentID { get; set; }  
        public required string StudentName { get; set; }
        public int? StudentAge { get; set; }
    }

}
