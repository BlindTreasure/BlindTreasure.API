using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.CategoryDtos
{
    public class CategoryCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

      //  [Required]
        [MaxLength(255)]
        public string Description { get; set; }
        public Guid? ParentId { get; set; }
    }
}
