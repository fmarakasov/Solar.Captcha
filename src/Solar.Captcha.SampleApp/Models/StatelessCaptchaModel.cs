using System.ComponentModel.DataAnnotations;

namespace Solar.Captcha.SampleApp.Models;

public class StatelessCaptchaModel : ICaptchableWithToken
{
    [Required]
    [StringLength(4)]
    public string? CaptchaCode { get; set; }

    public string? CaptchaToken { get; set; }
}