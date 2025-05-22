using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Services
{
    public class OAuthService : IOAuthService
    {
        private readonly IUserService _userService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly string clientId;
        private readonly string passwordCharacters;

        public OAuthService(IUserService userService, IUnitOfWork unitOfWork, IConfiguration configuration)
        {
            _userService = userService;
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            clientId = _configuration["OAuthSettings:ClientId"] ?? throw new Exception("Missing google client Id in config");
            passwordCharacters = _configuration["OAuthSettings:PasswordCharacters"] ?? throw new Exception("Missing google oauth setting in config");
        }


        public async Task<UserDto> AuthenticateWithGoogle(string token)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw ErrorHelper.BadRequest("Client Id is missing");
            }

            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new List<string> { clientId }
            };

            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(token, settings);
            }
            catch
            {
                throw ErrorHelper.Forbidden("Invalid token");
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.Email))
            {
                throw ErrorHelper.BadRequest("Null Payload từ google");
            }

            try
            {
                var user = await _userService.GetUserByEmail(payload.Email, true);
                if (user == null)
                {
                    throw ErrorHelper.NotFound("Account not found");
                }
                return ToUserDto(user);
            }
            catch (Exception ex) 
            {
                // User not found, register a new one
                if (ex.Message.Contains("Account not found", StringComparison.OrdinalIgnoreCase))
                {
                    var request = new UserCreateDto
                    {
                        Email = payload.Email,
                        FullName = payload.Name,
                        Password = GenerateSecurePassword(16), // Tạo mật khẩu ngẫu nhiên
                        AvatarUrl = payload.Picture,
                        RoleName = RoleType.Customer,
                        DateOfBirth = DateTime.UtcNow, 
                        // hoặc để null nếu cho phép                                                       
                        // Phone = null,                                                       
                        // Gender = null,
                        // Password = random
                    };

                    var result = await _userService.CreateUserAsync(request);
                    if (result == null)
                        throw ErrorHelper.Internal("Failed to create user");

                    return result;
                }
                // Nếu là lỗi khác, quăng lại để middleware xử lý
                throw;
            }
        }

     private static string GenerateSecurePassword(int length = 16)
        {
            const string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()";
            var password = new StringBuilder();
            using (var rng = RandomNumberGenerator.Create())
            {
                var data = new byte[4];
                for (int i = 0; i < length; i++)
                {
                    rng.GetBytes(data);
                    var randomIndex = BitConverter.ToUInt32(data, 0) % characters.Length;
                    password.Append(characters[(int)randomIndex]);
                }
            }
            return password.ToString();
        }



        /// <summary>
        ///     Maps User entity to UserDto.
        /// </summary>
        private static UserDto ToUserDto(User user)
        {
            return new UserDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                DateOfBirth = user.DateOfBirth,
                AvatarUrl = user.AvatarUrl,
                Status = user.Status,
                PhoneNumber = user.Phone,
                RoleName = user.RoleName,
                CreatedAt = user.CreatedAt
            };
        }
    }
}
