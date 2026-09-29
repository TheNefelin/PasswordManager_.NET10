using System.ComponentModel.DataAnnotations;

namespace WebApiCore.Application.DTOs;

public class CoreDataReplacement
{
    [Key]
    public Guid Data_Id { get; set; }

    [MaxLength(256)]
    public required string Data01 { get; set; }

    [MaxLength(256)]
    public required string Data02 { get; set; }

    [MaxLength(256)]
    public required string Data03 { get; set; }
}