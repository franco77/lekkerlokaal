﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LekkerLokaal.Models;
using LekkerLokaal.Models.AccountViewModels;
using LekkerLokaal.Services;
using LekkerLokaal.Models.Domain;
using MimeKit;
using System.IO;
using System.Net.Mail;

namespace LekkerLokaal.Controllers
{
    [Authorize]
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger _logger;
        private readonly ICategorieRepository _categorieRepository;
        private readonly IGebruikerRepository _gebruikerRepository;
        private readonly IHandelaarRepository _handelaarRepository;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILogger<AccountController> logger,
            ICategorieRepository categorieRepository,
            IGebruikerRepository gebruikerRepository,
            IHandelaarRepository handelaarRepository)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = logger;
            _categorieRepository = categorieRepository;
            _gebruikerRepository = gebruikerRepository;
            _handelaarRepository = handelaarRepository;
        }

        [TempData]
        public string ErrorMessage { get; set; }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string returnUrl = null)
        {
            if (returnUrl != null && returnUrl.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Admin");
            }
            else
            {
                // Clear the existing external cookie to ensure a clean login process
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");
                    return RedirectToLocal(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToAction(nameof(LoginWith2fa), new { returnUrl, model.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToAction(nameof(Lockout));
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Uw e-mailadres en/of wachtwoord is fout. Gelieve het opnieuw te proberen");
                    return View(model);
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> LoginWith2fa(bool rememberMe, string returnUrl = null)
        {
            // Ensure the user has gone through the username & password screen first
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();

            if (user == null)
            {
                throw new ApplicationException($"Unable to load two-factor authentication user.");
            }

            var model = new LoginWith2faViewModel { RememberMe = rememberMe };
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginWith2fa(LoginWith2faViewModel model, bool rememberMe, string returnUrl = null)
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var authenticatorCode = model.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);

            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, rememberMe, model.RememberMachine);

            if (result.Succeeded)
            {
                _logger.LogInformation("User with ID {UserId} logged in with 2fa.", user.Id);
                return RedirectToLocal(returnUrl);
            }
            else if (result.IsLockedOut)
            {
                _logger.LogWarning("User with ID {UserId} account locked out.", user.Id);
                return RedirectToAction(nameof(Lockout));
            }
            else
            {
                _logger.LogWarning("Invalid authenticator code entered for user with ID {UserId}.", user.Id);
                ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
                return View();
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> LoginWithRecoveryCode(string returnUrl = null)
        {
            // Ensure the user has gone through the username & password screen first
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                throw new ApplicationException($"Unable to load two-factor authentication user.");
            }

            ViewData["ReturnUrl"] = returnUrl;
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginWithRecoveryCode(LoginWithRecoveryCodeViewModel model, string returnUrl = null)
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                throw new ApplicationException($"Unable to load two-factor authentication user.");
            }

            var recoveryCode = model.RecoveryCode.Replace(" ", string.Empty);

            var result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);

            if (result.Succeeded)
            {
                _logger.LogInformation("User with ID {UserId} logged in with a recovery code.", user.Id);
                return RedirectToLocal(returnUrl);
            }
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User with ID {UserId} account locked out.", user.Id);
                return RedirectToAction(nameof(Lockout));
            }
            else
            {
                _logger.LogWarning("Invalid recovery code entered for user with ID {UserId}", user.Id);
                ModelState.AddModelError(string.Empty, "Invalid recovery code entered.");
                return View();
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Lockout()
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register(string returnUrl = null)
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["Geslacht"] = Geslachten();
            return View();
        }

        private SelectList Geslachten()
        {
            var geslachten = new List<Geslacht>();
            foreach (Geslacht geslacht in Enum.GetValues(typeof(Geslacht)))
            {
                geslachten.Add(geslacht);
            }
            return new SelectList(geslachten);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string returnUrl = null)
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    var gebruiker = new Gebruiker
                    {
                        Voornaam = model.Voornaam,
                        Familienaam = model.Familienaam,
                        Emailadres = model.Email,
                        Geslacht = model.Geslacht ?? Geslacht.Anders
                    };
                    _gebruikerRepository.Add(gebruiker);
                    _gebruikerRepository.SaveChanges();
                    _logger.LogInformation("User created a new account with password.");

                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var callbackUrl = Url.EmailConfirmationLink(user.Id, code, Request.Scheme);
                    await _emailSender.SendEmailConfirmationAsync(model.Email, callbackUrl, model.Voornaam);

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    _logger.LogInformation("User created a new account with password.");
                    return RedirectToLocal(returnUrl);
                }
                AddErrors(result);
            }
            // If we got this far, something failed, redisplay form
            ViewData["Geslacht"] = Geslachten();
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult RegisterHandelaar(string returnUrl = null)
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            ViewData["Handelaar"] = returnUrl;
            ViewData["categorie"] = new SelectList(_categorieRepository.GetAll().Select(c => c.Naam));
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterHandelaar(RegisterHandelaarViewModel model, string returnUrl = null)
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await _userManager.CreateAsync(user, Guid.NewGuid().ToString());
                if (result.Succeeded)
                {
                    await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.Role, "handelaar"));

                    var message = new MailMessage();
                    message.From = new MailAddress("lekkerlokaalst@gmail.com");
                    message.To.Add("lekkerlokaalst@gmail.com");
                    message.Subject = "Een nieuwe handelaar heeft zich zopas ingeschreven via het handelaarsformulier";
                    message.Body = String.Format("Naam handelszaak: {0}\n" +
                                            "E-mailadres: {1}\n" +
                                            "Straat: {2}\n" +
                                            "Huisnummer: {3}\n" +
                                            "Postcode: {4}\n" +
                                            "Gemeente: {5}\n" +
                                            "BTW Nummer: {6}\n" +
                                            "Beschrijving: {7}\n",
                                            model.NaamHandelszaak, model.Email, model.Straat, model.Huisnummer, model.Postcode, model.Plaatsnaam, model.BTWNummer, model.Beschrijving);

                    Handelaar nieuweHandelaar = new Handelaar(model.NaamHandelszaak, model.Email, model.Beschrijving, model.BTWNummer, model.Straat, model.Huisnummer, model.Postcode, model.Plaatsnaam, false);
                    _handelaarRepository.Add(nieuweHandelaar);
                    _handelaarRepository.SaveChanges();


                    var filePath = @"wwwroot/images/handelaar/" + nieuweHandelaar.HandelaarId + "/logo.jpg";
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    var fileStream = new FileStream(filePath, FileMode.Create);
                    await model.Logo.CopyToAsync(fileStream);
                    fileStream.Close();

                    var attachment = new Attachment(@"wwwroot/images/handelaar/" + nieuweHandelaar.HandelaarId + "/logo.jpg");
                    attachment.Name = "logo.jpg";
                    message.Attachments.Add(attachment);
                    var SmtpServer = new SmtpClient("smtp.gmail.com");
                    SmtpServer.Port = 587;
                    SmtpServer.Credentials = new System.Net.NetworkCredential("lekkerlokaalst@gmail.com", "LokaalLekker123");
                    SmtpServer.EnableSsl = true;

                    SmtpServer.Send(message);
                    message.Attachments.Remove(attachment);
                    attachment.Dispose();

                    message.From = new MailAddress("lekkerlokaalst@gmail.com");
                    message.To.Clear();
                    message.To.Add(model.Email);
                    message.Subject = "Uw verzoek om handelaar te worden op LekkerLokaal.be is correct ontvangen.";
                    message.Body = String.Format("Beste medwerker van {0}, \n" +
                                            "Uw verzoek om handelaar te worden bij LekkerLokaal.be is correct ontvangen. \n\n" +
                                            "Onderstaande gegevens zullen gecontroleerd worden door een administrator. U mag een E-mail verwachten zodra uw verzoek al dan niet aanvaard wordt." +
                                            "Naam handelszaak: {1}\n" +
                                            "E-mailadres: {2}\n" +
                                            "Straat: {3}\n" +
                                            "Huisnummer: {4}\n" +
                                            "Postcode: {5}\n" +
                                            "Gemeente: {6}\n" +
                                            "BTW Nummer: {7}\n" +
                                            "Beschrijving: {8}\n",
                                            model.NaamHandelszaak, model.NaamHandelszaak, model.Email, model.Straat, model.Huisnummer, model.Postcode, model.Plaatsnaam, model.BTWNummer, model.Beschrijving);
                    SmtpServer.Send(message);

                    return RedirectToLocal(returnUrl);
                }
                AddErrors(result);
            }
            // If we got this far, something failed, redisplay form
            ViewData["categorie"] = new SelectList(_categorieRepository.GetAll().Select(c => c.Naam));
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(string returnUrl = null)
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            if (Url.IsLocalUrl(returnUrl) && returnUrl != null)
                return Redirect(returnUrl);
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToAction(nameof(Login));
            }
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return RedirectToAction(nameof(Login));
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in with {Name} provider.", info.LoginProvider);
                return RedirectToLocal(returnUrl);
            }
            if (result.IsLockedOut)
            {
                return RedirectToAction(nameof(Lockout));
            }
            else
            {
                // If the user does not have an account, then ask the user to create an account.
                ViewData["ReturnUrl"] = returnUrl;
                ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
                ViewData["Geslacht"] = Geslachten();
                ViewData["LoginProvider"] = info.LoginProvider;

                var email = info.Principal.FindFirstValue(ClaimTypes.Email) ?? "Uw e-mailadres";
                var voornaam = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "John";
                var familienaam = info.Principal.FindFirstValue(ClaimTypes.Surname) ?? "Doe";
                var geslachtTekst = info.Principal.FindFirstValue(ClaimTypes.Gender) ?? "Anders";
                var geslacht = Geslacht.Anders;

                switch (geslachtTekst.ToLower())
                {
                    case "male":
                        geslacht = Geslacht.Man;
                        break;
                    case "female":
                        geslacht = Geslacht.Vrouw;
                        break;
                    default:
                        geslacht = Geslacht.Anders;
                        break;
                }

                return View("ExternalLogin", new ExternalLoginViewModel
                {
                    Email = email,
                    Voornaam = voornaam,
                    Familienaam = familienaam,
                    Geslacht = geslacht
                });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalLoginConfirmation(ExternalLoginViewModel model, string returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                // Get the information about the user from the external login provider
                var info = await _signInManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    throw new ApplicationException("Error loading external login information during confirmation.");
                }
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await _userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    var gebruiker = new Gebruiker
                    {
                        Voornaam = model.Voornaam,
                        Familienaam = model.Familienaam,
                        Geslacht = model.Geslacht,
                        Emailadres = model.Email
                    };

                    _gebruikerRepository.Add(gebruiker);
                    _gebruikerRepository.SaveChanges();

                    var wachtwoord = Guid.NewGuid().ToString();
                    await _userManager.AddPasswordAsync(user, wachtwoord);
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    await _userManager.ConfirmEmailAsync(user, token);

                    var message = new MailMessage();
                    message.From = new MailAddress("lekkerlokaalst@gmail.com");
                    message.To.Add(model.Email);
                    message.Subject = "U heeft een account aangemaakt bij Lekker Lokaal!";

                    message.Body = String.Format("Beste, \n" +
                   "U heeft recent een account aangemaakt op Lekker Lokaal via sociale media. \n\n" +
                   "Uw gegevens om aan te melden zijn: \n" +
                   "E-mailadres: " + model.Email + "\n" +
                   "Wachtwoord: " + wachtwoord + "\n\n" +
                   "We bevelen u aan om bij uw eerste aanmelding uw wachtwoord te wijzigen. \n\n" +
                   "Met vriendelijke groeten, \n" +
                   "Het Lekker Lokaal team");

                    var SmtpServer = new SmtpClient("smtp.gmail.com");
                    SmtpServer.Port = 587;
                    SmtpServer.Credentials = new System.Net.NetworkCredential("lekkerlokaalst@gmail.com", "LokaalLekker123");
                    SmtpServer.EnableSsl = true;
                    SmtpServer.Send(message);

                    result = await _userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);
                        return RedirectToLocal(returnUrl);
                    }
                }
                AddErrors(result);
            }
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            ViewData["ReturnUrl"] = returnUrl;
            return View(nameof(ExternalLogin), model);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string code)
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            if (userId == null || code == null)
            {
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{userId}'.");
            }
            var result = await _userManager.ConfirmEmailAsync(user, code);
            return View(result.Succeeded ? "ConfirmEmail" : "Error");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return RedirectToAction(nameof(ForgotPasswordConfirmation));
                }

                // For more information on how to enable account confirmation and password reset please
                // visit https://go.microsoft.com/fwlink/?LinkID=532713
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = Url.ResetPasswordCallbackLink(user.Id, code, Request.Scheme);
                await _emailSender.SendEmailAsync(model.Email, "Reset Password",
                   $"Please reset your password by clicking here: <a href='{callbackUrl}'>link</a>");
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation()
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string code = null)
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            if (code == null)
            {
                throw new ApplicationException("A code must be supplied for password reset.");
            }
            var model = new ResetPasswordViewModel { Code = code };
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }
            var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }
            AddErrors(result);
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            return View();
        }


        [HttpGet]
        public IActionResult AccessDenied()
        {
            ViewData["AlleCategorien"] = _categorieRepository.GetAll().ToList();
            return View();
        }

        #region Helpers

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                if (error.Description.StartsWith("User name") && error.Description.EndsWith("is already taken."))
                    ModelState.AddModelError(string.Empty, "Het door u gekozen e-mailadres is reeds in gebruik. Gelieve een ander e-mailadres op te geven.");
                else
                    ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
        }
        #endregion
    }
}
