using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenEvent.Web.Models.Analytic;
using OpenEvent.Web.Models.User;
using OpenEvent.Web.Services;

namespace OpenEvent.Web.Controllers
{
    /// <summary>
    /// API controller for all user related endpoints.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService UserService;
        private readonly ILogger<UserController> Logger;

        /// <summary>
        /// UserService default constructor.
        /// </summary>
        /// <param name="userService"></param>
        /// <param name="logger"></param>
        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            UserService = userService;
            Logger = logger;
        }

        /// <summary>
        /// Endpoint for creating a new user
        /// </summary>
        /// <param name="newUserBody"></param>
        /// <returns>
        /// ActionResult of <see cref="UserViewModel"/> representing basic user information.
        /// BadRequest if any exceptions are caught.
        /// </returns>
        [HttpPost]
        public async Task<ActionResult> Create([FromBody] NewUserBody newUserBody)
        {
            try
            {
                await UserService.Create(newUserBody);
                return Ok();
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
                return BadRequest(e);
            }
        }

        /// <summary>
        /// Endpoint for deleting a user
        /// </summary>
        /// <param name="id"></param>
        /// <returns>
        /// ActionResult if the user has been deleted.
        /// BadRequest if any exceptions are caught.
        /// </returns>
        [HttpDelete]
        public async Task<ActionResult> Destroy(Guid id)
        {
            try
            {
                await UserService.Destroy(id);
                return Ok();
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
                return BadRequest(e);
            }
        }

        /// <summary>
        /// Endpoint for getting a user for account page
        /// </summary>
        /// <param name="id"></param>
        /// <returns>
        /// ActionResult of <see cref="UserAccountModel"/> representing all data needed for account page.
        /// BadRequest if any exceptions are caught.
        /// </returns>
        [HttpGet("Account")]
        public async Task<ActionResult<UserAccountModel>> GetAccountUser(Guid id)
        {
            try
            {
                var result = await UserService.Get(id);
                return result;
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
                return BadRequest(e);
            }
        }

        /// <summary>
        /// Endpoint for checking if a username is inuse.
        /// </summary>
        /// <param name="username"></param>
        /// <returns>
        /// ActionResult of bool if username exists.
        /// </returns>
        [HttpGet("UserNameExists")]
        public async Task<ActionResult<bool>> UserNameExists(string username)
        {
            return Ok(await UserService.UserNameExists(username));
        }

        /// <summary>
        /// Endpoint for checking if a email is inuse.
        /// </summary>
        /// <param name="email"></param>
        /// <returns>
        /// ActionResult of bool if email exists.
        /// </returns>
        [HttpGet("EmailExists")]
        public async Task<ActionResult<bool>> EmailExists(string email)
        {
            return Ok(await UserService.EmailExists(email));
        }

        /// <summary>
        /// Endpoint for checking if a phone number is inuse. 
        /// </summary>
        /// <param name="phoneNumber"></param>
        /// <returns>
        /// ActionResult of bool if phone number exists.
        /// </returns>
        [HttpGet("PhoneExists")]
        public async Task<ActionResult<bool>> PhoneExists(string phoneNumber)
        {
            return Ok(await UserService.PhoneExists(phoneNumber));
        }
        
        /// <summary>
        /// Endpoint for updating a users username
        /// </summary>
        /// <param name="updateUserNameBody"></param>
        /// <returns>Ok and username if updated</returns>
        [HttpPost("updateUserName")]
        public async Task<ActionResult<string>> UpdateUserName([FromBody] UpdateUserNameBody updateUserNameBody)
        {
            try
            {
                var result = await UserService.UpdateUserName(updateUserNameBody.Id, updateUserNameBody.UserName);
                return Ok(new
                {
                    username = result
                });
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
                return BadRequest(e);
            }
        }

        /// <summary>
        /// Endpoint for updating a users avatar
        /// </summary>
        /// <param name="updateAvatarBody"></param>
        /// <returns>Ok and avatar string if updated</returns>
        [HttpPost("updateAvatar")]
        public async Task<ActionResult<string>> UpdateAvatar([FromBody] UpdateAvatarBody updateAvatarBody)
        {
            try
            {
                var result = await UserService.UpdateAvatar(updateAvatarBody.Id, updateAvatarBody.Avatar);
                return Ok(new {avatar = result});
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
                return BadRequest(e);
            }
        }

        /// <summary>
        /// Endpoint for updating a users theme preference.
        /// </summary>
        /// <param name="updateThemePreferenceBody"></param>
        /// <returns></returns>
        [HttpPost("updateThemePreference")]
        public async Task<ActionResult> UpdateThemePreference(
            [FromBody] UpdateThemePreferenceBody updateThemePreferenceBody)
        {
            try
            {
                await UserService.UpdateThemePreference(updateThemePreferenceBody.Id,
                    updateThemePreferenceBody.IsDarkMode);
                return Ok(new {isDarkMode = updateThemePreferenceBody.IsDarkMode});
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
                return BadRequest(e);
            }
        }
        
        /// <summary>
        /// Endpoint for updating a users address.
        /// </summary>
        /// <param name="updateUserAddressBody"></param>
        /// <returns></returns>
        [HttpPost("updateAddress")]
        public async Task<ActionResult<string>> UpdateAddress([FromBody] UpdateUserAddressBody updateUserAddressBody)
        {
            try
            {
                var result = await UserService.UpdateAddress(updateUserAddressBody.Id, updateUserAddressBody.Address);
                return Ok(new
                {
                    address = result
                });
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
                return BadRequest(e);
            }
        }

        /// <summary>
        /// Endpoint for getting all analytics associated with the user.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("Analytics")]
        public async Task<ActionResult<UsersAnalytics>> GetUsersAnalytics(Guid id)
        {
            try
            {
                var result = await UserService.GetAnalytics(id);
                return result;
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
                return BadRequest(e);
            }
        }
    }
}