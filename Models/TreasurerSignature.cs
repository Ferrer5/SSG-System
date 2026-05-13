using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcApp.Models;

[Table("treasurer_signatures")]
public class TreasurerSignature
{
    [Key]
    [Column("signature_id")]
    public int SignatureId { get; set; }

    [Column("account_id")]
    public int AccountId { get; set; }

    [Column("signature_data")]
    public string SignatureData { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(AccountId))]
    public Account? Account { get; set; }
}
