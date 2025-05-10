using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Domain;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly BlindTreasureDbContext _context;
    private readonly ILoggerService _logger;

    public SystemController(BlindTreasureDbContext context, ILoggerService logger)
    {
        _context = context;
        _logger = logger;
    }

    // private async Task SeedRolesAndUsers()
    // {
    //     // Seed Roles
    //     var roles = new List<Role>
    //     {
    //         new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), RoleName = RoleType.Customer },
    //         new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), RoleName = RoleType.Staff },
    //         new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), RoleName = RoleType.Admin }
    //     };
    //
    //     _logger.Info("Seeding roles...");
    //     await _context.Roles.AddRangeAsync(roles);
    //     await _context.SaveChangesAsync();
    //     _logger.Success("Roles seeded successfully.");
    //
    //     // Seed Users
    //     var passwordHasher = new PasswordHasher();
    //     var users = new List<User>
    //     {
    //         new()
    //         {
    //             FullName = "Admin Phuc",
    //             Email = "phucadmin@example.com",
    //             Gender = true,
    //             DateOfBirth = new DateTime(1985, 1, 1),
    //             PhoneNumber = "0393734206",
    //             PasswordHash = passwordHasher.HashPassword("AdminPassword@"),
    //             RoleName = RoleType.Admin
    //         },
    //         new()
    //         {
    //             FullName = "Admin Two",
    //             Email = "admin2@example.com",
    //             Gender = false,
    //             DateOfBirth = new DateTime(1987, 2, 2),
    //             PhoneNumber = "0987654321",
    //             PasswordHash = passwordHasher.HashPassword("AdminPassword@"),
    //             RoleName = RoleType.Admin
    //         },
    //         new()
    //         {
    //             FullName = "Staff Phúc",
    //             Email = "staff1@gmail.com",
    //             Gender = true,
    //             DateOfBirth = new DateTime(1990, 3, 3),
    //             PhoneNumber = "1122334455",
    //             PasswordHash = passwordHasher.HashPassword("1@"),
    //             RoleName = RoleType.Staff
    //         },
    //         new()
    //         {
    //             FullName = "Staff uy lê",
    //             Email = "staff2@gmail.com",
    //             Gender = false,
    //             DateOfBirth = new DateTime(1992, 4, 4),
    //             PhoneNumber = "5566778899",
    //             PasswordHash = passwordHasher.HashPassword("1@"),
    //             RoleName = RoleType.Staff,
    //             ImageUrl =
    //                 "https://scontent-hkg4-1.xx.fbcdn.net/v/t1.15752-9/475528128_1134900321451127_3323942936519002305_n.png?_nc_cat=108&ccb=1-7&_nc_sid=9f807c&_nc_eui2=AeGc9uwG_xXuF9RJoF7bI17SeurTYb30fQh66tNhvfR9CKznXnCZn4dU5Bc59_JA3_eYgxJX5aKpI0iFLjxy86bK&_nc_ohc=7CpLZ9FbsmgQ7kNvgGSiMCT&_nc_oc=Adi_PRwfWJIV7gbsWdbStchrmVdAHsHWGFV_1nNFo5X716uaWl7yNxMqHEJLoZtyE4_nijOpXbFJrFCXfwWdjZWF&_nc_zt=23&_nc_ht=scontent-hkg4-1.xx&oh=03_Q7cD1gE_4j4t6T7fbVLzoUGvojV-dXqkEzD-KVSHY0aHx9PdgA&oe=67EFCCED"
    //         }
    //     };
    //
    //     _logger.Info("Seeding users...");
    //     await _context.Users.AddRangeAsync(users);
    //     await _context.SaveChangesAsync();
    //     _logger.Success("Users seeded successfully.");
    // }
}