using System.ComponentModel.DataAnnotations;

namespace Solar.Captcha.SampleApp.Models;

public class SessionCaptchaModel : ICaptchable
{
    [Required]
    [StringLength(4)]
    public string CaptchaCode { get; set; }
}