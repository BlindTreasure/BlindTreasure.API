using BlindTreasure.Domain.DTOs.UserDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Infrastructure.Interfaces
{
    public interface IOAuthService
    {
        Task<UserDto> AuthenticateWithGoogle(string token);
    }
}
