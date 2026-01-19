using System.ComponentModel.DataAnnotations;

namespace Worker.SendNotification.Configurations
{
    public class ExternalApi
    {
        [Required(ErrorMessage = "BaseAddress is required")]
        [Url(ErrorMessage = "BaseAddress must be a valid URL")]
        public string BaseAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Path is required")]
        public string Path { get; set; } = string.Empty;
    }
}
