﻿using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Promact.Oauth.Server.Constants;
using Promact.Oauth.Server.Data_Repository;
using Promact.Oauth.Server.ExceptionHandler;
using Promact.Oauth.Server.Models;
using Promact.Oauth.Server.Models.ApplicationClasses;
using Promact.Oauth.Server.Models.ManageViewModels;
using Promact.Oauth.Server.Repository.ProjectsRepository;
using Promact.Oauth.Server.Services;
using Promact.Oauth.Server.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Promact.Oauth.Server.Repository
{
    public class UserRepository : IUserRepository
    {

        #region "Private Variable(s)"



        private readonly IDataRepository<ApplicationUser> _applicationUserDataRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;
        private readonly IMapper _mapperContext;
        private readonly IDataRepository<ProjectUser> _projectUserRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IDataRepository<Project> _projectDataRepository;
        private readonly IOptions<AppSettingUtil> _appSettingUtil;
        private readonly IStringConstant _stringConstant;
        private readonly IDataRepository<ProjectUser> _projectUserDataRepository;
        private readonly IEmailUtil _emailUtil;

        #endregion

        #region "Constructor"

        public UserRepository(IDataRepository<ApplicationUser> applicationUserDataRepository,
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager, IEmailSender emailSender,
            IMapper mapperContext, IDataRepository<ProjectUser> projectUserRepository,
            IProjectRepository projectRepository, IOptions<AppSettingUtil> appSettingUtil,
            IDataRepository<Project> projectDataRepository,
            IStringConstant stringConstant,
            IDataRepository<ProjectUser> projectUserDataRepository, IEmailUtil emailUtil)
        {
            _applicationUserDataRepository = applicationUserDataRepository;
            _userManager = userManager;
            _emailSender = emailSender;
            _mapperContext = mapperContext;
            _projectUserRepository = projectUserRepository;
            _projectRepository = projectRepository;
            _roleManager = roleManager;
            _projectDataRepository = projectDataRepository;
            _appSettingUtil = appSettingUtil;
            _stringConstant = stringConstant;
            _projectUserDataRepository = projectUserDataRepository;
            _emailUtil = emailUtil;
        }

        #endregion

        #region "Public Method(s)"

        /// <summary>
        /// This method is used to add new user
        /// </summary>
        /// <param name="newUser">Passed userAC object</param>
        /// <param name="createdBy">Passed id of user who has created this user.</param>
        /// <returns>Added user id</returns>
        public async Task<string> AddUserAsync(UserAc newUser, string createdBy)
        {
            LeaveCalculator leaveCalculator = new LeaveCalculator();
            leaveCalculator = CalculateAllowedLeaves(Convert.ToDateTime(newUser.JoiningDate));
            newUser.NumberOfCasualLeave = leaveCalculator.CasualLeave;
            newUser.NumberOfSickLeave = leaveCalculator.SickLeave;
            var user = _mapperContext.Map<UserAc, ApplicationUser>(newUser);
            user.UserName = user.Email;
            user.CreatedBy = createdBy;
            user.CreatedDateTime = DateTime.UtcNow;
            string password = GetRandomString();//get readom password.
            await _userManager.CreateAsync(user, password);
            await _userManager.AddToRoleAsync(user, newUser.RoleName);//add role of new user.
            SendEmail(user.FirstName, user.Email, password);//send mail with generated password of new user. 
            return user.Id;
        }

        /// <summary>
        ///This method is used to get all role. -An
        /// </summary>
        /// <returns>List of user roles</returns>
        public async Task<List<RolesAc>> GetRolesAsync()
        {
            var roles = await _roleManager.Roles.ToListAsync();
            return _mapperContext.Map<List<IdentityRole>, List<RolesAc>>(roles);
        }

        /// <summary>
        /// This method is used for fetching the list of all users
        /// </summary>
        /// <returns>List of all users</returns>
        public async Task<IEnumerable<UserAc>> GetAllUsersAsync()
        {
            var users = await _userManager.Users.OrderByDescending(x => x.CreatedDateTime).ToListAsync();
            return _mapperContext.Map<IEnumerable<ApplicationUser>, IEnumerable<UserAc>>(users);
        }

        /// <summary>
        /// This method is used for getting the list of all Employees
        /// </summary>
        /// <returns>List of all Employees</returns>
        public async Task<IEnumerable<UserAc>> GetAllEmployeesAsync()
        {
            var users = await _userManager.Users.Where(user => user.IsActive).OrderBy(user => user.FirstName).ToListAsync();
            return _mapperContext.Map<IEnumerable<ApplicationUser>, IEnumerable<UserAc>>(users);
        }

        /// <summary>
        /// This method is used to edit the details of an existing user
        /// </summary>
        /// <param name="editedUser">Passed UserAc object</param>
        /// <param name="updatedBy">Passed id of user who has updated this user.</param>
        /// <returns>Updated user id.</returns>
        public async Task<string> UpdateUserDetailsAsync(UserAc editedUser, string updatedBy)
        {
            var user = _userManager.Users.FirstOrDefault(x => x.SlackUserName == editedUser.SlackUserName && x.Id != editedUser.Id);
            if (user == null)
            {
                user = await _userManager.FindByIdAsync(editedUser.Id);
                user.FirstName = editedUser.FirstName;
                user.LastName = editedUser.LastName;
                user.Email = editedUser.Email;
                user.IsActive = editedUser.IsActive;
                user.UpdatedBy = updatedBy;
                user.UpdatedDateTime = DateTime.UtcNow;
                user.NumberOfCasualLeave = editedUser.NumberOfCasualLeave;
                user.NumberOfSickLeave = editedUser.NumberOfSickLeave;
                user.SlackUserName = editedUser.SlackUserName;
                await _userManager.UpdateAsync(user);
                //get user roles
                IList<string> listofUserRole = await _userManager.GetRolesAsync(user);
                //remove user role 
                var removeFromRole = await _userManager.RemoveFromRoleAsync(user, listofUserRole.First());
                //add new role of user.
                var addNewRole = await _userManager.AddToRoleAsync(user, editedUser.RoleName);
                return user.Id;
            }
            throw new SlackUserNotFound();
        }

        /// <summary>
        ///  This method used for get user detail by user id 
        /// </summary>
        /// <param name="id">Passed user id</param>
        /// <returns>UserAc application class object</returns>
        public async Task<UserAc> GetByIdAsync(string id)
        {
            ApplicationUser applicationUser = await _userManager.FindByIdAsync(id);
            if (applicationUser != null)
            {
                UserAc userAc = _mapperContext.Map<ApplicationUser, UserAc>(applicationUser);
                userAc.RoleName = (await _userManager.GetRolesAsync(applicationUser)).First();
                return userAc;
            }
            throw new UserNotFound();
        }

        /// <summary>
        /// This method is used to change the password of a particular user. -An
        /// </summary>
        /// <param name="passwordModel">Passed changePasswordViewModel object(OldPassword,NewPassword,ConfirmPassword,Email)</param>
        /// <returns>If password is changed successfully, return empty otherwise error message.</returns>
        public async Task<ChangePasswordErrorModel> ChangePasswordAsync(ChangePasswordViewModel passwordModel)
        {
            var user = await _userManager.FindByEmailAsync(passwordModel.Email);
            if (user != null)
            {
                ChangePasswordErrorModel changePasswordErrorModel = new ChangePasswordErrorModel();
                IdentityResult result = await _userManager.ChangePasswordAsync(user, passwordModel.OldPassword, passwordModel.NewPassword);
                if (!result.Succeeded)//When password not changed successfully then error message will be added in changePasswordErrorModel
                {
                    changePasswordErrorModel.ErrorMessage = result.Errors.First().Description.ToString();
                }
                return changePasswordErrorModel;
            }
            throw new UserNotFound();
        }

        /// <summary>
        /// This method is used to check if a user already exists in the database with the given userName
        /// </summary>
        /// <param name="userName">Passed userName</param>
        /// <returns>boolean: true if the user name exists,otherwise throw UserNotFound exception.</returns>
        public async Task<bool> FindByUserNameAsync(string userName)
        {
            var user = await _userManager.FindByNameAsync(userName);
            if (user == null)
            {
                throw new UserNotFound();
            }
            return true;
        }

        /// <summary>
        /// This method is used to check email is already exists in database.
        /// </summary>
        /// <param name="email">Passed user email address</param>
        /// <returns> boolean: true if the email exists, false if does not exist</returns>
        public async Task<bool> CheckEmailIsExistsAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            return user == null ? false : true;
        }


        /// <summary>
        /// Fetch user with given slack user name
        /// </summary>
        /// <param name="slackUserName">Passed slack user name</param>
        /// <returns>If user is exists return user otherwise throw SlackUserNotFound exception.</returns>
        public async Task<ApplicationUser> FindUserBySlackUserNameAsync(string slackUserName)
        {
            var user = await _applicationUserDataRepository.FirstOrDefaultAsync(x => x.SlackUserName == slackUserName);
            if (user == null)
                throw new SlackUserNotFound();
            else
                return user;
        }


        /// <summary>
        /// This method used for re-send mail for user credentials. -An
        /// </summary>
        /// <param name="id">passed userid</param>
        /// <returns></returns>
        public async Task ReSendMailAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            string newPassword = GetRandomString();
            var token = await _userManager.GeneratePasswordResetTokenAsync(user); //genrate passsword reset token
            IdentityResult result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            SendEmail(user.FirstName, user.Email, newPassword);
        }

        /// <summary>
        /// Method to get user details by slackUserId -SD
        /// </summary>
        /// <param name="slackUserId"></param>
        /// <returns>user details</returns>
        public ApplicationUser UserDetialByUserSlackId(string slackUserId)
        {

            var user = _applicationUserDataRepository.FirstOrDefault(x => x.SlackUserId == slackUserId);
            var newUser = new ApplicationUser
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                SlackUserId = user.SlackUserId
            };
            return newUser;
        }

        /// <summary>
        /// Method to get team leader's details by userSlackId -SD
        /// </summary>
        /// <param name="userSlackId"></param>
        /// <returns>list of team leader</returns>
        public async Task<List<ApplicationUser>> TeamLeaderByUserSlackIdAsync(string userSlackId)
        {
            var user = _userManager.Users.FirstOrDefault(x => x.SlackUserId == userSlackId);
            var projects = _projectUserRepository.Fetch(x => x.UserId == user.Id);
            List<ApplicationUser> teamLeaders = new List<ApplicationUser>();
            foreach (var project in projects)
            {
                var teamLeaderId = await _projectRepository.GetProjectByIdAsync(project.ProjectId);
                var teamLeader = teamLeaderId.TeamLeaderId;
                user = await _userManager.FindByIdAsync(teamLeader);
                //user = _userManager.Users.FirstOrDefault(x => x.Id == teamLeader);
                var newUser = new ApplicationUser
                {
                    UserName = user.UserName,
                    Email = user.Email,
                    SlackUserId = user.SlackUserId
                };
                teamLeaders.Add(newUser);
            }
            return teamLeaders;
        }

        /// <summary>
        /// Method to get management people details - SD
        /// </summary>
        /// <returns>list of management</returns>
        public async Task<List<ApplicationUser>> ManagementDetailsAsync()
        {
            var management = await _userManager.GetUsersInRoleAsync("Admin");
            List<ApplicationUser> managementUser = new List<ApplicationUser>();
            foreach (var user in management)
            {
                var newUser = new ApplicationUser
                {
                    FirstName = user.FirstName,
                    Email = user.Email,
                    SlackUserId = user.SlackUserId
                };
                managementUser.Add(newUser);
            }
            return managementUser;
        }


        /// <summary>
        /// Method to get the number of casual leave allowed to a user by slack user name 
        /// </summary>
        /// <param name="slackUserId"></param>
        /// <returns>number of casual leave</returns>
        public LeaveAllowed GetUserAllowedLeaveBySlackId(string slackUserId)
        {
            var user = _applicationUserDataRepository.FirstOrDefault(x => x.SlackUserId == slackUserId);
            LeaveAllowed leaveAllowed = new LeaveAllowed();
            leaveAllowed.CasualLeave = user.NumberOfCasualLeave;
            leaveAllowed.SickLeave = user.NumberOfSickLeave;
            return leaveAllowed;
        }

        /// <summary>
        /// Method to check whether user is admin or not
        /// </summary>
        /// <param name="slackUserId"></param>
        /// <returns>true or false</returns>
        public async Task<bool> IsAdminAsync(string slackUserId)
        {
            var user = _applicationUserDataRepository.FirstOrDefault(x => x.SlackUserId == slackUserId);
            var isAdmin = await _userManager.IsInRoleAsync(user, _stringConstant.Admin);
            return isAdmin;
        }

        /// <summary>
        /// This method is used to Get User details by Id
        /// </summary>
        /// <param name="userId"></param>
        /// <returns>details of user</returns>
        public async Task<UserAc> UserDetailByIdAsync(string userId)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == userId);
            return await GetUserAsync(user);
        }


        /// <summary>
        /// Method to return user role. - RS
        /// </summary>
        /// <param name="userId">passed user id for getting list of user role </param>
        /// <returns>users/user information</returns>
        public async Task<List<UserRoleAc>> GetUserRoleAsync(string userId)
        {
            ApplicationUser applicationUser = await _userManager.FindByIdAsync(userId);
            var userRole = (await _userManager.GetRolesAsync(applicationUser)).First();
            List<UserRoleAc> userRoleAcList = new List<UserRoleAc>();

            if (userRole == _stringConstant.RoleAdmin)
            {
                //getting the all user infromation. 
                var userRoleAdmin = new UserRoleAc(applicationUser.Id, applicationUser.UserName, applicationUser.FirstName + " " + applicationUser.LastName, userRole);
                userRoleAcList.Add(userRoleAdmin);
                //getting employee role id. 
                var roleId = (await _roleManager.Roles.SingleAsync(x => x.Name == _stringConstant.RoleEmployee)).Id;
                //getting active employee list.
                var userList = await _applicationUserDataRepository.Fetch(y => y.IsActive == true && y.Roles.Any(x => x.RoleId == roleId)).ToListAsync();
                foreach (var user in userList)
                {
                    var userRoleAc = new UserRoleAc(user.Id, user.UserName, user.FirstName + " " + user.LastName, userRole);
                    userRoleAcList.Add(userRoleAc);
                }
            }
            else
            {
                //check login user is teamLeader or not.
                var isProjectExists = await _projectDataRepository.FirstOrDefaultAsync(x => x.TeamLeaderId == applicationUser.Id);
                //If isProjectExists is null then user role is employee other wise user role is teamleader. 
                var userRoleAc = new UserRoleAc(applicationUser.Id, applicationUser.UserName, applicationUser.FirstName + " " + applicationUser.LastName, (isProjectExists != null ? _stringConstant.RoleTeamLeader : _stringConstant.RoleEmployee));
                userRoleAcList.Add(userRoleAc);
            }
            return userRoleAcList;
        }

        /// <summary>
        /// Method to return list of users. - RS
        /// </summary>
        /// <param name="userId"></param>
        /// <returns>teamMembers information</returns>
        public async Task<List<UserRoleAc>> GetTeamMembersAsync(string userId)
        {
            ApplicationUser applicationUser = await _userManager.FindByIdAsync(userId);
            var userRolesAcList = new List<UserRoleAc>();
            var userRoleAc = new UserRoleAc(applicationUser.Id, applicationUser.UserName, applicationUser.FirstName + " " + applicationUser.LastName, _stringConstant.RoleTeamLeader);
            userRolesAcList.Add(userRoleAc);
            //getting teamLeader Project.
            var project = await _projectDataRepository.FirstAsync(x => x.TeamLeaderId == applicationUser.Id);
            //getting user Id list of particular project.
            var userIdList = (await _projectUserDataRepository.Fetch(x => x.ProjectId == project.Id).ToListAsync()).Select(y => y.UserId);
            //getting list of user infromation.
            var userList = await _applicationUserDataRepository.Fetch(x => userIdList.Contains(x.Id)).ToListAsync();
            foreach (var user in userList)
            {
                var usersRoleAc = new UserRoleAc(user.Id, user.UserName, user.FirstName + " " + user.LastName, _stringConstant.RoleEmployee);
                userRolesAcList.Add(usersRoleAc);
            }
            return userRolesAcList;
        }


        /// <summary>
        /// Method to return list of users/employees of the given slack channel name. - JJ
        /// </summary>
        /// <param name="slackChannelName"></param>
        /// <returns>list of object of UserAc</returns>
        public async Task<List<UserAc>> GetProjectUserBySlackChannelNameAsync(string slackChannelName)
        {
            Project project = await _projectDataRepository.FirstOrDefaultAsync(x => x.SlackChannelName == slackChannelName);
            List<UserAc> userAcList = new List<UserAc>();
            if (project != null)
            {
                //fetches the ids of users of the project
                IEnumerable<string> userIdList = (await _projectUserDataRepository.Fetch(x => x.ProjectId == project.Id).ToListAsync()).Select(y => y.UserId);
                //fetches the application users of the above obtained ids.
                List<ApplicationUser> applicationUsers = await _applicationUserDataRepository.Fetch(x => userIdList.Contains(x.Id)).ToListAsync();
                //perform mapping
                userAcList = _mapperContext.Map<List<ApplicationUser>, List<UserAc>>(applicationUsers);
            }
            return userAcList;
        }


        /// <summary>
        /// The method is used to get list of projects along with its users for a specific teamleader 
        /// </summary>
        /// <param name="teamLeaderId"></param>
        /// <returns>list of projects with users for a specific teamleader</returns>
        public async Task<List<UserAc>> GetProjectUsersByTeamLeaderIdAsync(string teamLeaderId)
        {
            List<UserAc> projectUsers = new List<UserAc>();
            //Get projects for that specific teamleader
            List<Project> projects = await _projectDataRepository.Fetch(x => x.TeamLeaderId.Equals(teamLeaderId)).ToListAsync();

            if (projects.Any())
            {
                //Get details of teamleader
                ApplicationUser teamLeader = await _applicationUserDataRepository.FirstOrDefaultAsync(x => x.Id.Equals(teamLeaderId));
                if (teamLeader != null)
                {
                    UserAc projectTeamLeader = _mapperContext.Map<ApplicationUser, UserAc>(teamLeader);
                    projectTeamLeader.Role = _stringConstant.TeamLeader;
                    projectUsers.Add(projectTeamLeader);
                }

                //Get details of employees for projects with that particular teamleader 
                foreach (var project in projects)
                {
                    List<ProjectUser> projectUsersList = await _projectUserRepository.Fetch(x => x.ProjectId == project.Id).ToListAsync();
                    foreach (var projectUser in projectUsersList)
                    {
                        ApplicationUser user = await _applicationUserDataRepository.FirstOrDefaultAsync(x => x.Id.Equals(projectUser.UserId));
                        if (user != null)
                        {
                            var Roles = (await _userManager.GetRolesAsync(user)).First();
                            UserAc employee = _mapperContext.Map<ApplicationUser, UserAc>(user);
                            employee.Role = Roles;
                            //Checking if employee is already present in the list or not
                            if (!projectUsers.Any(x => x.Id == employee.Id))
                            {
                                projectUsers.Add(employee);
                            }
                        }
                    }
                }
            }
            return projectUsers;
        }
        #endregion

        #region Private Methods

        /// <summary>
        /// This method is used to send email to the currently added user. -An
        /// </summary>
        /// <param name="firstName">Passed user first name</param>
        /// <param name="email">Passed user email</param>
        /// <param name="password">Passed password</param>
        private void SendEmail(string firstName, string email, string password)
        {
            string finaleTemplate = _emailUtil.GetEmailTemplateForUserDetail(firstName, email, password);
            _emailSender.SendEmail(email, _stringConstant.LoginCredentials, finaleTemplate);
        }

        /// <summary>
        /// Method is used to return a user after assigning a role and mapping from ApplicationUser class to UserAc class
        /// </summary>
        /// <param name="user"></param>
        /// <returns>user</returns>
        private async Task<UserAc> GetUserAsync(ApplicationUser user)
        {
            //Gets a list of roles the specified user belongs to
            string roles = (await _userManager.GetRolesAsync(user)).First();
            UserAc newUser = _mapperContext.Map<ApplicationUser, UserAc>(user);
            //assign role
            if (String.Compare(roles, _stringConstant.Admin, true) == 0)
            {
                newUser.Role = roles;
            }
            else if (String.Compare(roles, _stringConstant.Employee, true) == 0)
            {
                Project project = await _projectDataRepository.FirstOrDefaultAsync(x => x.TeamLeaderId.Equals(user.Id));
                if (project != null)
                {
                    newUser.Role = _stringConstant.TeamLeader;
                }
                else
                {
                    newUser.Role = _stringConstant.Employee;
                }
            }
            return newUser;
        }

        /// <summary>
        /// This method used for genrate random string with alphanumeric words and special characters. -An
        /// </summary>
        /// <returns>Random string</returns>
        private string GetRandomString()
        {
            Random random = new Random();
            //Initialize static Ato,atoz,0to9 and special characters seprated by '|'.
            string chars = _stringConstant.RandomString;
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                //Get random 4 characters from diffrent portion and append on stringbuilder. 
                stringBuilder.Append(new string(Enumerable.Repeat(chars.Split('|').ToArray()[i], 3).Select(s => s[random.Next(4)]).ToArray()));
            }
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Calculat casual leava and sick leave from the date of joining - RS
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        private LeaveCalculator CalculateAllowedLeaves(DateTime dateTime)
        {
            double casualAllowed = 0;
            double sickAllowed = 0;
            var day = dateTime.Day;
            var month = dateTime.Month;
            var year = dateTime.Year;
            double casualAllow = _appSettingUtil.Value.CasualLeave;
            double sickAllow = _appSettingUtil.Value.SickLeave;
            if (year >= DateTime.Now.Year)
            {
                //If an employee joins between 1st to 15th of month, then he/she will be eligible for that particular month's leaves 
                //and if he/she joins after 15th of month, he/she will not be eligible for that month's leaves.

                //calculate casualAllowed and sickAllowed.
                //In Our Project we consider Leave renewal on 1st april
                if (month >= 4)
                {
                    //if first 15 days of month april to December then substact 4 other wise substact 3 in month
                    if (day <= 15)
                    {

                        casualAllowed = (casualAllow / 12) * (12 - (month - 4));
                        sickAllowed = (sickAllow / 12) * (12 - (month - 4));
                    }
                    else
                    {
                        casualAllowed = (casualAllow / 12) * (12 - (month - 3));
                        sickAllowed = (sickAllow / 12) * (12 - (month - 3));
                    }
                }

                else
                {
                    //if first 15 days of month January to March then add 8 other wise add 9 in month
                    if (day <= 15)
                    {
                        casualAllowed = (casualAllow / 12) * (12 - (month + 8));
                        sickAllowed = (sickAllow / 12) * (12 - (month + 8));
                    }
                    else
                    {
                        casualAllowed = (casualAllow / 12) * (12 - (month + 9));
                        sickAllowed = (sickAllow / 12) * (12 - (month + 9));
                    }
                }

                // If calculated casualAllowed decimal value is exact 0.5 then it's considered half day casual leave
                if (casualAllowed % 1 != 0)
                {
                    double CasualAlloweddecimal = casualAllowed - Math.Floor(casualAllowed);
                    if (CasualAlloweddecimal != 0.5) { casualAllowed = Convert.ToInt32(casualAllowed); }
                }

                // If calculated sickAllowed decimal value is exact 0.5 then it's considered half day sick leave 
                // If calculated sickAllowed decimal value is more than  0.90 then add one leave in sick leave 
                if (sickAllowed % 1 != 0)
                {
                    double sickAlloweddecimal = sickAllowed - Math.Floor(sickAllowed);
                    if (sickAlloweddecimal != 0.5) { sickAllowed = Convert.ToInt32(Math.Floor(sickAllowed)); }
                    if (sickAlloweddecimal > 0.90) { sickAllowed = sickAllowed + 1; }

                }
            }
            else
            {
                casualAllowed = _appSettingUtil.Value.CasualLeave;
                sickAllowed = _appSettingUtil.Value.SickLeave;
            }
            LeaveCalculator calculate = new LeaveCalculator
            {
                CasualLeave = casualAllowed,
                SickLeave = sickAllowed
            };
            return calculate;
        }

        #endregion


    }
}