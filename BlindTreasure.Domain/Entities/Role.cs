namespace BlindTreasure.Domain.Entities;

public class Role : BaseEntity
{
    public string Name { get; set; }
    public string Description { get; set; }

    // 1-n: Role → Users
    public ICollection<User> Users { get; set; }
}